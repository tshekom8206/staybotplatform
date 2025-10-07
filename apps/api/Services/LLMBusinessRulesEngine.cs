using Microsoft.EntityFrameworkCore;
using Hostr.Api.Data;
using Hostr.Api.Models;
using System.Text.Json;
using System.Text;

namespace Hostr.Api.Services;

public class LLMBusinessRulesEngine : ILLMBusinessRulesEngine
{
    private readonly HostrDbContext _context;
    private readonly IOpenAIService _openAIService;
    private readonly ITenantCacheService _tenantCache;
    private readonly ILogger<LLMBusinessRulesEngine> _logger;

    // Define semantic business rules
    private static readonly List<SemanticRule> DefaultSemanticRules = new()
    {
        new SemanticRule
        {
            RuleName = "spa_services_availability",
            Description = "Spa and wellness services require checked-in guest status and are time-sensitive",
            ServiceTypes = new() { "spa_services", "massage", "wellness", "beauty_treatments", "fitness" },
            RequiredConditions = new() { "checked_in_guest", "business_hours" },
            ExcludedItems = new() { "food_items", "beverages", "room_amenities", "maintenance_requests" },
            RequiredGuestType = new() { GuestType.Active },
            MinimumConfidence = 0.8,
            Severity = "BLOCK"
        },
        new SemanticRule
        {
            RuleName = "room_service_hours",
            Description = "Room service items have specific availability hours",
            ServiceTypes = new() { "food_service", "room_service", "beverages" },
            RequiredConditions = new() { "service_hours" },
            ExcludedItems = new() { "spa_services", "maintenance_requests" },
            MinimumConfidence = 0.7,
            Severity = "WARNING"
        },
        new SemanticRule
        {
            RuleName = "maintenance_priority",
            Description = "Maintenance requests require immediate attention for checked-in guests",
            ServiceTypes = new() { "maintenance", "repairs", "technical_issues" },
            RequiredConditions = new() { "checked_in_guest" },
            RequiredGuestType = new() { GuestType.Active },
            MinimumConfidence = 0.6,
            Severity = "ESCALATE"
        }
    };

    public LLMBusinessRulesEngine(
        HostrDbContext context,
        IOpenAIService openAIService,
        ITenantCacheService tenantCache,
        ILogger<LLMBusinessRulesEngine> logger)
    {
        _context = context;
        _openAIService = openAIService;
        _tenantCache = tenantCache;
        _logger = logger;
    }

    public async Task<BusinessRuleAnalysis> AnalyzeMessageAsync(
        string message,
        TenantContext tenantContext,
        GuestStatus guestStatus,
        ConversationContext conversationContext)
    {
        try
        {
            _logger.LogInformation("Starting LLM business rules analysis for message: '{Message}'", message);

            // 1. Build comprehensive context for LLM analysis
            var analysisContext = await BuildAnalysisContextAsync(tenantContext);

            // 2. Create structured LLM request
            var llmRequest = new LLMBusinessRuleRequest
            {
                Message = message,
                TenantContext = tenantContext,
                GuestStatus = guestStatus,
                ConversationContext = conversationContext,
                AvailableServices = analysisContext.AvailableServices,
                AvailableItems = analysisContext.AvailableItems,
                CurrentTime = DateTime.UtcNow
            };

            // 3. Generate analysis prompt
            var analysisPrompt = BuildBusinessRuleAnalysisPrompt(llmRequest, analysisContext);

            // 4. Get structured LLM response
            var llmResponse = await _openAIService.GetStructuredResponseAsync<LLMBusinessRuleResponse>(
                analysisPrompt,
                temperature: 0.2 // Lower temperature for more consistent business logic
            );

            if (llmResponse == null)
            {
                _logger.LogWarning("LLM response was null, using fallback analysis");
                return CreateFallbackAnalysis(message);
            }

            // 5. Convert to business rule analysis
            var analysis = new BusinessRuleAnalysis
            {
                PrimaryIntent = llmResponse.PrimaryIntent,
                ServiceCategory = llmResponse.ServiceCategory,
                SpecificItem = llmResponse.SpecificItem,
                OverallConfidence = llmResponse.OverallConfidence,
                CategoryConfidences = llmResponse.CategoryConfidences,
                ContextFactors = llmResponse.ContextFactors,
                DetectedKeywords = llmResponse.DetectedKeywords,
                RawLLMResponse = JsonSerializer.Serialize(llmResponse),
                AnalyzedAt = DateTime.UtcNow
            };

            _logger.LogInformation(
                "LLM business rules analysis completed. Intent: {Intent}, Category: {Category}, Confidence: {Confidence}",
                analysis.PrimaryIntent, analysis.ServiceCategory, analysis.OverallConfidence);

            return analysis;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in LLM business rules analysis for message: '{Message}'", message);
            return CreateFallbackAnalysis(message);
        }
    }

