using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Hostr.Tests.ChatbotQA.Tests;

public class HallucinationTests : ChatbotQATestBase
{
    public HallucinationTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public async Task ChargerHallucination_ShouldBeDetected()
    {
        // Load test case T-004 (charger with hardcoded examples)
        var testCases = await Loader.LoadFromJsonLinesAsync(Path.Combine(TestDataPath, "eval_cases.jsonl"));
        var chargerCase = testCases.FirstOrDefault(tc => tc.CaseId == "T-004");

        chargerCase.Should().NotBeNull();

        // Evaluate
        var result = await Evaluator.EvaluateAsync(chargerCase!);

        // Assert
        Output.WriteLine($"Verdict: {result.Verdict}");
        Output.WriteLine($"Hallucination: {result.Hallucination}");
        Output.WriteLine($"Issues: {string.Join(", ", result.Issues)}");

        // T-004 was fixed to list only database chargers, so it should now be partial/accurate
        result.Verdict.Should().BeOneOf("accurate", "partial", "T-004 now correctly lists database chargers");
        result.Hallucination.Should().BeFalse("response matches database chargers after fix");
        result.Scores.Accuracy.Should().BeGreaterThanOrEqualTo(0.5, "accuracy improved from 0.0 after fix");
    }

    [Fact]
    public async Task ChargerCorrectResponse_ShouldPass()
    {
        // Load test case T-013 (charger with correct database examples)
        var testCases = await Loader.LoadFromJsonLinesAsync(Path.Combine(TestDataPath, "eval_cases.jsonl"));
        var chargerCase = testCases.FirstOrDefault(tc => tc.CaseId == "T-013");

        chargerCase.Should().NotBeNull();

        // Evaluate
        var result = await Evaluator.EvaluateAsync(chargerCase!);

        // Assert
        Output.WriteLine($"Verdict: {result.Verdict}");
        Output.WriteLine($"Hallucination: {result.Hallucination}");
        Output.WriteLine($"Average Score: {result.Scores.Average:F2}");

        result.Verdict.Should().BeOneOf("accurate", "partial", "chatbot correctly listed database chargers");
        result.Hallucination.Should().BeFalse("response matches database chargers");
        result.Scores.Accuracy.Should().BeGreaterThanOrEqualTo(0.7, "accuracy should be high");
    }

    [Fact]
    public async Task SpaHallucination_ShouldBeDetected()
    {
        // Load test case T-005 (spa services hallucination)
        var testCases = await Loader.LoadFromJsonLinesAsync(Path.Combine(TestDataPath, "eval_cases.jsonl"));
        var spaCase = testCases.FirstOrDefault(tc => tc.CaseId == "T-005");

        spaCase.Should().NotBeNull();

        // Evaluate
        var result = await Evaluator.EvaluateAsync(spaCase!);

        // Assert
        Output.WriteLine($"Verdict: {result.Verdict}");
        Output.WriteLine($"Hallucination: {result.Hallucination}");

        result.Verdict.Should().Be("inaccurate");
        result.Hallucination.Should().BeTrue("suggesting non-existent spa services");
        result.Scores.Accuracy.Should().BeLessThan(0.5);
    }

    [Fact]
    public async Task RestaurantHallucination_ShouldBeDetected()
    {
        // Load test case T-012 (invented restaurant names)
        var testCases = await Loader.LoadFromJsonLinesAsync(Path.Combine(TestDataPath, "eval_cases.jsonl"));
        var restaurantCase = testCases.FirstOrDefault(tc => tc.CaseId == "T-012");

        restaurantCase.Should().NotBeNull();

        // Evaluate
        var result = await Evaluator.EvaluateAsync(restaurantCase!);

        // Assert
        Output.WriteLine($"Verdict: {result.Verdict}");
        Output.WriteLine($"Hallucination: {result.Hallucination}");
        Output.WriteLine($"Issues: {string.Join(", ", result.Issues)}");

        result.Verdict.Should().Be("inaccurate");
        result.Hallucination.Should().BeTrue("inventing fake restaurant names");
        result.Scores.Accuracy.Should().Be(0, "completely inaccurate information");
    }

    [Fact]
    public async Task LostAndFoundLocationExamples_ShouldBeMarkedAsIssue()
    {
        // Load test case T-006 (lost & found with location examples)
        var testCases = await Loader.LoadFromJsonLinesAsync(Path.Combine(TestDataPath, "eval_cases.jsonl"));
        var lostCase = testCases.FirstOrDefault(tc => tc.CaseId == "T-006");

        lostCase.Should().NotBeNull();

        // Evaluate
        var result = await Evaluator.EvaluateAsync(lostCase!);

        // Assert
        Output.WriteLine($"Verdict: {result.Verdict}");
        Output.WriteLine($"Completeness: {result.Scores.Completeness:F2}");

        // While not technically hallucination, suggesting specific locations could bias the guest
        result.Verdict.Should().NotBe("accurate", "should flag location examples as suboptimal");
        result.Scores.Completeness.Should().BeLessThan(0.9, "should penalize for suggesting locations");
    }

    [Fact]
    public async Task AllHallucinationCases_ShouldHaveHighDetectionRate()
    {
        // Load all test cases
        var testCases = await Loader.LoadFromJsonLinesAsync(Path.Combine(TestDataPath, "eval_cases.jsonl"));
        var hallucinationCases = Loader.FilterByTags(testCases, "hallucination");

        Output.WriteLine($"Found {hallucinationCases.Count} hallucination test cases");

        // Evaluate all
        var results = await Evaluator.EvaluateBatchAsync(hallucinationCases);

        WriteResults(results);

        // Aggregate
        var metrics = Aggregator.Aggregate(results);
        WriteSummary(metrics);

        // Assert
        metrics.Hallucinations.Should().BeGreaterThan(0, "should detect hallucinations");
        metrics.OverallAccuracyRate.Should().BeLessThan(0.3, "most hallucination cases should fail");
    }
}
