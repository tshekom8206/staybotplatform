using Hostr.Api.DTOs;

namespace Hostr.Api.Services;

/// <summary>
/// Interface for Google Analytics 4 Data API integration
/// </summary>
public interface IGA4DataService
{
    /// <summary>
    /// Check if GA4 integration is configured and available
    /// </summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Get overview metrics for a tenant
    /// </summary>
    Task<GA4OverviewDto> GetOverviewAsync(int tenantId, string startDate, string endDate);

    /// <summary>
    /// Get page view data for a tenant
    /// </summary>
    Task<IEnumerable<PageViewDto>> GetTopPagesAsync(int tenantId, string startDate, string endDate, int limit = 10);

    /// <summary>
    /// Get page views over time for charting
    /// </summary>
    Task<IEnumerable<PageViewTimeSeriesDto>> GetPageViewsTimeSeriesAsync(int tenantId, string startDate, string endDate);

    /// <summary>
    /// Get custom events for a tenant
    /// </summary>
    Task<IEnumerable<EventDto>> GetEventsAsync(int tenantId, string startDate, string endDate);

    /// <summary>
    /// Get user engagement metrics for a tenant
    /// </summary>
    Task<EngagementDto> GetEngagementAsync(int tenantId, string startDate, string endDate);
}
