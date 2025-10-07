using Microsoft.EntityFrameworkCore;
using Hostr.Api.Data;
using Hostr.Api.Models;
using System.Text.Json;

namespace Hostr.Api.Services;

public interface IEnhancedResponseGeneratorService
{
    Task<ResponseGenerationResult> GenerateContextAwareResponseAsync(ResponseGenerationRequest request);
    Task<string> EnhanceResponseWithContextAsync(string baseResponse, ResponseContext context);
    Task<ResponsePersonality> GetResponsePersonalityAsync(int tenantId, string messageType);
    Task<string> ApplyTemporalAwarenessAsync(string response, TimeContext timeContext);
    Task<List<ResponseSuggestion>> GenerateFollowUpSuggestionsAsync(ResponseContext context);
}

public enum ResponseTone
{
    Professional = 1,
    Friendly = 2,
    Casual = 3,
    Formal = 4,
    Empathetic = 5,
    Urgent = 6,
    Reassuring = 7
}

public enum ResponseComplexity
{
    Simple = 1,
    Detailed = 2,
    Comprehensive = 3,
    Technical = 4,
    Conversational = 5
}

public class ResponseGenerationRequest
{
    public int ConversationId { get; set; }
    public int TenantId { get; set; }
    public string CurrentMessage { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Intent { get; set; } = string.Empty;
    public ConfidenceLevel IntentConfidence { get; set; }
    public List<RelevantContext> RelevantContexts { get; set; } = new();
    public ConversationState ConversationState { get; set; } = new();
    public TimeContext TimeContext { get; set; } = new();
    public BusinessRuleResult BusinessRules { get; set; } = new();
}

public class ResponseGenerationResult
{
    public string GeneratedResponse { get; set; } = string.Empty;
    public ResponseTone Tone { get; set; }
    public ResponseComplexity Complexity { get; set; }
    public double ConfidenceScore { get; set; }
    public List<string> ContextFactorsUsed { get; set; } = new();
    public List<ResponseSuggestion> FollowUpSuggestions { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
    public bool RequiresHumanHandoff { get; set; }
    public string ReasoningTrace { get; set; } = string.Empty;
}

public class ResponseContext
{
    public int ConversationId { get; set; }
    public int TenantId { get; set; }
    public string Intent { get; set; } = string.Empty;
    public string CurrentMessage { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public TimeContext TimeContext { get; set; } = new();
    public ConversationState ConversationState { get; set; } = new();
    public List<RelevantContext> HistoricalContext { get; set; } = new();
    public BusinessRuleResult BusinessRules { get; set; } = new();
    public bool IsEmergency { get; set; }
    public bool IsFollowUp { get; set; }
}

public class ResponsePersonality
{
    public ResponseTone PreferredTone { get; set; }
    public ResponseComplexity DefaultComplexity { get; set; }
    public Dictionary<string, string> PersonalityTraits { get; set; } = new();
    public List<string> PreferredPhrases { get; set; } = new();
    public List<string> AvoidedPhrases { get; set; } = new();
    public string BrandVoice { get; set; } = string.Empty;
}

public class ResponseSuggestion
{
    public string Text { get; set; } = string.Empty;
    public string Intent { get; set; } = string.Empty;
    public double RelevanceScore { get; set; }
    public string Category { get; set; } = string.Empty;
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public class EnhancedResponseGeneratorService : IEnhancedResponseGeneratorService
{
    private readonly HostrDbContext _context;
    private readonly ILogger<EnhancedResponseGeneratorService> _logger;
    private readonly ITemporalContextService _temporalContextService;
    private readonly IConversationStateService _conversationStateService;
    private readonly IContextRelevanceService _contextRelevanceService;
    private readonly IBusinessRulesEngine _businessRulesEngine;
    private readonly IOpenAIService _openAIService;

    // Response enhancement patterns
    private static readonly Dictionary<ResponseTone, Dictionary<string, string>> ToneEnhancements = new()
    {
        {
            ResponseTone.Professional, new Dictionary<string, string>
            {
                { "greeting", "Good {timeOfDay}! I'm here to assist you with your inquiry." },
                { "confirmation", "I can confirm that your request has been processed." },
                { "apology", "I apologize for any inconvenience this may have caused." },
                { "closure", "Please don't hesitate to reach out if you need further assistance." }
            }
        },
        {
            ResponseTone.Friendly, new Dictionary<string, string>
            {
                { "greeting", "Hi there! Hope you're having a great {timeOfDay}!" },
                { "confirmation", "Great news! I've got that sorted for you." },
                { "apology", "So sorry about that! Let me make it right." },
                { "closure", "Feel free to message me anytime if you need anything else!" }
            }
        },
        {
            ResponseTone.Empathetic, new Dictionary<string, string>
            {
                { "greeting", "Hello! I understand you need some help, and I'm here for you." },
                { "confirmation", "I completely understand your concern, and I've taken care of this for you." },
                { "apology", "I truly understand how frustrating this must be. Let me help resolve this." },
                { "closure", "I hope this helps! I'm always here if you need support." }
            }
        }
    };

    private static readonly Dictionary<string, List<string>> ContextualPhrases = new()
    {
        {
            "morning", new List<string> { "Good morning", "Hope you're starting your day well", "This morning" }
        },
        {
            "afternoon", new List<string> { "Good afternoon", "Hope your day is going well", "This afternoon" }
        },
        {
            "evening", new List<string> { "Good evening", "Hope you've had a good day", "This evening" }
        },
        {
            "late", new List<string> { "Thanks for reaching out", "I'm here to help even at this hour", "Despite the late hour" }
        }
    };

    public EnhancedResponseGeneratorService(
        HostrDbContext context,
        ILogger<EnhancedResponseGeneratorService> logger,
        ITemporalContextService temporalContextService,
        IConversationStateService conversationStateService,
        IContextRelevanceService contextRelevanceService,
        IBusinessRulesEngine businessRulesEngine,
        IOpenAIService openAIService)
    {
        _context = context;
        _logger = logger;
        _temporalContextService = temporalContextService;
        _conversationStateService = conversationStateService;
        _contextRelevanceService = contextRelevanceService;
        _businessRulesEngine = businessRulesEngine;
        _openAIService = openAIService;
    }

    public async Task<ResponseGenerationResult> GenerateContextAwareResponseAsync(ResponseGenerationRequest request)
    {
        try
        {
            var result = new ResponseGenerationResult();
            var contextFactors = new List<string>();

            // Step 1: Determine response personality based on tenant and message type
            var personality = await GetResponsePersonalityAsync(request.TenantId, request.Intent);
            result.Tone = personality.PreferredTone;
            result.Complexity = personality.DefaultComplexity;

            // Step 2: Build enhanced context for response generation
            var responseContext = new ResponseContext
            {
                ConversationId = request.ConversationId,
                TenantId = request.TenantId,
                Intent = request.Intent,
                CurrentMessage = request.CurrentMessage,
                PhoneNumber = request.PhoneNumber,
                TimeContext = request.TimeContext,
                ConversationState = request.ConversationState,
                HistoricalContext = request.RelevantContexts,
                BusinessRules = request.BusinessRules,
                IsEmergency = request.Intent.Contains("emergency") || request.BusinessRules.RequiresEscalation,
                IsFollowUp = request.ConversationState.PendingClarifications.Any()
            };

            // Step 3: Generate base response using OpenAI with enhanced context
            var baseResponse = await GenerateBaseResponseAsync(request, responseContext);

            // Step 4: Enhance response with temporal awareness
            var temporallyEnhancedResponse = await ApplyTemporalAwarenessAsync(baseResponse, request.TimeContext);
            contextFactors.Add("temporal_awareness");

            // Step 5: Apply personality and tone enhancements
            var personalityEnhancedResponse = await ApplyPersonalityEnhancementsAsync(
                temporallyEnhancedResponse, personality, responseContext);
            contextFactors.Add("personality_enhancement");

            // Step 6: Add contextual information from conversation history
            var contextEnhancedResponse = await EnhanceResponseWithContextAsync(
                personalityEnhancedResponse, responseContext);
            contextFactors.Add("historical_context");

            // Step 7: Apply business rules and constraints
            var finalResponse = await ApplyBusinessRuleConstraintsAsync(
                contextEnhancedResponse, request.BusinessRules, responseContext);
            contextFactors.Add("business_rules");

            // Step 8: Generate follow-up suggestions
            var followUpSuggestions = await GenerateFollowUpSuggestionsAsync(responseContext);

            // Step 9: Calculate confidence score
            var confidenceScore = CalculateResponseConfidence(request, contextFactors);

            // Step 10: Build final result
            result.GeneratedResponse = finalResponse;
            result.ContextFactorsUsed = contextFactors;
            result.FollowUpSuggestions = followUpSuggestions;
            result.ConfidenceScore = confidenceScore;
            result.RequiresHumanHandoff = ShouldRequireHumanHandoff(request, confidenceScore);
            result.ReasoningTrace = BuildReasoningTrace(request, contextFactors);
            result.Metadata = BuildResponseMetadata(request, responseContext);

            _logger.LogInformation("Generated context-aware response for conversation {ConversationId} with confidence {Confidence}",
                request.ConversationId, confidenceScore);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating context-aware response for conversation {ConversationId}",
                request.ConversationId);

            // Return fallback response
            return new ResponseGenerationResult
            {
                GeneratedResponse = "I apologize, but I'm experiencing some technical difficulties. A team member will assist you shortly.",
                Tone = ResponseTone.Professional,
                Complexity = ResponseComplexity.Simple,
                ConfidenceScore = 0.1,
                RequiresHumanHandoff = true,
                ReasoningTrace = "Fallback due to error in response generation"
            };
        }
    }

