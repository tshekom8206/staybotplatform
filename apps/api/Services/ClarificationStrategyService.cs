using Microsoft.EntityFrameworkCore;
using Hostr.Api.Data;
using Hostr.Api.Models;
using System.Text;

namespace Hostr.Api.Services;

public interface IClarificationStrategyService
{
    Task<ClarificationStrategy> DetermineBestStrategyAsync(AmbiguityResult ambiguityResult, int conversationId, int tenantId);
    Task<string> FormatClarificationMessageAsync(ClarificationStrategy strategy, string originalMessage);
    Task<bool> ShouldEscalateToClarificationAsync(AmbiguityResult ambiguityResult, ConversationState conversationState);
    Task<List<string>> GenerateFollowUpOptionsAsync(AmbiguityType ambiguityType, int tenantId);
}

public enum ClarificationApproach
{
    Direct = 1,           // "Which booking do you mean - your restaurant reservation or spa appointment?"
    Guided = 2,           // "I see you have multiple bookings. Let me help you choose."
    Contextual = 3,       // "Based on your recent requests, I think you mean..."
    Educational = 4,      // "Here's how you can be more specific next time..."
    Escalation = 5        // "Let me connect you with a staff member for assistance."
}

public class ClarificationStrategy
{
    public ClarificationApproach Approach { get; set; }
    public List<string> Questions { get; set; } = new();
    public List<object> Options { get; set; } = new();
    public string Explanation { get; set; } = string.Empty;
    public bool RequiresImmediateAction { get; set; }
    public int Priority { get; set; } = 1; // 1 = highest priority
    public TimeSpan EstimatedResolutionTime { get; set; }
    public bool CanBeAutomated { get; set; } = true;
}

public class ClarificationStrategyService : IClarificationStrategyService
{
    private readonly HostrDbContext _context;
    private readonly ILogger<ClarificationStrategyService> _logger;
    private readonly IConversationStateService _conversationStateService;
    private readonly ITemporalContextService _temporalContextService;

    // Strategy selection weights based on ambiguity type and context
    private static readonly Dictionary<AmbiguityType, Dictionary<ClarificationApproach, int>> StrategyWeights = new()
    {
        {
            AmbiguityType.MultipleOptions, new Dictionary<ClarificationApproach, int>
            {
                { ClarificationApproach.Direct, 8 },
                { ClarificationApproach.Guided, 6 },
                { ClarificationApproach.Contextual, 4 }
            }
        },
        {
            AmbiguityType.MissingContext, new Dictionary<ClarificationApproach, int>
            {
                { ClarificationApproach.Guided, 8 },
                { ClarificationApproach.Direct, 6 },
                { ClarificationApproach.Educational, 3 }
            }
        },
        {
            AmbiguityType.TemporalVague, new Dictionary<ClarificationApproach, int>
            {
                { ClarificationApproach.Contextual, 8 },
                { ClarificationApproach.Guided, 6 },
                { ClarificationApproach.Direct, 4 }
            }
        },
        {
            AmbiguityType.PrivacyViolation, new Dictionary<ClarificationApproach, int>
            {
                { ClarificationApproach.Educational, 8 },
                { ClarificationApproach.Escalation, 6 },
                { ClarificationApproach.Direct, 2 }
            }
        },
        {
            AmbiguityType.ConflictingContext, new Dictionary<ClarificationApproach, int>
            {
                { ClarificationApproach.Escalation, 8 },
                { ClarificationApproach.Direct, 6 },
                { ClarificationApproach.Guided, 4 }
            }
        },
        {
            AmbiguityType.IncompleteRequest, new Dictionary<ClarificationApproach, int>
            {
                { ClarificationApproach.Guided, 8 },
                { ClarificationApproach.Educational, 5 },
                { ClarificationApproach.Direct, 4 }
            }
        }
    };

    public ClarificationStrategyService(
        HostrDbContext context,
        ILogger<ClarificationStrategyService> logger,
        IConversationStateService conversationStateService,
        ITemporalContextService temporalContextService)
    {
        _context = context;
        _logger = logger;
        _conversationStateService = conversationStateService;
        _temporalContextService = temporalContextService;
    }

