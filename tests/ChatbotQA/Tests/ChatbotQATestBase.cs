using Hostr.Tests.ChatbotQA.Services;
using Microsoft.Extensions.Configuration;
using Xunit.Abstractions;

namespace Hostr.Tests.ChatbotQA.Tests;

public abstract class ChatbotQATestBase
{
    protected readonly ITestOutputHelper Output;
    protected readonly ChatbotEvaluatorService Evaluator;
    protected readonly TestCaseLoader Loader;
    protected readonly ResultAggregator Aggregator;
    protected readonly string TestDataPath;

    protected ChatbotQATestBase(ITestOutputHelper output)
    {
        Output = output;

        // Get OpenAI API key from environment variable or user secrets
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
                     ?? GetApiKeyFromUserSecrets()
                     ?? throw new InvalidOperationException("OpenAI API key not found. Set OPENAI_API_KEY environment variable or add to user secrets.");

        // Use gpt-3.5-turbo for cost-effective testing, or gpt-4 for higher accuracy
        var model = Environment.GetEnvironmentVariable("OPENAI_MODEL") ?? "gpt-3.5-turbo";

        Evaluator = new ChatbotEvaluatorService(apiKey, model);
        Loader = new TestCaseLoader();
        Aggregator = new ResultAggregator();

        // Path to test data
        TestDataPath = Path.Combine(GetProjectRoot(), "ChatbotQA", "Data");
    }

    private string? GetApiKeyFromUserSecrets()
    {
        try
        {
            var configuration = new ConfigurationBuilder()
                .AddUserSecrets<ChatbotQATestBase>()
                .Build();

            return configuration["OpenAI:ApiKey"];
        }
        catch
        {
            return null;
        }
    }

    private string GetProjectRoot()
    {
        var directory = Directory.GetCurrentDirectory();

        // Navigate up to find the tests folder
        while (directory != null && !Path.GetFileName(directory).Equals("tests", StringComparison.OrdinalIgnoreCase))
        {
            directory = Directory.GetParent(directory)?.FullName;
        }

        return directory ?? throw new InvalidOperationException("Could not find tests folder");
    }

    protected void WriteResults(List<Models.EvaluationResult> results)
    {
        foreach (var result in results)
        {
            Output.WriteLine($"\n=== {result.CaseId} ===");
            Output.WriteLine($"Verdict: {result.Verdict}");
            Output.WriteLine($"Scores: U={result.Scores.Understanding:F2}, A={result.Scores.Accuracy:F2}, C={result.Scores.Completeness:F2}, P={result.Scores.PolicyCompliance:F2}, T={result.Scores.Tone:F2}");
            Output.WriteLine($"Average: {result.Scores.Average:F2}");

            if (result.Hallucination)
            {
                Output.WriteLine("⚠️ HALLUCINATION DETECTED");
            }

            if (result.Issues.Any())
            {
                Output.WriteLine($"Issues: {string.Join("; ", result.Issues)}");
            }

            if (!string.IsNullOrEmpty(result.RecommendedFix))
            {
                Output.WriteLine($"Fix: {result.RecommendedFix}");
            }
        }
    }

    protected void WriteSummary(AggregatedMetrics metrics)
    {
        Output.WriteLine("\n" + new string('=', 50));
        Output.WriteLine("SUMMARY");
        Output.WriteLine(new string('=', 50));
        Output.WriteLine(metrics.ToString());
    }
}