    public async Task<List<BusinessRuleViolation>> ValidateBusinessRulesAsync(
        BusinessRuleAnalysis analysis,
        TenantContext tenantContext)
    {
        var violations = new List<BusinessRuleViolation>();

        try
        {
            _logger.LogInformation("Validating business rules for analysis: Intent={Intent}, Category={Category}",
                analysis.PrimaryIntent, analysis.ServiceCategory);

            // Get applicable semantic rules based on LLM analysis
            var applicableRules = GetApplicableSemanticRules(analysis);

            foreach (var rule in applicableRules)
            {
                // Only apply rule if LLM confidence meets threshold
                if (analysis.OverallConfidence < rule.MinimumConfidence)
                {
                    _logger.LogDebug("Skipping rule {RuleName} due to low confidence: {Confidence} < {Required}",
                        rule.RuleName, analysis.OverallConfidence, rule.MinimumConfidence);
                    continue;
                }

                // Check if service category matches rule
                if (!IsServiceCategoryMatch(analysis.ServiceCategory, rule.ServiceTypes))
                {
                    _logger.LogDebug("Rule {RuleName} not applicable to service category: {Category}",
                        rule.RuleName, analysis.ServiceCategory);
                    continue;
                }

                // Check exclusions - CRITICAL for preventing false positives
                if (IsExcludedByRule(analysis, rule))
                {
                    _logger.LogInformation("Request excluded from rule {RuleName} due to exclusion criteria. " +
                        "Item: {Item}, Category: {Category}",
                        rule.RuleName, analysis.SpecificItem, analysis.ServiceCategory);
                    continue;
                }

                // Evaluate rule conditions
                var violation = await EvaluateSemanticRuleAsync(rule, analysis, tenantContext);
                if (violation != null)
                {
                    violations.Add(violation);
                    _logger.LogInformation("Business rule violation detected: {RuleName} - {Message}",
                        violation.RuleName, violation.Message);
                }
            }

            _logger.LogInformation("Business rules validation completed. Found {Count} violations", violations.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating business rules");
        }

        return violations;
    }

    private async Task<BusinessRuleAnalysisContext> BuildAnalysisContextAsync(TenantContext tenantContext)
    {
        // Get available services and items for context
        var services = await _context.Services
            .Where(s => s.TenantId == tenantContext.TenantId && s.IsAvailable)
            .Select(s => s.Name)
            .ToListAsync();

        var menuItems = await _context.MenuItems
            .Where(m => m.TenantId == tenantContext.TenantId && m.IsAvailable)
            .Select(m => m.Name)
            .ToListAsync();

        var requestItems = await _context.RequestItems
            .Where(r => r.TenantId == tenantContext.TenantId && r.IsAvailable)
            .Select(r => r.Name)
            .ToListAsync();

        return new BusinessRuleAnalysisContext
        {
            AvailableServices = services,
            AvailableItems = menuItems.Concat(requestItems).ToList()
        };
    }

    private string BuildBusinessRuleAnalysisPrompt(LLMBusinessRuleRequest request, BusinessRuleAnalysisContext context)
    {
        var prompt = $@"You are an expert business rules analyzer for a hospitality property.
Analyze the guest message to understand intent, categorize the request, and identify relevant business rules.

GUEST MESSAGE: ""{request.Message}""

RECENT CONVERSATION HISTORY:
{(request.ConversationContext.RecentMessages.Count > 0 ? string.Join("\n", request.ConversationContext.RecentMessages.TakeLast(6)) : "No recent conversation")}

CONTEXT:
- Guest Status: {request.GuestStatus}
- Tenant: {request.TenantContext.TenantName}
- Current Time: {request.CurrentTime:yyyy-MM-dd HH:mm}
- Available Services: {string.Join(", ", request.AvailableServices)}
- Available Items: {string.Join(", ", request.AvailableItems)}

CRITICAL ANALYSIS REQUIREMENTS:
1. CONVERSATION CONTEXT: Analyze the guest's current message in the context of the recent conversation history above
2. CONTEXTUAL MATCHING: If the conversation shows the bot recently offered specific options, match the guest's response to those options
3. SEMANTIC UNDERSTANDING: ""spa"" in ""sparkling water"" is NOT ""spa services"" - understand context not just keywords
4. INTENT CLASSIFICATION: What does the guest actually want based on both the message AND conversation flow?
5. CONFIDENCE SCORING: How certain are you about the classification considering the full conversation context?

SERVICE CATEGORIES:
- FOOD_BEVERAGE: Food items, drinks, beverages (including sparkling water, still water, juices)
- SPA_WELLNESS: Actual spa treatments, massages, wellness services
- MAINTENANCE: Repairs, technical issues, room problems
- HOUSEKEEPING: Cleaning, amenities, towels, toiletries
- ACTIVITIES_EXPERIENCES: Tours, safaris, excursions, activities, bookable experiences, transfers
- CONCIERGE: Information, bookings, general assistance

RESPOND WITH VALID JSON:
{{
    ""primaryIntent"": ""REQUEST_ITEM | REQUEST_SERVICE | INQUIRY | COMPLAINT | BOOKING"",
    ""serviceCategory"": ""FOOD_BEVERAGE | SPA_WELLNESS | MAINTENANCE | HOUSEKEEPING | ACTIVITIES_EXPERIENCES | CONCIERGE"",
    ""specificItem"": ""exact item or service name"",
    ""overallConfidence"": 0.0-1.0,
    ""categoryConfidences"": {{
        ""FOOD_BEVERAGE"": 0.0-1.0,
        ""SPA_WELLNESS"": 0.0-1.0,
        ""MAINTENANCE"": 0.0-1.0,
        ""HOUSEKEEPING"": 0.0-1.0,
        ""ACTIVITIES_EXPERIENCES"": 0.0-1.0,
        ""CONCIERGE"": 0.0-1.0
    }},
    ""contextFactors"": {{
        ""timeRelevant"": true/false,
        ""locationRelevant"": true/false,
        ""guestStatusRelevant"": true/false,
        ""conversationContextRelevant"": true/false,
        ""relevantServices"": [""applicable service types""],
        ""excludedServices"": [""services that should NOT apply""]
    }},
    ""detectedKeywords"": [""actual keywords in context""],
    ""relevantBusinessRules"": [""spa_services_availability"", ""room_service_hours"", ""maintenance_priority""],
    ""reasoningExplanation"": ""Clear explanation of why you classified this way""
}}

EXAMPLES:
- ""I need sparkling water"" → FOOD_BEVERAGE (water is a beverage, NOT spa related)
- ""I want a spa treatment"" → SPA_WELLNESS (actual spa service request)
- ""Book me a massage"" → SPA_WELLNESS (wellness service)
- ""I want to go on a safari"" → ACTIVITIES_EXPERIENCES + BOOKING intent (bookable activity/experience)
- ""Can you arrange a tour?"" → ACTIVITIES_EXPERIENCES + BOOKING intent (bookable experience)
- ""My AC is broken"" → MAINTENANCE (technical repair)

Analyze the message with semantic understanding, not just keyword matching.";

        return prompt;
    }

    private List<SemanticRule> GetApplicableSemanticRules(BusinessRuleAnalysis analysis)
    {
        return DefaultSemanticRules
            .Where(rule => rule.IsActive)
            .Where(rule => IsServiceCategoryMatch(analysis.ServiceCategory, rule.ServiceTypes) ||
                          analysis.ContextFactors.RelevantServices.Any(s => rule.ServiceTypes.Contains(s)))
            .ToList();
    }

    private bool IsServiceCategoryMatch(string serviceCategory, List<string> ruleServiceTypes)
    {
        if (string.IsNullOrEmpty(serviceCategory)) return false;

        var categoryLower = serviceCategory.ToLower();
        return ruleServiceTypes.Any(serviceType =>
        {
            var serviceTypeLower = serviceType.ToLower().Replace("_", " ");
            return categoryLower.Contains(serviceTypeLower) || serviceTypeLower.Contains(categoryLower);
        });
    }

    private bool IsExcludedByRule(BusinessRuleAnalysis analysis, SemanticRule rule)
    {
        if (rule.ExcludedItems.Count == 0) return false;

        var itemLower = analysis.SpecificItem.ToLower();
        var categoryLower = analysis.ServiceCategory.ToLower();

        // Check specific item exclusions
        if (rule.ExcludedItems.Any(excluded =>
        {
            var excludedLower = excluded.ToLower().Replace("_", " ");
            return itemLower.Contains(excludedLower) || excludedLower.Contains(itemLower);
        }))
        {
            return true;
        }

        // Check category exclusions
        if (rule.ExcludedItems.Any(excluded =>
        {
            var excludedLower = excluded.ToLower().Replace("_", " ");
            return categoryLower.Contains(excludedLower);
        }))
        {
            return true;
        }

        // Check LLM-provided exclusions
        return analysis.ContextFactors.ExcludedServices.Any(excluded =>
            rule.ServiceTypes.Any(serviceType =>
                serviceType.ToLower().Contains(excluded.ToLower()) ||
                excluded.ToLower().Contains(serviceType.ToLower())));
    }

    private async Task<BusinessRuleViolation?> EvaluateSemanticRuleAsync(
        SemanticRule rule,
        BusinessRuleAnalysis analysis,
        TenantContext tenantContext)
    {
        // For now, implement basic service availability check
        // This can be expanded with more complex rule logic
        if (rule.RuleName == "spa_services_availability")
        {
            // Check if spa services are actually available for this tenant
            var hasSpServices = await _context.Services
                .AnyAsync(s => s.TenantId == tenantContext.TenantId &&
                              s.IsAvailable &&
                              (s.Name.Contains("Spa") || s.Name.Contains("Massage") || s.Name.Contains("Wellness")));

            if (!hasSpServices)
            {
                return new BusinessRuleViolation
                {
                    RuleName = rule.RuleName,
                    RuleType = "SERVICE_AVAILABILITY",
                    Severity = rule.Severity,
                    Message = "Spa services are not available at this property",
                    Confidence = analysis.OverallConfidence,
                    Context = new Dictionary<string, object>
                    {
                        ["analysisCategory"] = analysis.ServiceCategory,
                        ["specificItem"] = analysis.SpecificItem,
                        ["tenantId"] = tenantContext.TenantId
                    }
                };
            }
        }

        // No violation found
        return null;
    }

    private BusinessRuleAnalysis CreateFallbackAnalysis(string message)
    {
        return new BusinessRuleAnalysis
        {
            PrimaryIntent = "UNKNOWN",
            ServiceCategory = "UNKNOWN",
            SpecificItem = message,
            OverallConfidence = 0.3,
            CategoryConfidences = new Dictionary<string, double>(),
            ContextFactors = new ContextFactors(),
            DetectedKeywords = new List<string>(),
            RawLLMResponse = "Fallback analysis - LLM not available"
        };
    }

    private class BusinessRuleAnalysisContext
    {
        public List<string> AvailableServices { get; set; } = new();
        public List<string> AvailableItems { get; set; } = new();
    }
}