    public async Task<ClarificationStrategy> DetermineBestStrategyAsync(AmbiguityResult ambiguityResult, int conversationId, int tenantId)
    {
        try
        {
            var conversationState = await _conversationStateService.GetStateAsync(conversationId);
            var timeContext = await _temporalContextService.GetCurrentTimeContextAsync(tenantId);

            // Determine primary ambiguity type (highest confidence or first in list)
            var primaryAmbiguity = ambiguityResult.AmbiguityTypes.FirstOrDefault();
            if (primaryAmbiguity == AmbiguityType.None) primaryAmbiguity = AmbiguityType.IncompleteRequest;

            // Calculate strategy scores based on context
            var strategyScores = await CalculateStrategyScoresAsync(primaryAmbiguity, conversationState, timeContext, ambiguityResult);

            // Select best approach
            var bestApproach = strategyScores.OrderByDescending(s => s.Value).First().Key;

            // Build strategy
            var strategy = await BuildStrategyAsync(bestApproach, primaryAmbiguity, ambiguityResult, conversationState, tenantId);

            _logger.LogInformation("Selected clarification strategy: {Approach} for ambiguity: {AmbiguityType} (confidence: {Confidence})",
                bestApproach, primaryAmbiguity, ambiguityResult.Confidence);

            return strategy;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error determining clarification strategy for conversation {ConversationId}", conversationId);
            return GetFallbackStrategy(ambiguityResult);
        }
    }

    public async Task<string> FormatClarificationMessageAsync(ClarificationStrategy strategy, string originalMessage)
    {
        try
        {
            var messageBuilder = new StringBuilder();

            switch (strategy.Approach)
            {
                case ClarificationApproach.Direct:
                    messageBuilder.AppendLine(strategy.Questions.FirstOrDefault() ?? "Could you please clarify what you mean?");
                    if (strategy.Options.Any())
                    {
                        messageBuilder.AppendLine();
                        for (int i = 0; i < Math.Min(strategy.Options.Count, 5); i++)
                        {
                            messageBuilder.AppendLine($"{i + 1}. {strategy.Options[i]}");
                        }
                    }
                    break;

                case ClarificationApproach.Guided:
                    messageBuilder.AppendLine("I'd be happy to help! Let me guide you through this step by step.");
                    messageBuilder.AppendLine();
                    messageBuilder.AppendLine(strategy.Questions.FirstOrDefault() ?? "What would you like assistance with?");
                    if (strategy.Options.Any())
                    {
                        messageBuilder.AppendLine();
                        messageBuilder.AppendLine("Here are some options:");
                        foreach (var option in strategy.Options.Take(5))
                        {
                            messageBuilder.AppendLine($"• {option}");
                        }
                    }
                    break;

                case ClarificationApproach.Contextual:
                    messageBuilder.AppendLine(strategy.Explanation);
                    if (strategy.Questions.Any())
                    {
                        messageBuilder.AppendLine();
                        messageBuilder.AppendLine(strategy.Questions.First());
                    }
                    break;

                case ClarificationApproach.Educational:
                    messageBuilder.AppendLine(strategy.Explanation);
                    if (strategy.Questions.Any())
                    {
                        messageBuilder.AppendLine();
                        messageBuilder.AppendLine(strategy.Questions.First());
                    }
                    messageBuilder.AppendLine();
                    messageBuilder.AppendLine("💡 For faster service next time, try being more specific in your request.");
                    break;

                case ClarificationApproach.Escalation:
                    messageBuilder.AppendLine("I understand this might be complex. Let me connect you with a staff member who can assist you directly.");
                    messageBuilder.AppendLine();
                    messageBuilder.AppendLine("In the meantime, could you provide any additional details about what you need?");
                    break;
            }

            return messageBuilder.ToString().Trim();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error formatting clarification message");
            return "I'd like to help! Could you provide a bit more detail about what you need?";
        }
    }

    public async Task<bool> ShouldEscalateToClarificationAsync(AmbiguityResult ambiguityResult, ConversationState conversationState)
    {
        try
        {
            // Always escalate for high confidence ambiguity
            if (ambiguityResult.Confidence >= ConfidenceLevel.VeryHigh) return true;

            // Escalate for privacy violations
            if (ambiguityResult.AmbiguityTypes.Contains(AmbiguityType.PrivacyViolation)) return true;

            // Escalate for conflicting context
            if (ambiguityResult.AmbiguityTypes.Contains(AmbiguityType.ConflictingContext)) return true;

            // Escalate if too many clarifications already in this conversation
            if (conversationState.PendingClarifications.Count >= 3) return true;

            // Escalate if multiple ambiguity types detected
            if (ambiguityResult.AmbiguityTypes.Count >= 3) return true;

            // Don't escalate for low confidence or simple cases
            if (ambiguityResult.Confidence <= ConfidenceLevel.Low) return false;

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error determining escalation need");
            return false;
        }
    }

