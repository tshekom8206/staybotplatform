using Hostr.Api.DTOs;
using Hostr.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hostr.Api.Controllers;

/// <summary>
/// Controller for Hotelier Intelligence Reports
/// Provides strategic operational insights for hotel management
/// </summary>
[ApiController]
[Route("api/analytics/hotelier")]
[Authorize]
public class HotelierReportsController : ControllerBase
{
    private readonly IHotelierReportsService _reportsService;
    private readonly ILogger<HotelierReportsController> _logger;

    public HotelierReportsController(
        IHotelierReportsService reportsService,
        ILogger<HotelierReportsController> logger)
    {
        _reportsService = reportsService;
        _logger = logger;
    }

    private int GetTenantId()
    {
        if (HttpContext.Items.TryGetValue("TenantId", out var tenantIdObj) && tenantIdObj is int tenantId)
        {
            return tenantId;
        }
        var tenantClaim = User.FindFirst("tenant_id");
        if (tenantClaim != null && int.TryParse(tenantClaim.Value, out var claimTenantId))
        {
            return claimTenantId;
        }
        return 1;
    }

    /// <summary>
    /// Get service demand heatmap showing request volume by hour and day of week
    /// Helps optimize staff scheduling based on when guests make requests
    /// </summary>
    [HttpGet("service-demand-heatmap")]
    public async Task<ActionResult<ServiceDemandHeatmapDto>> GetServiceDemandHeatmap(
        [FromQuery] string startDate = "30daysAgo",
        [FromQuery] string endDate = "today",
        [FromQuery] string? department = null)
    {
        try
        {
            var tenantId = GetTenantId();
            var (start, end) = ParseDateRange(startDate, endDate);

            var data = await _reportsService.GetServiceDemandHeatmapAsync(tenantId, start, end, department);
            return Ok(data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching service demand heatmap");
            return StatusCode(500, new { error = "Failed to fetch service demand heatmap" });
        }
    }

    /// <summary>
    /// Get maintenance issue trends with category breakdown and repeat issue analysis
    /// Identifies recurring problems by room/floor to prioritize repairs
    /// </summary>
    [HttpGet("maintenance-trends")]
    public async Task<ActionResult<MaintenanceTrendsDto>> GetMaintenanceTrends(
        [FromQuery] string startDate = "30daysAgo",
        [FromQuery] string endDate = "today")
    {
        try
        {
            var tenantId = GetTenantId();
            var (start, end) = ParseDateRange(startDate, endDate);

            var data = await _reportsService.GetMaintenanceTrendsAsync(tenantId, start, end);
            return Ok(data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching maintenance trends");
            return StatusCode(500, new { error = "Failed to fetch maintenance trends" });
        }
    }

    /// <summary>
    /// Get guest journey funnel from portal landing to action completion
    /// Shows drop-off points in self-service flows
    /// </summary>
    [HttpGet("guest-journey-funnel")]
    public async Task<ActionResult<GuestJourneyFunnelDto>> GetGuestJourneyFunnel(
        [FromQuery] string startDate = "30daysAgo",
        [FromQuery] string endDate = "today")
    {
        try
        {
            var tenantId = GetTenantId();
            var (start, end) = ParseDateRange(startDate, endDate);

            var data = await _reportsService.GetGuestJourneyFunnelAsync(tenantId, start, end);
            return Ok(data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching guest journey funnel");
            return StatusCode(500, new { error = "Failed to fetch guest journey funnel" });
        }
    }

    /// <summary>
    /// Get correlation between response time and guest satisfaction
    /// Proves ROI of quick service response
    /// </summary>
    [HttpGet("response-satisfaction-correlation")]
    public async Task<ActionResult<ResponseSatisfactionCorrelationDto>> GetResponseSatisfactionCorrelation(
        [FromQuery] string startDate = "90daysAgo",
        [FromQuery] string endDate = "today")
    {
        try
        {
            var tenantId = GetTenantId();
            var (start, end) = ParseDateRange(startDate, endDate);

            var data = await _reportsService.GetResponseSatisfactionCorrelationAsync(tenantId, start, end);
            return Ok(data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching response-satisfaction correlation");
            return StatusCode(500, new { error = "Failed to fetch response-satisfaction correlation" });
        }
    }

    /// <summary>
    /// Get WhatsApp escalation metrics showing bot vs agent resolution rates
    /// Measures self-service effectiveness
    /// </summary>
    [HttpGet("whatsapp-escalation")]
    public async Task<ActionResult<WhatsAppEscalationDto>> GetWhatsAppEscalation(
        [FromQuery] string startDate = "30daysAgo",
        [FromQuery] string endDate = "today")
    {
        try
        {
            var tenantId = GetTenantId();
            var (start, end) = ParseDateRange(startDate, endDate);

            var data = await _reportsService.GetWhatsAppEscalationAsync(tenantId, start, end);
            return Ok(data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching WhatsApp escalation data");
            return StatusCode(500, new { error = "Failed to fetch WhatsApp escalation data" });
        }
    }

    /// <summary>
    /// Get upselling performance metrics including conversion rates and revenue
    /// </summary>
    [HttpGet("upsell-performance")]
    public async Task<ActionResult<UpsellPerformanceDto>> GetUpsellPerformance(
        [FromQuery] string startDate = "30daysAgo",
        [FromQuery] string endDate = "today")
    {
        try
        {
            var tenantId = GetTenantId();
            var (start, end) = ParseDateRange(startDate, endDate);

            var data = await _reportsService.GetUpsellPerformanceAsync(tenantId, start, end);
            return Ok(data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching upsell performance");
            return StatusCode(500, new { error = "Failed to fetch upsell performance" });
        }
    }

    private static (DateTime start, DateTime end) ParseDateRange(string startDate, string endDate)
    {
        var end = endDate.ToLower() switch
        {
            "today" => DateTime.Today.AddDays(1).AddSeconds(-1),
            "yesterday" => DateTime.Today.AddSeconds(-1),
            _ => DateTime.TryParse(endDate, out var parsedEnd) ? parsedEnd : DateTime.Today.AddDays(1).AddSeconds(-1)
        };

        var start = startDate.ToLower() switch
        {
            "7daysago" => DateTime.Today.AddDays(-7),
            "30daysago" => DateTime.Today.AddDays(-30),
            "90daysago" => DateTime.Today.AddDays(-90),
            "today" => DateTime.Today,
            "yesterday" => DateTime.Today.AddDays(-1),
            _ => DateTime.TryParse(startDate, out var parsedStart) ? parsedStart : DateTime.Today.AddDays(-30)
        };

        return (start, end);
    }
}
