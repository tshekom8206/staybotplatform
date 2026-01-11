using Hostr.Tests.ChatbotQA.Models;

namespace Hostr.Tests.ChatbotQA.Services;

public class ResultAggregator
{
    public AggregatedMetrics Aggregate(List<EvaluationResult> results)
    {
        if (results.Count == 0)
        {
            return new AggregatedMetrics();
        }

        var metrics = new AggregatedMetrics
        {
            TotalCases = results.Count,
            AccurateCases = results.Count(r => r.Verdict == "accurate"),
            PartialCases = results.Count(r => r.Verdict == "partial"),
            InaccurateCases = results.Count(r => r.Verdict == "inaccurate"),
            HardViolations = results.Count(r => r.HasHardViolation),
            Hallucinations = results.Count(r => r.Hallucination),

            AverageUnderstanding = results.Average(r => r.Scores.Understanding),
            AverageAccuracy = results.Average(r => r.Scores.Accuracy),
            AverageCompleteness = results.Average(r => r.Scores.Completeness),
            AveragePolicyCompliance = results.Average(r => r.Scores.PolicyCompliance),
            AverageTone = results.Average(r => r.Scores.Tone),
        };

        metrics.OverallAccuracyRate = (double)metrics.AccurateCases / metrics.TotalCases;
        metrics.AverageScore = (metrics.AverageUnderstanding + metrics.AverageAccuracy +
                               metrics.AverageCompleteness + metrics.AveragePolicyCompliance +
                               metrics.AverageTone) / 5.0;

        return metrics;
    }

    public Dictionary<string, AggregatedMetrics> AggregateByTag(List<TestCase> testCases, List<EvaluationResult> results)
    {
        var resultDict = results.ToDictionary(r => r.CaseId);
        var metricsByTag = new Dictionary<string, AggregatedMetrics>();

        // Get all unique tags
        var allTags = testCases.SelectMany(tc => tc.Tags).Distinct().ToList();

        foreach (var tag in allTags)
        {
            var taggedCases = testCases.Where(tc => tc.Tags.Contains(tag)).ToList();
            var taggedResults = taggedCases
                .Select(tc => resultDict.GetValueOrDefault(tc.CaseId))
                .Where(r => r != null)
                .Cast<EvaluationResult>()
                .ToList();

            if (taggedResults.Any())
            {
                metricsByTag[tag] = Aggregate(taggedResults);
            }
        }

        return metricsByTag;
    }
}

public class AggregatedMetrics
{
    public int TotalCases { get; set; }
    public int AccurateCases { get; set; }
    public int PartialCases { get; set; }
    public int InaccurateCases { get; set; }
    public int HardViolations { get; set; }
    public int Hallucinations { get; set; }

    public double OverallAccuracyRate { get; set; }
    public double AverageScore { get; set; }

    public double AverageUnderstanding { get; set; }
    public double AverageAccuracy { get; set; }
    public double AverageCompleteness { get; set; }
    public double AveragePolicyCompliance { get; set; }
    public double AverageTone { get; set; }

    public override string ToString()
    {
        return $@"Total Cases: {TotalCases}
Accurate: {AccurateCases} ({OverallAccuracyRate:P1})
Partial: {PartialCases}
Inaccurate: {InaccurateCases}
Hard Violations: {HardViolations}
Hallucinations: {Hallucinations}

Average Scores:
  Understanding: {AverageUnderstanding:F2}
  Accuracy: {AverageAccuracy:F2}
  Completeness: {AverageCompleteness:F2}
  Policy Compliance: {AveragePolicyCompliance:F2}
  Tone: {AverageTone:F2}
  Overall: {AverageScore:F2}";
    }
}