    public async Task<string> EnhanceResponseWithContextAsync(string baseResponse, ResponseContext context)
    {
        try
        {
            var enhancedResponse = baseResponse;

            // Add contextual information from recent interactions
            if (context.HistoricalContext.Any(c => c.Type == ContextType.ServiceHistory && c.IsCritical))
            {
                var activeService = context.HistoricalContext.First(c => c.Type == ContextType.ServiceHistory && c.IsCritical);
                enhancedResponse = AddServiceContextReference(enhancedResponse, activeService);
            }

            // Add booking context if relevant
            if (context.HistoricalContext.Any(c => c.Type == ContextType.BookingInformation && c.IsCritical))
            {
                var activeBooking = context.HistoricalContext.First(c => c.Type == ContextType.BookingInformation && c.IsCritical);
                enhancedResponse = AddBookingContextReference(enhancedResponse, activeBooking);
            }

            // Add follow-up context for ongoing conversations
            if (context.IsFollowUp && context.ConversationState.PendingClarifications.Any())
            {
                enhancedResponse = AddFollowUpContext(enhancedResponse, context.ConversationState);
            }

            return enhancedResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enhancing response with context");
            return baseResponse; // Return unenhanced response on error
        }
    }

    public async Task<ResponsePersonality> GetResponsePersonalityAsync(int tenantId, string messageType)
    {
        try
        {
            // Get tenant-specific personality settings (could be stored in database)
            var tenant = await _context.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId);
            return GetResponsePersonality(messageType, tenant?.Name ?? "");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting response personality for tenant {TenantId}", tenantId);
            return GetResponsePersonality(messageType, "Professional Hotel Assistant");
        }
    }

