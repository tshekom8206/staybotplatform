using Microsoft.EntityFrameworkCore;
using Hostr.Api.Data;
using Hostr.Api.Models;
using System.Text.Json;

namespace Hostr.Api.Services;

public class UpsellService : IUpsellService
{
    private readonly HostrDbContext _context;
    private readonly ILogger<UpsellService> _logger;
    private const double MIN_CONFIDENCE_THRESHOLD = 0.8;
    private const int MAX_SUGGESTIONS_PER_CONVERSATION = 3;

    public UpsellService(
        HostrDbContext context,
        ILogger<UpsellService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<UpsellRecommendation?> GetRelevantUpsellAsync(
        int tenantId,
        int conversationId,
        UpsellContext context)
    {
        var recommendations = await GetMultipleUpsellsAsync(tenantId, conversationId, context, maxSuggestions: 1);
        return recommendations.FirstOrDefault();
    }

    public async Task<List<UpsellRecommendation>> GetMultipleUpsellsAsync(
        int tenantId,
        int conversationId,
        UpsellContext context,
        int maxSuggestions = 2)
    {
        try
        {
            _logger.LogInformation("Finding upsell opportunities for conversation {ConversationId}, category: {Category}",
                conversationId, context.ServiceCategory);

            // 1. Check if we've already made too many suggestions in this conversation
            var previousSuggestions = await GetPreviousSuggestionsAsync(conversationId);
            if (previousSuggestions.Count >= MAX_SUGGESTIONS_PER_CONVERSATION)
            {
                _logger.LogInformation("Maximum suggestions ({Max}) already made for conversation {ConversationId}",
                    MAX_SUGGESTIONS_PER_CONVERSATION, conversationId);
                return new List<UpsellRecommendation>();
            }

            // 2. Get active upsell items for this tenant
            var upsellItems = await _context.UpsellItems
                .Where(u => u.TenantId == tenantId && u.IsActive)
                .ToListAsync();

            if (!upsellItems.Any())
            {
                _logger.LogInformation("No active upsell items found for tenant {TenantId}", tenantId);
                return new List<UpsellRecommendation>();
            }

            // 3. Get relevant business rules that might have upsell suggestions
            var relevantRules = await GetRelevantBusinessRulesAsync(tenantId, context);

            // 4. Score each upsell item for relevance
            var scoredRecommendations = new List<UpsellRecommendation>();
            foreach (var item in upsellItems)
            {
                // Skip if already suggested
                if (previousSuggestions.Contains(item.Id))
                {
                    _logger.LogDebug("Skipping upsell item {ItemId} - already suggested", item.Id);
                    continue;
                }

                var score = CalculateRelevanceScore(item, context, relevantRules);
                if (score.RelevanceScore >= MIN_CONFIDENCE_THRESHOLD)
                {
                    scoredRecommendations.Add(score);
                }
            }

            // 5. Sort by relevance and return top N
            var topRecommendations = scoredRecommendations
                .OrderByDescending(r => r.RelevanceScore)
                .Take(maxSuggestions)
                .ToList();

            _logger.LogInformation("Found {Count} high-confidence upsell recommendations for conversation {ConversationId}",
                topRecommendations.Count, conversationId);

            return topRecommendations;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting upsell recommendations for conversation {ConversationId}", conversationId);
            return new List<UpsellRecommendation>();
        }
    }

    public async Task TrackSuggestionAsync(int conversationId, int upsellItemId, bool wasAccepted)
    {
        try
        {
            // Store suggestion tracking in conversation metadata
            // This could be stored in a new table or in JSON metadata on the Conversation
            _logger.LogInformation("Tracking upsell suggestion: Conversation={ConversationId}, Item={ItemId}, Accepted={Accepted}",
                conversationId, upsellItemId, wasAccepted);

            // For now, we'll track this in the conversation's state
            // In production, you might want a dedicated UpsellSuggestionTracking table
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error tracking upsell suggestion for conversation {ConversationId}", conversationId);
        }
    }

    private async Task<List<int>> GetPreviousSuggestionsAsync(int conversationId)
    {
        // Get previous upsell suggestions from conversation state
        // For now, we'll check messages for upsell keywords
        // In production, implement proper tracking table

        var conversation = await _context.Conversations
            .Where(c => c.Id == conversationId)
            .FirstOrDefaultAsync();

        if (conversation?.BookingInfoState != null)
        {
            try
            {
                var stateDoc = JsonDocument.Parse(conversation.BookingInfoState);
                if (stateDoc.RootElement.TryGetProperty("suggestedUpsells", out var suggestedUpsells))
                {
                    var ids = new List<int>();
                    foreach (var element in suggestedUpsells.EnumerateArray())
                    {
                        if (element.TryGetInt32(out var id))
                        {
                            ids.Add(id);
                        }
                    }
                    return ids;
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Could not parse conversation state for upsell tracking");
            }
        }

        return new List<int>();
    }

    private async Task<List<object>> GetRelevantBusinessRulesAsync(int tenantId, UpsellContext context)
    {
        var rules = new List<object>();

        // Get service business rules if applicable
        if (!string.IsNullOrEmpty(context.ServiceCategory))
        {
            var serviceRules = await _context.ServiceBusinessRules
                .Where(r => r.TenantId == tenantId && r.IsActive)
                .Where(r => r.UpsellSuggestions != null)
                .ToListAsync();
            rules.AddRange(serviceRules);
        }

        // Get request item rules if applicable
        if (!string.IsNullOrEmpty(context.SpecificItem))
        {
            var itemRules = await _context.RequestItemRules
                .Where(r => r.TenantId == tenantId && r.IsActive)
                .Where(r => r.UpsellSuggestions != null)
                .ToListAsync();
            rules.AddRange(itemRules);
        }

        return rules;
    }

    private UpsellRecommendation CalculateRelevanceScore(
        UpsellItem item,
        UpsellContext context,
        List<object> relevantRules)
    {
        double score = 0.0;
        var reasons = new List<string>();
        var matchedCategories = new List<string>();

        // 1. Category matching (30% weight)
        var categoryScore = CalculateCategoryMatch(item.Categories, context.ServiceCategory);
        if (categoryScore > 0)
        {
            score += categoryScore * 0.3;
            reasons.Add($"Category match: {string.Join(", ", item.Categories)}");
            matchedCategories.AddRange(item.Categories);
        }

        // 2. Business rule explicit upsell (50% weight) - highest priority
        var ruleScore = CalculateRuleMatch(item, relevantRules, context);
        if (ruleScore > 0)
        {
            score += ruleScore * 0.5;
            reasons.Add("Recommended by business rules");
        }

        // 3. Context relevance (20% weight)
        var contextScore = CalculateContextRelevance(item, context);
        if (contextScore > 0)
        {
            score += contextScore * 0.2;
            reasons.Add("Contextually relevant");
        }

        // Generate warm, concierge-style suggestion text
        var suggestionText = GenerateSuggestionText(item, context);

        return new UpsellRecommendation
        {
            UpsellItemId = item.Id,
            Title = item.Title,
            Description = item.Description,
            PriceCents = item.PriceCents,
            Unit = item.Unit,
            RelevanceScore = score,
            RelevanceReason = string.Join(". ", reasons),
            SuggestionText = suggestionText,
            IsHighConfidence = score >= MIN_CONFIDENCE_THRESHOLD,
            MatchedCategories = matchedCategories
        };
    }

    private double CalculateCategoryMatch(string[] itemCategories, string serviceCategory)
    {
        if (itemCategories.Length == 0 || string.IsNullOrEmpty(serviceCategory))
            return 0.0;

        var categoryLower = serviceCategory.ToLower();

        // Exact category match
        if (itemCategories.Any(c => c.Equals(categoryLower, StringComparison.OrdinalIgnoreCase)))
            return 1.0;

        // Partial category match
        if (itemCategories.Any(c => categoryLower.Contains(c.ToLower()) || c.ToLower().Contains(categoryLower)))
            return 0.7;

        // Related categories (dining + food, spa + wellness, etc.)
        var relatedMatches = new Dictionary<string, string[]>
        {
            ["food_beverage"] = new[] { "dining", "room_service", "beverages", "food" },
            ["spa_wellness"] = new[] { "spa", "wellness", "massage", "beauty" },
            ["activities"] = new[] { "experiences", "excursions", "tours", "safari" },
            ["housekeeping"] = new[] { "amenities", "room_items", "toiletries" }
        };

        foreach (var kvp in relatedMatches)
        {
            if (categoryLower.Contains(kvp.Key))
            {
                if (itemCategories.Any(c => kvp.Value.Any(related => c.ToLower().Contains(related))))
                    return 0.6;
            }
        }

        return 0.0;
    }

    private double CalculateRuleMatch(UpsellItem item, List<object> relevantRules, UpsellContext context)
    {
        // Check if this item is explicitly mentioned in business rules
        foreach (var rule in relevantRules)
        {
            string? upsellSuggestionsJson = null;
            double? minConfidence = null;

            if (rule is ServiceBusinessRule serviceRule)
            {
                upsellSuggestionsJson = serviceRule.UpsellSuggestions;
                minConfidence = (double?)serviceRule.MinConfidenceScore;
            }
            else if (rule is RequestItemRule itemRule)
            {
                upsellSuggestionsJson = itemRule.UpsellSuggestions;
                minConfidence = (double?)itemRule.MinConfidenceScore;
            }

            if (!string.IsNullOrEmpty(upsellSuggestionsJson))
            {
                try
                {
                    var suggestions = JsonSerializer.Deserialize<List<int>>(upsellSuggestionsJson);
                    if (suggestions != null && suggestions.Contains(item.Id))
                    {
                        // Use the rule's confidence requirement or default to 0.8
                        return minConfidence ?? 0.8;
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Could not parse upsell suggestions from business rule");
                }
            }
        }

        return 0.0;
    }

    private double CalculateContextRelevance(UpsellItem item, UpsellContext context)
    {
        double score = 0.0;

        // Time-based relevance
        if (context.RequestedTime.HasValue)
        {
            var hour = context.RequestedTime.Value.Hour;

            // Breakfast items (6-11 AM)
            if (hour >= 6 && hour < 11 && item.Categories.Any(c => c.Contains("breakfast")))
                score += 0.3;

            // Dinner/evening items (5 PM+)
            if (hour >= 17 && item.Categories.Any(c => c.Contains("dinner") || c.Contains("evening")))
                score += 0.3;
        }

        // Group size relevance
        if (context.NumberOfPeople.HasValue && context.NumberOfPeople > 1)
        {
            if (item.Categories.Any(c => c.Contains("group") || c.Contains("family")))
                score += 0.3;
        }

        // Booking stage relevance
        if (context.CurrentStage == "confirmation" && item.Categories.Any(c => c.Contains("premium") || c.Contains("upgrade")))
        {
            score += 0.4; // Higher relevance at confirmation stage
        }

        return Math.Min(score, 1.0); // Cap at 1.0
    }

    private string GenerateSuggestionText(UpsellItem item, UpsellContext context)
    {
        // Generate warm, concierge-style suggestion
        var price = item.PriceCents > 0 ? $" (R{item.PriceCents / 100:F2} {item.Unit})" : "";

        var templates = new List<string>
        {
            $"By the way, many of our guests also enjoy our {item.Title}{price}. {item.Description}",
            $"I'd also like to mention our {item.Title}{price} - {item.Description}",
            $"You might be interested in our {item.Title}{price}. {item.Description}",
            $"May I also suggest our {item.Title}{price}? {item.Description}"
        };

        // Select template based on context
        if (context.CurrentStage == "confirmation")
        {
            return templates[0]; // "By the way" is softer at confirmation
        }
        else if (context.NumberOfPeople > 2)
        {
            return templates[1]; // "I'd also like to mention" for groups
        }

        return templates[new Random().Next(templates.Count)];
    }
}
