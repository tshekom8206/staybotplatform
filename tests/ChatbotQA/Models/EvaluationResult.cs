using System.Text.Json.Serialization;

namespace Hostr.Tests.ChatbotQA.Models;

public class EvaluationResult
{
    [JsonPropertyName("case_id")]
    public string CaseId { get; set; } = string.Empty;

    [JsonPropertyName("scores")]
    public ScoringCriteria Scores { get; set; } = new();

    [JsonPropertyName("verdict")]
    public string Verdict { get; set; } = string.Empty;

    [JsonPropertyName("issues")]
    public List<string> Issues { get; set; } = new();

    [JsonPropertyName("hallucination")]
    public bool Hallucination { get; set; }

    [JsonPropertyName("recommended_fix")]
    public string RecommendedFix { get; set; } = string.Empty;

    public bool IsAccurate => Verdict == "accurate";
    public bool HasHardViolation => Scores.PolicyCompliance == 0 || Hallucination;
}