    public ResponsePersonality GetResponsePersonality(string messageType, string tenantName)
    {
        try
        {
            return new ResponsePersonality
            {
                PreferredTone = DeterminePreferredTone(messageType, tenantName),
                DefaultComplexity = DetermineDefaultComplexity(messageType),
                BrandVoice = tenantName,
                PersonalityTraits = GetPersonalityTraits(messageType),
                PreferredPhrases = GetPreferredPhrases(messageType),
                AvoidedPhrases = GetAvoidedPhrases()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting response personality for messageType {MessageType}", messageType);

            // Return default personality
            return new ResponsePersonality
            {
                PreferredTone = ResponseTone.Professional,
                DefaultComplexity = ResponseComplexity.Detailed,
                BrandVoice = "Professional Hotel Assistant"
            };
        }
    }

    public async Task<string> ApplyTemporalAwarenessAsync(string response, TimeContext timeContext)
    {
        try
        {
            var enhancedResponse = response;

            // Replace time placeholders
            enhancedResponse = enhancedResponse.Replace("{timeOfDay}", GetTimeOfDayGreeting(timeContext.LocalTime));
            enhancedResponse = enhancedResponse.Replace("{currentTime}", timeContext.LocalTime.ToString("HH:mm"));
            enhancedResponse = enhancedResponse.Replace("{currentDate}", timeContext.LocalTime.ToString("MMMM dd"));

            // Add time-sensitive context
            if (timeContext.IsBusinessHours)
            {
                enhancedResponse = AddBusinessHoursContext(enhancedResponse, timeContext);
            }
            else
            {
                enhancedResponse = AddAfterHoursContext(enhancedResponse, timeContext);
            }

            // Add meal time context if relevant
            if (timeContext.MealPeriod != MealPeriod.None)
            {
                enhancedResponse = AddMealTimeContext(enhancedResponse, timeContext);
            }

            return enhancedResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying temporal awareness to response");
            return response;
        }
    }

    public async Task<List<ResponseSuggestion>> GenerateFollowUpSuggestionsAsync(ResponseContext context)
    {
        try
        {
            var suggestions = new List<ResponseSuggestion>();

            // Intent-based suggestions
            suggestions.AddRange(GetIntentBasedSuggestions(context.Intent));

            // Time-based suggestions
            suggestions.AddRange(GetTimeBasedSuggestions(context.TimeContext));

            // Context-based suggestions
            if (context.HistoricalContext.Any())
            {
                suggestions.AddRange(GetContextBasedSuggestions(context.HistoricalContext));
            }

            // Business rule-based suggestions
            if (context.BusinessRules.AvailableServices.Any())
            {
                suggestions.AddRange(GetServiceBasedSuggestions(context.BusinessRules.AvailableServices));
            }

            // Score and rank suggestions
            foreach (var suggestion in suggestions)
            {
                suggestion.RelevanceScore = CalculateSuggestionRelevance(suggestion, context);
            }

            return suggestions
                .OrderByDescending(s => s.RelevanceScore)
                .Take(5)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating follow-up suggestions");
            return new List<ResponseSuggestion>();
        }
    }

    private async Task<string> GenerateBaseResponseAsync(ResponseGenerationRequest request, ResponseContext context)
    {
        try
        {
            // Build comprehensive context for OpenAI
            var contextualPrompt = BuildContextualPrompt(request, context);

            // Generate response using OpenAI service with enhanced context
            var response = await _openAIService.GenerateResponseAsync(
                contextualPrompt,
                "", // context
                "", // itemsContext
                request.CurrentMessage,
                request.PhoneNumber);

            return response?.Reply ?? "I'm here to help! Could you please provide more details about what you need?";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating base response");
            return "I'm here to assist you. How can I help today?";
        }
    }

    private string BuildContextualPrompt(ResponseGenerationRequest request, ResponseContext context)
    {
        var promptBuilder = new List<string>
        {
            $"You are a helpful hotel assistant for {context.TenantId}.",
            $"Current time: {context.TimeContext.LocalTime:yyyy-MM-dd HH:mm} ({context.TimeContext.Timezone})",
            $"Business hours: {(context.TimeContext.IsBusinessHours ? "Open" : "Closed")}",
            $"Current meal period: {context.TimeContext.MealPeriod}"
        };

        // Add conversation context
        if (context.ConversationState.Variables.Any())
        {
            promptBuilder.Add("Conversation context:");
            foreach (var variable in context.ConversationState.Variables.Take(3))
            {
                promptBuilder.Add($"- {variable.Key}: {variable.Value}");
            }
        }

        // Add relevant historical context
        if (context.HistoricalContext.Any())
        {
            promptBuilder.Add("Recent relevant context:");
            foreach (var ctx in context.HistoricalContext.Take(3))
            {
                promptBuilder.Add($"- {ctx.Type}: {ctx.Content}");
            }
        }

        // Add business rules
        if (context.BusinessRules.AvailableServices.Any())
        {
            promptBuilder.Add($"Available services: {string.Join(", ", context.BusinessRules.AvailableServices.Take(5))}");
        }

        promptBuilder.Add("Respond naturally and helpfully. Be concise but thorough.");

        return string.Join("\n", promptBuilder);
    }

    private async Task<string> ApplyPersonalityEnhancementsAsync(string response, ResponsePersonality personality, ResponseContext context)
    {
        var enhancedResponse = response;

        // Apply tone-specific enhancements
        if (ToneEnhancements.TryGetValue(personality.PreferredTone, out var tonePatterns))
        {
            enhancedResponse = ApplyTonePatterns(enhancedResponse, tonePatterns, context);
        }

        // Apply preferred phrases
        enhancedResponse = ApplyPreferredPhrases(enhancedResponse, personality.PreferredPhrases);

        // Remove avoided phrases
        enhancedResponse = RemoveAvoidedPhrases(enhancedResponse, personality.AvoidedPhrases);

        return enhancedResponse;
    }

    private async Task<string> ApplyBusinessRuleConstraintsAsync(string response, BusinessRuleResult businessRules, ResponseContext context)
    {
        var constrainedResponse = response;

        // Apply availability constraints
        if (!businessRules.IsAvailable)
        {
            constrainedResponse = AddAvailabilityConstraint(constrainedResponse, businessRules);
        }

        // Apply escalation requirements
        if (businessRules.RequiresEscalation)
        {
            constrainedResponse = AddEscalationContext(constrainedResponse, businessRules);
        }

        // Apply capacity constraints
        if (businessRules.HasCapacityConstraints)
        {
            constrainedResponse = AddCapacityConstraint(constrainedResponse, businessRules);
        }

        return constrainedResponse;
    }

    private double CalculateResponseConfidence(ResponseGenerationRequest request, List<string> contextFactors)
    {
        double confidence = 0.5; // Base confidence

        // Intent confidence contribution
        confidence += (double)request.IntentConfidence * 0.2;

        // Context factors contribution
        confidence += contextFactors.Count * 0.1;

        // Historical context contribution
        if (request.RelevantContexts.Any())
        {
            var avgRelevance = request.RelevantContexts.Average(c => c.RelevanceScore);
            confidence += avgRelevance * 0.2;
        }

        // Business rules confidence
        if (request.BusinessRules.IsAvailable)
        {
            confidence += 0.1;
        }

        return Math.Min(confidence, 1.0);
    }

    private bool ShouldRequireHumanHandoff(ResponseGenerationRequest request, double confidence)
    {
        return confidence < 0.3 ||
               request.BusinessRules.RequiresEscalation ||
               request.Intent.Contains("emergency") ||
               request.Intent.Contains("complaint");
    }

    private string BuildReasoningTrace(ResponseGenerationRequest request, List<string> contextFactors)
    {
        return $"Intent: {request.Intent} (confidence: {request.IntentConfidence}) | " +
               $"Context factors: {string.Join(", ", contextFactors)} | " +
               $"Business rules: Available={request.BusinessRules.IsAvailable}, Escalation={request.BusinessRules.RequiresEscalation}";
    }

    private Dictionary<string, object> BuildResponseMetadata(ResponseGenerationRequest request, ResponseContext context)
    {
        return new Dictionary<string, object>
        {
            { "intent", request.Intent },
            { "intentConfidence", request.IntentConfidence },
            { "timeContext", context.TimeContext.LocalTime.ToString("yyyy-MM-dd HH:mm") },
            { "isBusinessHours", context.TimeContext.IsBusinessHours },
            { "conversationLength", context.ConversationState.Variables.Count },
            { "hasHistoricalContext", context.HistoricalContext.Any() },
            { "isEmergency", context.IsEmergency },
            { "isFollowUp", context.IsFollowUp }
        };
    }

    // Helper methods for response enhancement
    private ResponseTone DeterminePreferredTone(string messageType, string tenantName)
    {
        return messageType.ToLower() switch
        {
            "emergency" => ResponseTone.Urgent,
            "complaint" => ResponseTone.Empathetic,
            "booking" => ResponseTone.Professional,
            "service_request" => ResponseTone.Friendly,
            _ => ResponseTone.Professional
        };
    }

    private ResponseComplexity DetermineDefaultComplexity(string messageType)
    {
        return messageType.ToLower() switch
        {
            "simple_query" => ResponseComplexity.Simple,
            "booking" => ResponseComplexity.Detailed,
            "technical" => ResponseComplexity.Technical,
            _ => ResponseComplexity.Conversational
        };
    }

    private Dictionary<string, string> GetPersonalityTraits(string messageType)
    {
        return new Dictionary<string, string>
        {
            { "helpfulness", "high" },
            { "professionalism", "high" },
            { "empathy", messageType.Contains("complaint") ? "high" : "medium" },
            { "efficiency", "high" }
        };
    }

    private List<string> GetPreferredPhrases(string messageType)
    {
        return messageType.ToLower() switch
        {
            "emergency" => new List<string> { "immediately", "right away", "urgent priority" },
            "complaint" => new List<string> { "I understand", "I apologize", "let me make this right" },
            _ => new List<string> { "happy to help", "I'd be glad to", "certainly" }
        };
    }

    private List<string> GetAvoidedPhrases()
    {
        return new List<string> { "no", "can't", "impossible", "never" };
    }

    private string GetTimeOfDayGreeting(DateTime localTime)
    {
        return localTime.Hour switch
        {
            < 12 => "morning",
            < 17 => "afternoon",
            < 21 => "evening",
            _ => "evening"
        };
    }

    private string AddBusinessHoursContext(string response, TimeContext timeContext)
    {
        if (!response.Contains("hour") && !response.Contains("time"))
        {
            return response + " Our full services are available during business hours.";
        }
        return response;
    }

    private string AddAfterHoursContext(string response, TimeContext timeContext)
    {
        if (!response.Contains("hour") && !response.Contains("time"))
        {
            return response + " Please note that some services may have limited availability outside business hours.";
        }
        return response;
    }

    private string AddMealTimeContext(string response, TimeContext timeContext)
    {
        if (timeContext.MealPeriod != MealPeriod.None && response.Contains("menu"))
        {
            return response + $" Our {timeContext.MealPeriod.ToString().ToLower()} menu is currently available.";
        }
        return response;
    }

    private string AddServiceContextReference(string response, RelevantContext serviceContext)
    {
        if (serviceContext.Metadata.TryGetValue("status", out var status) && status.ToString() == "Pending")
        {
            return response + " I can see you have a pending service request that we're working on.";
        }
        return response;
    }

    private string AddBookingContextReference(string response, RelevantContext bookingContext)
    {
        if (bookingContext.Metadata.TryGetValue("isActive", out var isActive) && (bool)isActive)
        {
            return response + " I can see you're currently our guest.";
        }
        return response;
    }

    private string AddFollowUpContext(string response, ConversationState conversationState)
    {
        if (conversationState.PendingClarifications.Any())
        {
            var clarification = conversationState.PendingClarifications.First();
            return $"Thank you for clarifying. {response}";
        }
        return response;
    }

    private string ApplyTonePatterns(string response, Dictionary<string, string> tonePatterns, ResponseContext context)
    {
        // Apply greeting pattern if response starts conversation
        if (context.ConversationState.Variables.Count <= 1 && tonePatterns.TryGetValue("greeting", out var greeting))
        {
            var timeOfDay = GetTimeOfDayGreeting(context.TimeContext.LocalTime);
            greeting = greeting.Replace("{timeOfDay}", timeOfDay);
            response = greeting + " " + response;
        }

        return response;
    }

    private string ApplyPreferredPhrases(string response, List<string> preferredPhrases)
    {
        // This is a simplified implementation - in practice, you'd use more sophisticated NLP
        foreach (var phrase in preferredPhrases.Take(2))
        {
            if (!response.Contains(phrase, StringComparison.OrdinalIgnoreCase))
            {
                // Add phrase contextually (simplified logic)
                if (phrase.Contains("help") && !response.Contains("help"))
                {
                    response = response.Replace("I can", "I'm happy to help and can");
                }
            }
        }
        return response;
    }

    private string RemoveAvoidedPhrases(string response, List<string> avoidedPhrases)
    {
        foreach (var phrase in avoidedPhrases)
        {
            response = response.Replace($" {phrase} ", $" unfortunately {phrase} ", StringComparison.OrdinalIgnoreCase);
        }
        return response;
    }

    private string AddAvailabilityConstraint(string response, BusinessRuleResult businessRules)
    {
        return response + " Please note that this service may not be available at the moment.";
    }

    private string AddEscalationContext(string response, BusinessRuleResult businessRules)
    {
        return response + " I'll ensure a team member follows up with you personally.";
    }

    private string AddCapacityConstraint(string response, BusinessRuleResult businessRules)
    {
        return response + " Due to current capacity, there may be a brief wait time.";
    }

    private List<ResponseSuggestion> GetIntentBasedSuggestions(string intent)
    {
        return intent.ToLower() switch
        {
            "menu_inquiry" => new List<ResponseSuggestion>
            {
                new() { Text = "Would you like to see today's specials?", Intent = "specials", Category = "menu" },
                new() { Text = "Are you interested in dietary options?", Intent = "dietary", Category = "menu" }
            },
            "booking_inquiry" => new List<ResponseSuggestion>
            {
                new() { Text = "Would you like to modify your booking?", Intent = "booking_modify", Category = "booking" },
                new() { Text = "Do you need room service information?", Intent = "room_service", Category = "service" }
            },
            _ => new List<ResponseSuggestion>()
        };
    }

    private List<ResponseSuggestion> GetTimeBasedSuggestions(TimeContext timeContext)
    {
        var suggestions = new List<ResponseSuggestion>();

        if (timeContext.MealPeriod != MealPeriod.None)
        {
            suggestions.Add(new ResponseSuggestion
            {
                Text = $"Would you like to see our {timeContext.MealPeriod.ToString().ToLower()} menu?",
                Intent = "menu_inquiry",
                Category = "time_based"
            });
        }

        return suggestions;
    }

    private List<ResponseSuggestion> GetContextBasedSuggestions(List<RelevantContext> contexts)
    {
        var suggestions = new List<ResponseSuggestion>();

        foreach (var context in contexts.Where(c => c.IsCritical).Take(2))
        {
            switch (context.Type)
            {
                case ContextType.ServiceHistory:
                    suggestions.Add(new ResponseSuggestion
                    {
                        Text = "Would you like an update on your service request?",
                        Intent = "service_status",
                        Category = "context_based"
                    });
                    break;
                case ContextType.BookingInformation:
                    suggestions.Add(new ResponseSuggestion
                    {
                        Text = "Do you need assistance with your stay?",
                        Intent = "guest_assistance",
                        Category = "context_based"
                    });
                    break;
            }
        }

        return suggestions;
    }

    private List<ResponseSuggestion> GetServiceBasedSuggestions(List<string> availableServices)
    {
        return availableServices.Take(3).Select(service => new ResponseSuggestion
        {
            Text = $"Would you like information about our {service.ToLower()} service?",
            Intent = "service_inquiry",
            Category = "service_based",
            Metadata = new Dictionary<string, object> { { "service", service } }
        }).ToList();
    }

    private double CalculateSuggestionRelevance(ResponseSuggestion suggestion, ResponseContext context)
    {
        double relevance = 0.5; // Base relevance

        // Category-based relevance
        if (suggestion.Category == "time_based" && context.TimeContext.IsBusinessHours)
            relevance += 0.2;

        if (suggestion.Category == "context_based" && context.HistoricalContext.Any())
            relevance += 0.3;

        // Intent matching
        if (suggestion.Intent.Contains(context.Intent, StringComparison.OrdinalIgnoreCase))
            relevance += 0.3;

        return Math.Min(relevance, 1.0);
    }
}