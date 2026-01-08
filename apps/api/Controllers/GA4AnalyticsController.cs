using Hostr.Api.DTOs;
using Hostr.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hostr.Api.Controllers;

/// <summary>
/// Controller for Google Analytics 4 data endpoints
/// Provides per-tenant analytics data from GA4
/// </summary>
[ApiController]
[Route("api/ga4")]
[Authorize]
public class GA4AnalyticsController : ControllerBase
{
    private readonly IGA4DataService _ga4Service;
    private readonly ILogger<GA4AnalyticsController> _logger;

    public GA4AnalyticsController(IGA4DataService ga4Service, ILogger<GA4AnalyticsController> logger)
    {
        _ga4Service = ga4Service;
        _logger = logger;
    }

    /// <summary>
    /// Get the tenant ID from the current context
    /// </summary>
    private int GetTenantId()
    {
        if (HttpContext.Items.TryGetValue("TenantId", out var tenantIdObj) && tenantIdObj is int tenantId)
        {
            return tenantId;
        }

        // Fallback for testing - get from claims
        var tenantClaim = User.FindFirst("tenant_id");
        if (tenantClaim != null && int.TryParse(tenantClaim.Value, out var claimTenantId))
        {
            return claimTenantId;
        }

        return 1; // Default for development
    }

    /// <summary>
    /// Check if GA4 integration is configured
    /// </summary>
    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        return Ok(new
        {
            configured = _ga4Service.IsConfigured,
            message = _ga4Service.IsConfigured
                ? "GA4 Data API is configured and ready"
                : "GA4 Data API is not configured. Using mock data. Configure GA4:PropertyId and GA4:ServiceAccountKeyPath to enable."
        });
    }

    /// <summary>
    /// Test GA4 connection (diagnostic endpoint)
    /// </summary>
    [HttpGet("test")]
    [AllowAnonymous]
    public async Task<IActionResult> TestConnection()
    {
        try
        {
            var credPath = Path.Combine(AppContext.BaseDirectory, "ga4-credentials.json");
            var credExists = System.IO.File.Exists(credPath);
            var credAltPath = "ga4-credentials.json";
            var credAltExists = System.IO.File.Exists(credAltPath);

            if (!_ga4Service.IsConfigured)
            {
                return Ok(new
                {
                    success = false,
                    error = "GA4 is not configured",
                    baseDir = AppContext.BaseDirectory,
                    credPath = credPath,
                    credExists = credExists,
                    credAltPath = credAltPath,
                    credAltExists = credAltExists
                });
            }

            // Try a simple API call
            var data = await _ga4Service.GetOverviewAsync(1, "7daysAgo", "today");
            return Ok(new
            {
                success = true,
                baseDir = AppContext.BaseDirectory,
                credPath = credPath,
                credExists = credExists,
                data = data
            });
        }
        catch (Exception ex)
        {
            return Ok(new
            {
                success = false,
                error = ex.Message,
                innerError = ex.InnerException?.Message,
                stackTrace = ex.StackTrace?.Substring(0, Math.Min(500, ex.StackTrace?.Length ?? 0)),
                baseDir = AppContext.BaseDirectory
            });
        }
    }

    /// <summary>
    /// Get analytics overview metrics
    /// </summary>
    [HttpGet("overview")]
    public async Task<ActionResult<GA4OverviewDto>> GetOverview(
        [FromQuery] string startDate = "7daysAgo",
        [FromQuery] string endDate = "today")
    {
        try
        {
            var tenantId = GetTenantId();
            var data = await _ga4Service.GetOverviewAsync(tenantId, startDate, endDate);
            return Ok(data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching GA4 overview");
            return StatusCode(500, new { error = "Failed to fetch analytics overview", details = ex.Message });
        }
    }

    /// <summary>
    /// Get top pages by views
    /// </summary>
    [HttpGet("top-pages")]
    public async Task<ActionResult<IEnumerable<PageViewDto>>> GetTopPages(
        [FromQuery] string startDate = "7daysAgo",
        [FromQuery] string endDate = "today",
        [FromQuery] int limit = 10)
    {
        try
        {
            var tenantId = GetTenantId();
            var data = await _ga4Service.GetTopPagesAsync(tenantId, startDate, endDate, limit);
            return Ok(data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching GA4 top pages");
            return StatusCode(500, new { error = "Failed to fetch top pages data", details = ex.Message });
        }
    }

    /// <summary>
    /// Get page views over time (for charts)
    /// </summary>
    [HttpGet("page-views")]
    public async Task<ActionResult<IEnumerable<PageViewTimeSeriesDto>>> GetPageViews(
        [FromQuery] string startDate = "7daysAgo",
        [FromQuery] string endDate = "today")
    {
        try
        {
            var tenantId = GetTenantId();
            var data = await _ga4Service.GetPageViewsTimeSeriesAsync(tenantId, startDate, endDate);
            return Ok(data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching GA4 page views time series");
            return StatusCode(500, new { error = "Failed to fetch page views data", details = ex.Message });
        }
    }

    /// <summary>
    /// Get custom events breakdown
    /// </summary>
    [HttpGet("events")]
    public async Task<ActionResult<IEnumerable<EventDto>>> GetEvents(
        [FromQuery] string startDate = "7daysAgo",
        [FromQuery] string endDate = "today")
    {
        try
        {
            var tenantId = GetTenantId();
            var data = await _ga4Service.GetEventsAsync(tenantId, startDate, endDate);
            return Ok(data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching GA4 events");
            return StatusCode(500, new { error = "Failed to fetch events data", details = ex.Message });
        }
    }

    /// <summary>
    /// Get user engagement metrics
    /// </summary>
    [HttpGet("engagement")]
    public async Task<ActionResult<EngagementDto>> GetEngagement(
        [FromQuery] string startDate = "7daysAgo",
        [FromQuery] string endDate = "today")
    {
        try
        {
            var tenantId = GetTenantId();
            var data = await _ga4Service.GetEngagementAsync(tenantId, startDate, endDate);
            return Ok(data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching GA4 engagement");
            return StatusCode(500, new { error = "Failed to fetch engagement data", details = ex.Message });
        }
    }
}
