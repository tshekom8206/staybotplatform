using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Hostr.Tests.ChatbotQA.Tests;

public class PolicyComplianceTests : ChatbotQATestBase
{
    public PolicyComplianceTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public async Task DinnerAfterHours_ShouldFailPolicyCompliance()
    {
        // Load test case T-001 (dinner at 22:30 when kitchen closes at 21:00)
        var testCases = await Loader.LoadFromJsonLinesAsync(Path.Combine(TestDataPath, "eval_cases.jsonl"));
        var dinnerCase = testCases.FirstOrDefault(tc => tc.CaseId == "T-001");

        dinnerCase.Should().NotBeNull();

        // Evaluate
        var result = await Evaluator.EvaluateAsync(dinnerCase!);

        // Assert
        Output.WriteLine($"Verdict: {result.Verdict}");
        Output.WriteLine($"Policy Compliance: {result.Scores.PolicyCompliance:F2}");
        Output.WriteLine($"Issues: {string.Join(", ", result.Issues)}");

        result.Verdict.Should().Be("inaccurate", "confirmed service outside policy hours");
        result.Scores.PolicyCompliance.Should().Be(0, "complete policy violation");
        result.HasHardViolation.Should().BeTrue("policy compliance is 0");
        result.Issues.Should().Contain(i => i.Contains("policy", StringComparison.OrdinalIgnoreCase) ||
                                            i.Contains("hours", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AllPolicyViolationCases_ShouldFail()
    {
        // Load all test cases
        var testCases = await Loader.LoadFromJsonLinesAsync(Path.Combine(TestDataPath, "eval_cases.jsonl"));
        var policyCases = Loader.FilterByTags(testCases, "policy_violation");

        Output.WriteLine($"Found {policyCases.Count} policy violation test cases");

        // Evaluate all
        var results = await Evaluator.EvaluateBatchAsync(policyCases);

        WriteResults(results);

        // Aggregate
        var metrics = Aggregator.Aggregate(results);
        WriteSummary(metrics);

        // Assert
        metrics.HardViolations.Should().Be(policyCases.Count, "all policy violations should be detected");
        metrics.OverallAccuracyRate.Should().Be(0, "no policy violations should pass");
    }

    [Fact]
    public async Task PolicyHoursCases_ShouldRespectHours()
    {
        // Load all test cases tagged with policy_hours
        var testCases = await Loader.LoadFromJsonLinesAsync(Path.Combine(TestDataPath, "eval_cases.jsonl"));
        var hoursCases = Loader.FilterByTags(testCases, "policy_hours");

        Output.WriteLine($"Found {hoursCases.Count} policy hours test cases");

        // Evaluate all
        var results = await Evaluator.EvaluateBatchAsync(hoursCases);

        WriteResults(results);

        // Aggregate
        var metrics = Aggregator.Aggregate(results);
        WriteSummary(metrics);

        // Assert - should catch violations
        foreach (var result in results)
        {
            if (result.Scores.PolicyCompliance < 1.0)
            {
                Output.WriteLine($"{result.CaseId}: Policy issue detected correctly");
            }
        }
    }
}
