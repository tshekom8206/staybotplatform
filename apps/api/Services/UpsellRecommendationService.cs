using Hostr.Api.Data;
using Hostr.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Hostr.Api.Services;

/// <summary>
/// Database-driven upselling service with strict anti-hallucination validation.
/// Only suggests services from the Services table where IsAvailable=TRUE and IsChargeable=TRUE.
/// </summary>
public class UpsellRecommendationService : IUpsellRecommendationService
{
    private readonly HostrDbContext _context;
    private readonly ILogger<UpsellRecommendationService> _logger;

    // Cross-sell category mapping: Request Category → Upsell Categories
    private static readonly Dictionary<string, List<string>> CategoryCrossSellMap = new()
    {
        { "Recreation", new List<string> { "Wellness", "Dining", "Tours" } },
        { "Wellness", new List<string> { "Recreation", "Dining" } },
        { "Dining", new List<string> { "Tours", "Recreation", "Entertainment" } },
        { "Tours", new List<string> { "Dining", "Transportation" } },
        { "Transportation", new List<string> { "Tours", "Dining" } },
        { "Entertainment", new List<string> { "Dining", "Recreation" } },
        { "Room Service", new List<string> { "Dining", "Wellness" } },
        { "Housekeeping", new List<string> { "Laundry", "Room Service" } },
        { "Laundry", new List<string> { "Housekeeping", "Dry Cleaning" } }
    };

    public UpsellRecommendationService(HostrDbContext context, ILogger<UpsellRecommendationService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Service?> GetRelevantUpsellAsync(int tenantId, string requestCategory, int? excludeServiceId = null)
    {
        try
        {
            // Get cross-sell categories for the request category
            if (!CategoryCrossSellMap.TryGetValue(requestCategory, out var upsellCategories))
            {
                _logger.LogDebug("No cross-sell mapping found for category: {Category}", requestCategory);
                return null;
            }

            // Query database for paid services in cross-sell categories
            var query = _context.Services
                .Where(s => s.TenantId == tenantId
                    && s.IsAvailable == true
                    && s.IsChargeable == true
                    && upsellCategories.Contains(s.Category));

            // Exclude the requested service if specified
            if (excludeServiceId.HasValue)
            {
                query = query.Where(s => s.Id != excludeServiceId.Value);
            }

            // Get ONE service, ordered by price (highest first)
            var upsellService = await query
                .OrderByDescending(s => s.Price)
                .FirstOrDefaultAsync();

            if (upsellService != null)
            {
                _logger.LogInformation("Found upsell: {ServiceName} (${Price}) for category {Category}",
                    upsellService.Name, upsellService.Price, requestCategory);
            }

            return upsellService;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting upsell for category: {Category}", requestCategory);
            return null;
        }
    }

    public async Task<List<Service>> GetTopHighValueServicesAsync(int tenantId, int limit = 2)
    {
        try
        {
            var services = await _context.Services
                .Where(s => s.TenantId == tenantId
                    && s.IsAvailable == true
                    && s.IsChargeable == true)
                .OrderByDescending(s => s.Price)
                .Take(limit)
                .ToListAsync();

            _logger.LogInformation("Found {Count} high-value services for tenant {TenantId}", services.Count, tenantId);
            return services;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting high-value services for tenant: {TenantId}", tenantId);
            return new List<Service>();
        }
    }

    public async Task<bool> LogUpsellSuggestionAsync(int tenantId, int conversationId, int serviceId, string context, int? triggerServiceId = null)
    {
        try
        {
            // Validate that service exists and is chargeable
            var service = await _context.Services
                .FirstOrDefaultAsync(s => s.Id == serviceId
                    && s.TenantId == tenantId
                    && s.IsAvailable == true
                    && s.IsChargeable == true);

            if (service == null)
            {
                _logger.LogWarning("Cannot log upsell - service {ServiceId} not found or not chargeable", serviceId);
                return false;
            }

            // Create UpsellMetric record
            var metric = new UpsellMetric
            {
                TenantId = tenantId,
                ConversationId = conversationId,
                SuggestedServiceId = serviceId,
                SuggestedServiceName = service.Name,
                SuggestedServicePrice = service.Price ?? 0,  // Handle nullable decimal
                SuggestedServiceCategory = service.Category,
                TriggerContext = context,
                TriggerServiceId = triggerServiceId,
                WasSuggested = true,
                WasAccepted = false,
                SuggestedAt = DateTime.UtcNow
            };

            _context.UpsellMetrics.Add(metric);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Logged upsell suggestion: {ServiceName} in conversation {ConversationId}",
                service.Name, conversationId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging upsell suggestion for service {ServiceId}", serviceId);
            return false;
        }
    }

    public async Task<bool> MarkUpsellAcceptedAsync(int conversationId, int serviceId, decimal revenue)
    {
        try
        {
            // Find the most recent upsell suggestion for this conversation and service
            var metric = await _context.UpsellMetrics
                .Where(m => m.ConversationId == conversationId
                    && m.SuggestedServiceId == serviceId
                    && m.WasSuggested == true
                    && m.WasAccepted == false)
                .OrderByDescending(m => m.SuggestedAt)
                .FirstOrDefaultAsync();

            if (metric == null)
            {
                _logger.LogWarning("No matching upsell suggestion found for conversation {ConversationId}, service {ServiceId}",
                    conversationId, serviceId);
                return false;
            }

            // Mark as accepted
            metric.WasAccepted = true;
            metric.AcceptedAt = DateTime.UtcNow;
            metric.Revenue = revenue;
            metric.AcceptedVia = "task_creation";

            await _context.SaveChangesAsync();

            _logger.LogInformation("Marked upsell as accepted: {ServiceName} (${Revenue}) in conversation {ConversationId}",
                metric.SuggestedServiceName, revenue, conversationId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking upsell as accepted for service {ServiceId}", serviceId);
            return false;
        }
    }
}
