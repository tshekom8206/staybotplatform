using Hostr.Api.Models;

namespace Hostr.Api.Services;

public interface ILLMBusinessRulesEngine
{
    Task<BusinessRuleAnalysis> AnalyzeMessageAsync(
        string message,
        TenantContext tenantContext,
        GuestStatus guestStatus,
        ConversationContext conversationContext);

    Task<List<BusinessRuleViolation>> ValidateBusinessRulesAsync(
        BusinessRuleAnalysis analysis,
        TenantContext tenantContext);
}

public class BusinessRuleAnalysis
{
    public string PrimaryIntent { get; set; } = string.Empty; // REQUEST_ITEM, REQUEST_SERVICE, BOOKING, COMPLAINT, etc.
    public string ServiceCategory { get; set; } = string.Empty; // FOOD_BEVERAGE, SPA_WELLNESS, MAINTENANCE, etc.
    public string SpecificItem { get; set; } = string.Empty; // sparkling_water, spa_massage, etc.
    public double OverallConfidence { get; set; }
    public Dictionary<string, double> CategoryConfidences { get; set; } = new();
    public ContextFactors ContextFactors { get; set; } = new();
    public List<string> DetectedKeywords { get; set; } = new();
    public string RawLLMResponse { get; set; } = string.Empty;
    public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
}

public class ContextFactors
{
    public bool TimeRelevant { get; set; }
    public bool LocationRelevant { get; set; }
    public bool GuestStatusRelevant { get; set; }
    public bool ConversationContextRelevant { get; set; }
    public List<string> RelevantServices { get; set; } = new();
    public List<string> ExcludedServices { get; set; } = new();
}

public class BusinessRuleViolation
{
    public string RuleName { get; set; } = string.Empty;
    public string RuleType { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty; // INFO, WARNING, BLOCK, ESCALATE
    public string Message { get; set; } = string.Empty;
    public Dictionary<string, object> Context { get; set; } = new();
    public double Confidence { get; set; }
    public List<string> Recommendations { get; set; } = new();
}

public class ConversationContext
{
    public int ConversationId { get; set; }
    public List<string> RecentMessages { get; set; } = new();
    public Dictionary<string, object> ConversationState { get; set; } = new();
    public DateTime LastInteraction { get; set; }
    public string CurrentTopic { get; set; } = string.Empty;
}

public class SemanticRule
{
    public string RuleName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> ServiceTypes { get; set; } = new();
    public List<string> RequiredConditions { get; set; } = new();
    public List<string> ExcludedItems { get; set; } = new();
    public string TimeConstraints { get; set; } = string.Empty;
    public List<GuestType> RequiredGuestType { get; set; } = new();
    public double MinimumConfidence { get; set; } = 0.7;
    public string Severity { get; set; } = "WARNING";
    public bool IsActive { get; set; } = true;
}

public class LLMBusinessRuleRequest
{
    public string Message { get; set; } = string.Empty;
    public TenantContext TenantContext { get; set; } = new();
    public GuestStatus GuestStatus { get; set; }
    public ConversationContext ConversationContext { get; set; } = new();
    public List<string> AvailableServices { get; set; } = new();
    public List<string> AvailableItems { get; set; } = new();
    public DateTime CurrentTime { get; set; } = DateTime.UtcNow;
}

public class LLMBusinessRuleResponse
{
    public string PrimaryIntent { get; set; } = string.Empty;
    public string ServiceCategory { get; set; } = string.Empty;
    public string SpecificItem { get; set; } = string.Empty;
    public double OverallConfidence { get; set; }
    public Dictionary<string, double> CategoryConfidences { get; set; } = new();
    public ContextFactors ContextFactors { get; set; } = new();
    public List<string> DetectedKeywords { get; set; } = new();
    public List<string> RelevantBusinessRules { get; set; } = new();
    public string ReasoningExplanation { get; set; } = string.Empty;
}