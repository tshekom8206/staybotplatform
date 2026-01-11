using System.Text.Json.Serialization;

namespace Hostr.Tests.ChatbotQA.Models;

public class ScoringCriteria
{
    [JsonPropertyName("understanding")]
    public double Understanding { get; set; }

    [JsonPropertyName("accuracy")]
    public double Accuracy { get; set; }

    [JsonPropertyName("completeness")]
    public double Completeness { get; set; }

    [JsonPropertyName("policy_compliance")]
    public double PolicyCompliance { get; set; }

    [JsonPropertyName("tone")]
    public double Tone { get; set; }

    public double Average => (Understanding + Accuracy + Completeness + PolicyCompliance + Tone) / 5.0;

    public double WeightedAverage(ScoringWeights? weights = null)
    {
        weights ??= ScoringWeights.Default;
        return (Understanding * weights.Understanding) +
               (Accuracy * weights.Accuracy) +
               (Completeness * weights.Completeness) +
               (PolicyCompliance * weights.PolicyCompliance) +
               (Tone * weights.Tone);
    }
}

public class ScoringWeights
{
    public double Understanding { get; set; }
    public double Accuracy { get; set; }
    public double Completeness { get; set; }
    public double PolicyCompliance { get; set; }
    public double Tone { get; set; }

    public static ScoringWeights Default => new()
    {
        Understanding = 0.15,
        Accuracy = 0.40,
        Completeness = 0.20,
        PolicyCompliance = 0.20,
        Tone = 0.05
    };

    public static ScoringWeights Equal => new()
    {
        Understanding = 0.20,
        Accuracy = 0.20,
        Completeness = 0.20,
        PolicyCompliance = 0.20,
        Tone = 0.20
    };
}