    public async Task<List<string>> GenerateFollowUpOptionsAsync(AmbiguityType ambiguityType, int tenantId)
    {
        try
        {
            var options = new List<string>();

            switch (ambiguityType)
            {
                case AmbiguityType.MultipleOptions:
                    // Get actual booking/service options from database
                    var services = await _context.Services
                        .Where(s => s.TenantId == tenantId && s.IsAvailable)
                        .OrderBy(s => s.Priority)
                        .Take(5)
                        .Select(s => s.Name)
                        .ToListAsync();
                    options.AddRange(services);
                    break;

                case AmbiguityType.TemporalVague:
                    options.AddRange(new[]
                    {
                        "Right now",
                        "This evening (6-9 PM)",
                        "Tomorrow morning",
                        "Tomorrow evening",
                        "Specific time (please tell me)"
                    });
                    break;

                case AmbiguityType.IncompleteRequest:
                    options.AddRange(new[]
                    {
                        "Room service request",
                        "Housekeeping assistance",
                        "Concierge service",
                        "Technical support",
                        "Restaurant reservation",
                        "Spa booking"
                    });
                    break;

                case AmbiguityType.MissingContext:
                    var menuItems = await _context.MenuItems
                        .Where(m => m.TenantId == tenantId && m.IsAvailable)
                        .OrderBy(m => m.MenuCategory.DisplayOrder)
                        .Take(5)
                        .Select(m => m.Name)
                        .ToListAsync();
                    options.AddRange(menuItems);
                    break;

                default:
                    options.AddRange(new[]
                    {
                        "I need room service",
                        "I have a maintenance issue",
                        "I want to make a reservation",
                        "I need assistance with my booking"
                    });
                    break;
            }

            return options;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating follow-up options for {AmbiguityType}", ambiguityType);
            return new List<string> { "Please provide more details", "Let me know how I can help" };
        }
    }

    private async Task<Dictionary<ClarificationApproach, int>> CalculateStrategyScoresAsync(
        AmbiguityType primaryAmbiguity,
        ConversationState conversationState,
        TimeContext timeContext,
        AmbiguityResult ambiguityResult)
    {
        var scores = new Dictionary<ClarificationApproach, int>();

        // Get base weights for this ambiguity type
        if (StrategyWeights.TryGetValue(primaryAmbiguity, out var baseWeights))
        {
            foreach (var weight in baseWeights)
            {
                scores[weight.Key] = weight.Value;
            }
        }
        else
        {
            // Default weights
            scores[ClarificationApproach.Guided] = 5;
            scores[ClarificationApproach.Direct] = 4;
            scores[ClarificationApproach.Contextual] = 3;
        }

        // Adjust scores based on conversation context
        if (conversationState.MessageCount <= 2)
        {
            // Early in conversation - prefer guided approach
            scores[ClarificationApproach.Guided] += 3;
            scores[ClarificationApproach.Educational] += 2;
        }
        else if (conversationState.MessageCount >= 5)
        {
            // Later in conversation - prefer direct approach
            scores[ClarificationApproach.Direct] += 3;
            scores[ClarificationApproach.Escalation] += 2;
        }

        // Adjust based on previous clarifications
        if (conversationState.PendingClarifications.Count >= 2)
        {
            scores[ClarificationApproach.Escalation] += 4;
            scores[ClarificationApproach.Direct] += 2;
        }

        // Adjust based on confidence level
        if (ambiguityResult.Confidence >= ConfidenceLevel.VeryHigh)
        {
            scores[ClarificationApproach.Direct] += 3;
            scores[ClarificationApproach.Escalation] += 2;
        }

        // Adjust based on business hours
        if (!timeContext.IsBusinessHours)
        {
            scores[ClarificationApproach.Escalation] -= 3; // Avoid escalation after hours
            scores[ClarificationApproach.Guided] += 2;     // Prefer self-service
        }

        return scores;
    }

