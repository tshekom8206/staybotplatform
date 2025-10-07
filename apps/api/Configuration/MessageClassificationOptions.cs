namespace Hostr.Api.Configuration;

public enum ClassificationMode
{
    RegexOnly,
    LLMOnly,
    Hybrid
}

public class MessageClassificationOptions
{
    public const string SectionName = "MessageClassification";
    
    /// <summary>
    /// Classification mode: RegexOnly, LLMOnly, or Hybrid
    /// </summary>
    public ClassificationMode Mode { get; set; } = ClassificationMode.Hybrid;
    
    /// <summary>
    /// Confidence threshold for regex classification to be considered high-confidence
    /// </summary>
    public double RegexConfidenceThreshold { get; set; } = 0.8;
    
    /// <summary>
    /// Minimum confidence threshold for LLM classification results
    /// </summary>
    public double LLMConfidenceThreshold { get; set; } = 0.5;
    
    /// <summary>
    /// Whether to use LLM for ambiguous cases in Hybrid mode
    /// </summary>
    public bool EnableLLMForAmbiguous { get; set; } = true;
    
    /// <summary>
    /// Confidence threshold for greeting detection
    /// </summary>
    public double GreetingConfidenceThreshold { get; set; } = 0.6;
    
    /// <summary>
    /// Maximum number of LLM requests per minute to prevent API overuse
    /// </summary>
    public int MaxLLMRequestsPerMinute { get; set; } = 60;
    
    /// <summary>
    /// Enable detailed logging for classification decisions
    /// </summary>
    public bool EnableClassificationLogging { get; set; } = true;
    
    /// <summary>
    /// Check if current mode allows LLM usage
    /// </summary>
    public bool IsLLMEnabled => Mode == ClassificationMode.LLMOnly || 
                               (Mode == ClassificationMode.Hybrid && EnableLLMForAmbiguous);
    
    /// <summary>
    /// Check if current mode allows regex usage
    /// </summary>
    public bool IsRegexEnabled => Mode == ClassificationMode.RegexOnly || 
                                 Mode == ClassificationMode.Hybrid;
}