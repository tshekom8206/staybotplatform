using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Hostr.Tests.ChatbotQA.Tests;

public class FullSuiteTests : ChatbotQATestBase
{
    public FullSuiteTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public async Task RunFullEvaluationSuite()
    {
        // Load all test cases
        var testCases = await Loader.LoadFromJsonLinesAsync(Path.Combine(TestDataPath, "eval_cases.jsonl"));

        Output.WriteLine($"Loaded {testCases.Count} test cases");
        Output.WriteLine($"Tags: {string.Join(", ", testCases.SelectMany(tc => tc.Tags).Distinct())}");

        // Evaluate all cases
        var results = await Evaluator.EvaluateBatchAsync(testCases);

        WriteResults(results);

        // Overall metrics
        var overallMetrics = Aggregator.Aggregate(results);
        WriteSummary(overallMetrics);

        // Metrics by tag
        var metricsByTag = Aggregator.AggregateByTag(testCases, results);

        Output.WriteLine("\n" + new string('=', 50));
        Output.WriteLine("METRICS BY TAG");
        Output.WriteLine(new string('=', 50));

        foreach (var (tag, metrics) in metricsByTag.OrderByDescending(kvp => kvp.Value.TotalCases))
        {
            Output.WriteLine($"\n--- {tag} ---");
            Output.WriteLine(metrics.ToString());
        }

        // Assertions
        overallMetrics.TotalCases.Should().Be(testCases.Count);
        overallMetrics.Hallucinations.Should().BeGreaterThan(0, "should detect hallucinations in test suite");

        // Accurate cases should have high scores
        var accurateCases = results.Where(r => r.Verdict == "accurate").ToList();
        if (accurateCases.Any())
        {
            var avgAccurateScore = accurateCases.Average(r => r.Scores.Average);
            avgAccurateScore.Should().BeGreaterThanOrEqualTo(0.85, "accurate cases should have high average scores");
        }

        // Inaccurate cases should have low scores or hard violations
        var inaccurateCases = results.Where(r => r.Verdict == "inaccurate").ToList();
        foreach (var inaccurateCase in inaccurateCases)
        {
            var hasHardViolation = inaccurateCase.HasHardViolation;
            var hasLowScore = inaccurateCase.Scores.Average <= 0.7;

            (hasHardViolation || hasLowScore).Should().BeTrue(
                $"Case {inaccurateCase.CaseId} marked inaccurate should have hard violation or score <= 0.7");
        }
    }

    [Fact]
    public async Task ValidateHallucinationFixes()
    {
        // This test specifically validates the hallucination fixes we implemented
        var testCases = await Loader.LoadFromJsonLinesAsync(Path.Combine(TestDataPath, "eval_cases.jsonl"));

        // Get charger test cases - both T-004 and T-013 should now be non-hallucinating
        var chargerT004 = testCases.FirstOrDefault(tc => tc.CaseId == "T-004"); // Fixed - was hallucinating
        var chargerT013 = testCases.FirstOrDefault(tc => tc.CaseId == "T-013"); // Always correct

        chargerT004.Should().NotBeNull();
        chargerT013.Should().NotBeNull();

        // Evaluate both
        var t004Result = await Evaluator.EvaluateAsync(chargerT004!);
        var t013Result = await Evaluator.EvaluateAsync(chargerT013!);

        Output.WriteLine("\n=== T-004 (FIXED) ===");
        Output.WriteLine($"Verdict: {t004Result.Verdict}");
        Output.WriteLine($"Hallucination: {t004Result.Hallucination}");
        Output.WriteLine($"Accuracy: {t004Result.Scores.Accuracy:F2}");

        Output.WriteLine("\n=== T-013 (ALWAYS CORRECT) ===");
        Output.WriteLine($"Verdict: {t013Result.Verdict}");
        Output.WriteLine($"Hallucination: {t013Result.Hallucination}");
        Output.WriteLine($"Accuracy: {t013Result.Scores.Accuracy:F2}");

        // Assert: Both should now be non-hallucinating after fix
        t004Result.Hallucination.Should().BeFalse("T-004 was fixed to list only database chargers");
        t013Result.Hallucination.Should().BeFalse("T-013 correctly lists database chargers");

        // Both should have reasonable scores
        t004Result.Scores.Average.Should().BeGreaterThanOrEqualTo(0.7, "T-004 should have good score after fix");
        t013Result.Scores.Average.Should().BeGreaterThanOrEqualTo(0.9, "T-013 should have excellent score");
    }
}
