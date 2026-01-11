using System.Text.Json;
using Hostr.Tests.ChatbotQA.Models;
using Microsoft.Extensions.Logging;

namespace Hostr.Tests.ChatbotQA.Services;

public class TestCaseLoader
{
    private readonly ILogger<TestCaseLoader> _logger;

    public TestCaseLoader(ILogger<TestCaseLoader>? logger = null)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<TestCaseLoader>.Instance;
    }

    /// <summary>
    /// Loads test cases from a JSONL file (one JSON object per line)
    /// </summary>
    public async Task<List<TestCase>> LoadFromJsonLinesAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Test case file not found: {filePath}");
        }

        var testCases = new List<TestCase>();
        var lineNumber = 0;

        await foreach (var line in File.ReadLinesAsync(filePath, cancellationToken))
        {
            lineNumber++;

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                var testCase = JsonSerializer.Deserialize<TestCase>(line, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (testCase != null)
                {
                    testCases.Add(testCase);
                }
                else
                {
                    _logger.LogWarning("Failed to deserialize line {LineNumber} in {FilePath}", lineNumber, filePath);
                }
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "JSON error on line {LineNumber} in {FilePath}", lineNumber, filePath);
            }
        }

        _logger.LogInformation("Loaded {Count} test cases from {FilePath}", testCases.Count, filePath);
        return testCases;
    }

    /// <summary>
    /// Loads test cases from a standard JSON array file
    /// </summary>
    public async Task<List<TestCase>> LoadFromJsonArrayAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Test case file not found: {filePath}");
        }

        var json = await File.ReadAllTextAsync(filePath, cancellationToken);
        var testCases = JsonSerializer.Deserialize<List<TestCase>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (testCases == null)
        {
            throw new InvalidOperationException($"Failed to deserialize test cases from {filePath}");
        }

        _logger.LogInformation("Loaded {Count} test cases from {FilePath}", testCases.Count, filePath);
        return testCases;
    }

    /// <summary>
    /// Filters test cases by tags
    /// </summary>
    public List<TestCase> FilterByTags(List<TestCase> testCases, params string[] tags)
    {
        if (tags.Length == 0)
        {
            return testCases;
        }

        return testCases.Where(tc => tc.Tags.Any(t => tags.Contains(t, StringComparer.OrdinalIgnoreCase))).ToList();
    }
}
