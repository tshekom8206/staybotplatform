using Hostr.Api.DTOs;

namespace Hostr.Api.Services;

/// <summary>
/// Service interface for Hotelier Intelligence Reports
/// </summary>
public interface IHotelierReportsService
{
    /// <summary>
    /// Get service demand heatmap showing request volume by hour and day of week
    /// </summary>
    Task<ServiceDemandHeatmapDto> GetServiceDemandHeatmapAsync(int tenantId, DateTime startDate, DateTime endDate, string? department = null);

    /// <summary>
    /// Get maintenance issue trends with category breakdown and repeat analysis
    /// </summary>
    Task<MaintenanceTrendsDto> GetMaintenanceTrendsAsync(int tenantId, DateTime startDate, DateTime endDate);

    /// <summary>
    /// Get guest journey funnel from portal landing to action completion
    /// </summary>
    Task<GuestJourneyFunnelDto> GetGuestJourneyFunnelAsync(int tenantId, DateTime startDate, DateTime endDate);

    /// <summary>
    /// Get correlation between response time and guest satisfaction
    /// </summary>
    Task<ResponseSatisfactionCorrelationDto> GetResponseSatisfactionCorrelationAsync(int tenantId, DateTime startDate, DateTime endDate);

    /// <summary>
    /// Get WhatsApp escalation metrics showing bot vs agent resolution
    /// </summary>
    Task<WhatsAppEscalationDto> GetWhatsAppEscalationAsync(int tenantId, DateTime startDate, DateTime endDate);

    /// <summary>
    /// Get upselling performance metrics
    /// </summary>
    Task<UpsellPerformanceDto> GetUpsellPerformanceAsync(int tenantId, DateTime startDate, DateTime endDate);
}
