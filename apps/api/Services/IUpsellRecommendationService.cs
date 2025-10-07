using Hostr.Api.Models;

namespace Hostr.Api.Services;

/// <summary>
/// Service for database-driven upselling recommendations with strict anti-hallucination validation.
/// Only suggests services that exist in the Services table with IsAvailable=TRUE and IsChargeable=TRUE.
/// </summary>
public interface IUpsellRecommendationService
{
    /// <summary>
    /// Returns ONE paid service from database based on category cross-sell mapping, or null if none available.
    /// NEVER suggests services not in the database.
    /// </summary>
    /// <param name="tenantId">Tenant ID to filter services</param>
    /// <param name="requestCategory">Category of the service being requested (e.g., "Recreation")</param>
    /// <param name="excludeServiceId">Optional service ID to exclude (e.g., the service being requested)</param>
    /// <returns>A single paid Service or null</returns>
    Task<Service?> GetRelevantUpsellAsync(int tenantId, string requestCategory, int? excludeServiceId = null);

    /// <summary>
    /// Returns top N paid services for tenant, ordered by price (highest first).
    /// Used for welcome message paid service highlight.
    /// </summary>
    /// <param name="tenantId">Tenant ID to filter services</param>
    /// <param name="limit">Maximum number of services to return (default 2)</param>
    /// <returns>List of high-value paid services</returns>
    Task<List<Service>> GetTopHighValueServicesAsync(int tenantId, int limit = 2);

    /// <summary>
    /// Logs upsell suggestion to UpsellMetrics table with validation.
    /// Validates that the service exists and is chargeable before logging.
    /// </summary>
    /// <param name="tenantId">Tenant ID</param>
    /// <param name="conversationId">Conversation ID</param>
    /// <param name="serviceId">Suggested service ID</param>
    /// <param name="context">Context that triggered the suggestion (e.g., "pool_inquiry")</param>
    /// <param name="triggerServiceId">Optional ID of service that triggered the upsell</param>
    /// <returns>True if logged successfully, false if validation failed</returns>
    Task<bool> LogUpsellSuggestionAsync(int tenantId, int conversationId, int serviceId, string context, int? triggerServiceId = null);

    /// <summary>
    /// Marks an upsell as accepted when a task is created for it.
    /// Updates the UpsellMetric record with acceptance status and revenue.
    /// </summary>
    /// <param name="conversationId">Conversation ID</param>
    /// <param name="serviceId">Service ID that was accepted</param>
    /// <param name="revenue">Revenue from the accepted service</param>
    /// <returns>True if marked successfully, false if no matching suggestion found</returns>
    Task<bool> MarkUpsellAcceptedAsync(int conversationId, int serviceId, decimal revenue);
}