    private async Task<ClarificationStrategy> BuildStrategyAsync(
        ClarificationApproach approach,
        AmbiguityType ambiguityType,
        AmbiguityResult ambiguityResult,
        ConversationState conversationState,
        int tenantId)
    {
        var strategy = new ClarificationStrategy
        {
            Approach = approach,
            Priority = DeterminePriority(ambiguityType, ambiguityResult.Confidence),
            RequiresImmediateAction = IsUrgent(ambiguityType),
            EstimatedResolutionTime = EstimateResolutionTime(approach, ambiguityType),
            CanBeAutomated = approach != ClarificationApproach.Escalation
        };

        // Add questions from ambiguity result
        strategy.Questions.AddRange(ambiguityResult.ClarificationQuestions);

        // Add context-specific options
        var followUpOptions = await GenerateFollowUpOptionsAsync(ambiguityType, tenantId);
        strategy.Options.AddRange(followUpOptions.Cast<object>());

        // Add explanations based on approach
        strategy.Explanation = approach switch
        {
            ClarificationApproach.Contextual => GenerateContextualExplanation(ambiguityResult, conversationState),
            ClarificationApproach.Educational => GenerateEducationalExplanation(ambiguityType),
            ClarificationApproach.Escalation => "This requires specialized assistance to resolve properly.",
            _ => ambiguityResult.Explanation
        };

        return strategy;
    }

    private string GenerateContextualExplanation(AmbiguityResult ambiguityResult, ConversationState conversationState)
    {
        if (!string.IsNullOrEmpty(conversationState.LastUserMessage))
        {
            return $"Based on your previous message about '{conversationState.LastUserMessage.Substring(0, Math.Min(30, conversationState.LastUserMessage.Length))}...', I think you might be referring to something specific.";
        }
        return "Based on our conversation, I'd like to clarify what you're looking for.";
    }

    private string GenerateEducationalExplanation(AmbiguityType ambiguityType)
    {
        return ambiguityType switch
        {
            AmbiguityType.PrivacyViolation => "I need to protect guest privacy and cannot share information about other guests' locations or bookings.",
            AmbiguityType.TemporalVague => "To help you better, it would be helpful to know the specific time you have in mind.",
            AmbiguityType.MultipleOptions => "I see you might be referring to one of several options you have available.",
            _ => "To provide the best assistance, a bit more detail would be helpful."
        };
    }

    private int DeterminePriority(AmbiguityType ambiguityType, ConfidenceLevel confidence)
    {
        var basePriority = ambiguityType switch
        {
            AmbiguityType.PrivacyViolation => 1,       // Highest priority
            AmbiguityType.ConflictingContext => 1,
            AmbiguityType.MultipleOptions => 2,
            AmbiguityType.TemporalVague => 3,
            AmbiguityType.IncompleteRequest => 4,
            _ => 5                                      // Lowest priority
        };

        // Adjust for confidence
        if (confidence >= ConfidenceLevel.VeryHigh) basePriority = Math.Max(1, basePriority - 1);
        if (confidence <= ConfidenceLevel.Low) basePriority = Math.Min(5, basePriority + 1);

        return basePriority;
    }

    private bool IsUrgent(AmbiguityType ambiguityType)
    {
        return ambiguityType == AmbiguityType.PrivacyViolation ||
               ambiguityType == AmbiguityType.ConflictingContext;
    }

    private TimeSpan EstimateResolutionTime(ClarificationApproach approach, AmbiguityType ambiguityType)
    {
        return approach switch
        {
            ClarificationApproach.Direct => TimeSpan.FromMinutes(1),
            ClarificationApproach.Guided => TimeSpan.FromMinutes(2),
            ClarificationApproach.Contextual => TimeSpan.FromMinutes(1.5),
            ClarificationApproach.Educational => TimeSpan.FromMinutes(3),
            ClarificationApproach.Escalation => TimeSpan.FromMinutes(5),
            _ => TimeSpan.FromMinutes(2)
        };
    }

    private ClarificationStrategy GetFallbackStrategy(AmbiguityResult ambiguityResult)
    {
        return new ClarificationStrategy
        {
            Approach = ClarificationApproach.Guided,
            Questions = new List<string> { "I'd like to help! Could you provide a bit more detail about what you need?" },
            Options = new List<object> { "Room service", "Housekeeping", "Concierge", "Technical support" },
            Explanation = "I want to make sure I understand your request correctly.",
            RequiresImmediateAction = false,
            Priority = 3,
            EstimatedResolutionTime = TimeSpan.FromMinutes(2),
            CanBeAutomated = true
        };
    }
}