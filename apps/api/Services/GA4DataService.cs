using Google.Analytics.Data.V1Beta;
using Hostr.Api.Data;
using Hostr.Api.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Hostr.Api.Services;

/// <summary>
/// Service for fetching analytics data from Google Analytics 4 Data API
/// </summary>
public class GA4DataService : IGA4DataService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<GA4DataService> _logger;
    private readonly HostrDbContext _dbContext;
    private readonly string? _propertyId;
    private readonly string? _credentialsPath;
    private readonly bool _isConfigured;
    private BetaAnalyticsDataClient? _client;

    public GA4DataService(IConfiguration configuration, ILogger<GA4DataService> logger, HostrDbContext dbContext)
    {
        _configuration = configuration;
        _logger = logger;
        _dbContext = dbContext;
        _propertyId = configuration["GA4:PropertyId"];
        _credentialsPath = configuration["GA4:ServiceAccountKeyPath"];
        _isConfigured = !string.IsNullOrEmpty(_propertyId) &&
                        !string.IsNullOrEmpty(_credentialsPath);

        if (!_isConfigured)
        {
            _logger.LogWarning("GA4 Data API is not configured. " +
                "Set GA4:PropertyId and GA4:ServiceAccountKeyPath to enable real data.");
        }
        else
        {
            _logger.LogInformation("GA4 Data API configured with PropertyId: {PropertyId}", _propertyId);
        }
    }

    /// <summary>
    /// Get the tenant slug for filtering GA4 data
    /// </summary>
    private async Task<string?> GetTenantSlugAsync(int tenantId)
    {
        var tenant = await _dbContext.Tenants
            .Where(t => t.Id == tenantId)
            .Select(t => t.Slug)
            .FirstOrDefaultAsync();

        return tenant;
    }

    /// <summary>
    /// Create a filter for GA4 queries based on tenant_slug custom dimension
    /// The guest portal sends tenant_slug as a custom dimension with each event
    /// </summary>
    private FilterExpression CreateTenantFilter(string tenantSlug)
    {
        return new FilterExpression
        {
            Filter = new Filter
            {
                FieldName = "customEvent:tenant_slug",
                StringFilter = new Filter.Types.StringFilter
                {
                    Value = tenantSlug,
                    MatchType = Filter.Types.StringFilter.Types.MatchType.Exact
                }
            }
        };
    }

    public bool IsConfigured => _isConfigured;

    private BetaAnalyticsDataClient GetClient()
    {
        if (_client != null) return _client;

        if (!_isConfigured)
        {
            throw new InvalidOperationException("GA4 is not configured. Set GA4:PropertyId and GA4:ServiceAccountKeyPath in appsettings.json");
        }

        // Set environment variable for authentication
        var fullPath = Path.Combine(AppContext.BaseDirectory, _credentialsPath!);
        if (!File.Exists(fullPath))
        {
            // Try relative to current directory
            fullPath = _credentialsPath!;
        }

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"GA4 credentials file not found: {fullPath}");
        }

        Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", fullPath);
        _client = BetaAnalyticsDataClient.Create();
        _logger.LogInformation("GA4 client initialized successfully");
        return _client;
    }

    public async Task<GA4OverviewDto> GetOverviewAsync(int tenantId, string startDate, string endDate)
    {
        var client = GetClient();
        var tenantSlug = await GetTenantSlugAsync(tenantId);

        var request = new RunReportRequest
        {
            Property = _propertyId,
            DateRanges = { new DateRange { StartDate = startDate, EndDate = endDate } },
            Metrics =
            {
                new Metric { Name = "sessions" },
                new Metric { Name = "activeUsers" },
                new Metric { Name = "screenPageViews" },
                new Metric { Name = "engagementRate" },
                new Metric { Name = "averageSessionDuration" },
                new Metric { Name = "bounceRate" }
            }
        };

        // TODO: Re-enable tenant filtering once tenant_slug custom dimension is registered in GA4 Admin
        // For now, showing all data since custom dimension filtering requires GA4 setup
        // if (!string.IsNullOrEmpty(tenantSlug))
        // {
        //     request.DimensionFilter = CreateTenantFilter(tenantSlug);
        // }

        var response = await client.RunReportAsync(request);

        var result = new GA4OverviewDto();

        if (response.Rows.Count > 0)
        {
            var row = response.Rows[0];
            for (int i = 0; i < response.MetricHeaders.Count; i++)
            {
                var metricName = response.MetricHeaders[i].Name;
                var value = row.MetricValues[i].Value;

                switch (metricName)
                {
                    case "sessions":
                        result.Sessions = int.TryParse(value, out var sessions) ? sessions : 0;
                        break;
                    case "activeUsers":
                        result.ActiveUsers = int.TryParse(value, out var users) ? users : 0;
                        break;
                    case "screenPageViews":
                        result.PageViews = int.TryParse(value, out var views) ? views : 0;
                        break;
                    case "engagementRate":
                        result.EngagementRate = double.TryParse(value, out var engRate) ? Math.Round(engRate, 2) : 0;
                        break;
                    case "averageSessionDuration":
                        result.AvgSessionDuration = double.TryParse(value, out var duration) ? Math.Round(duration, 0) : 0;
                        break;
                    case "bounceRate":
                        result.BounceRate = double.TryParse(value, out var bounce) ? Math.Round(bounce, 2) : 0;
                        break;
                }
            }
        }

        // Get comparison data for change percentages
        var comparisonResult = await GetComparisonDataAsync(tenantId, startDate, endDate);
        result.SessionsChange = comparisonResult.SessionsChange;
        result.UsersChange = comparisonResult.UsersChange;
        result.PageViewsChange = comparisonResult.PageViewsChange;

        return result;
    }

    private async Task<(double SessionsChange, double UsersChange, double PageViewsChange)> GetComparisonDataAsync(
        int tenantId, string startDate, string endDate)
    {
        try
        {
            // Calculate previous period
            var start = ParseDate(startDate);
            var end = ParseDate(endDate);
            var periodDays = (end - start).Days;
            var previousStart = start.AddDays(-periodDays - 1);
            var previousEnd = start.AddDays(-1);

            var client = GetClient();
            var tenantSlug = await GetTenantSlugAsync(tenantId);

            var currentRequest = new RunReportRequest
            {
                Property = _propertyId,
                DateRanges = { new DateRange { StartDate = startDate, EndDate = endDate } },
                Metrics =
                {
                    new Metric { Name = "sessions" },
                    new Metric { Name = "activeUsers" },
                    new Metric { Name = "screenPageViews" }
                }
            };

            var previousRequest = new RunReportRequest
            {
                Property = _propertyId,
                DateRanges = { new DateRange { StartDate = previousStart.ToString("yyyy-MM-dd"), EndDate = previousEnd.ToString("yyyy-MM-dd") } },
                Metrics =
                {
                    new Metric { Name = "sessions" },
                    new Metric { Name = "activeUsers" },
                    new Metric { Name = "screenPageViews" }
                }
            };

            // Apply tenant filter if tenant has a configured slug
            if (!string.IsNullOrEmpty(tenantSlug))
            {
                currentRequest.DimensionFilter = CreateTenantFilter(tenantSlug);
                previousRequest.DimensionFilter = CreateTenantFilter(tenantSlug);
            }

            var currentResponse = await client.RunReportAsync(currentRequest);
            var previousResponse = await client.RunReportAsync(previousRequest);

            double currentSessions = 0, currentUsers = 0, currentViews = 0;
            double previousSessions = 0, previousUsers = 0, previousViews = 0;

            if (currentResponse.Rows.Count > 0)
            {
                var row = currentResponse.Rows[0];
                currentSessions = double.Parse(row.MetricValues[0].Value);
                currentUsers = double.Parse(row.MetricValues[1].Value);
                currentViews = double.Parse(row.MetricValues[2].Value);
            }

            if (previousResponse.Rows.Count > 0)
            {
                var row = previousResponse.Rows[0];
                previousSessions = double.Parse(row.MetricValues[0].Value);
                previousUsers = double.Parse(row.MetricValues[1].Value);
                previousViews = double.Parse(row.MetricValues[2].Value);
            }

            return (
                CalculatePercentageChange(previousSessions, currentSessions),
                CalculatePercentageChange(previousUsers, currentUsers),
                CalculatePercentageChange(previousViews, currentViews)
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to calculate comparison data");
            return (0, 0, 0);
        }
    }

    public async Task<IEnumerable<PageViewDto>> GetTopPagesAsync(int tenantId, string startDate, string endDate, int limit = 10)
    {
        var client = GetClient();
        var tenantSlug = await GetTenantSlugAsync(tenantId);

        var request = new RunReportRequest
        {
            Property = _propertyId,
            DateRanges = { new DateRange { StartDate = startDate, EndDate = endDate } },
            Dimensions =
            {
                new Dimension { Name = "pagePath" },
                new Dimension { Name = "pageTitle" }
            },
            Metrics =
            {
                new Metric { Name = "screenPageViews" },
                new Metric { Name = "activeUsers" },
                new Metric { Name = "averageSessionDuration" }
            },
            OrderBys =
            {
                new OrderBy { Metric = new OrderBy.Types.MetricOrderBy { MetricName = "screenPageViews" }, Desc = true }
            },
            Limit = limit
        };

        // TODO: Re-enable tenant filtering once tenant_slug custom dimension is registered in GA4 Admin
        // For now, showing all data since custom dimension filtering requires GA4 setup
        // if (!string.IsNullOrEmpty(tenantSlug))
        // {
        //     request.DimensionFilter = CreateTenantFilter(tenantSlug);
        // }

        var response = await client.RunReportAsync(request);
        var result = new List<PageViewDto>();

        foreach (var row in response.Rows)
        {
            result.Add(new PageViewDto
            {
                PagePath = row.DimensionValues[0].Value,
                PageTitle = row.DimensionValues[1].Value,
                Views = int.TryParse(row.MetricValues[0].Value, out var views) ? views : 0,
                UniqueViews = int.TryParse(row.MetricValues[1].Value, out var unique) ? unique : 0,
                AvgTimeOnPage = double.TryParse(row.MetricValues[2].Value, out var time) ? Math.Round(time, 0) : 0
            });
        }

        return result;
    }

    public async Task<IEnumerable<PageViewTimeSeriesDto>> GetPageViewsTimeSeriesAsync(int tenantId, string startDate, string endDate)
    {
        var client = GetClient();
        var tenantSlug = await GetTenantSlugAsync(tenantId);

        var request = new RunReportRequest
        {
            Property = _propertyId,
            DateRanges = { new DateRange { StartDate = startDate, EndDate = endDate } },
            Dimensions =
            {
                new Dimension { Name = "date" }
            },
            Metrics =
            {
                new Metric { Name = "screenPageViews" },
                new Metric { Name = "sessions" },
                new Metric { Name = "activeUsers" }
            },
            OrderBys =
            {
                new OrderBy { Dimension = new OrderBy.Types.DimensionOrderBy { DimensionName = "date" } }
            }
        };

        // TODO: Re-enable tenant filtering once tenant_slug custom dimension is registered in GA4 Admin
        // For now, showing all data since custom dimension filtering requires GA4 setup
        // if (!string.IsNullOrEmpty(tenantSlug))
        // {
        //     request.DimensionFilter = CreateTenantFilter(tenantSlug);
        // }

        var response = await client.RunReportAsync(request);
        var result = new List<PageViewTimeSeriesDto>();

        foreach (var row in response.Rows)
        {
            var dateStr = row.DimensionValues[0].Value; // Format: YYYYMMDD
            var formattedDate = $"{dateStr[0..4]}-{dateStr[4..6]}-{dateStr[6..8]}";

            result.Add(new PageViewTimeSeriesDto
            {
                Date = formattedDate,
                PageViews = int.TryParse(row.MetricValues[0].Value, out var views) ? views : 0,
                Sessions = int.TryParse(row.MetricValues[1].Value, out var sessions) ? sessions : 0,
                Users = int.TryParse(row.MetricValues[2].Value, out var users) ? users : 0
            });
        }

        return result;
    }

    public async Task<IEnumerable<EventDto>> GetEventsAsync(int tenantId, string startDate, string endDate)
    {
        var client = GetClient();
        var tenantSlug = await GetTenantSlugAsync(tenantId);

        var request = new RunReportRequest
        {
            Property = _propertyId,
            DateRanges = { new DateRange { StartDate = startDate, EndDate = endDate } },
            Dimensions =
            {
                new Dimension { Name = "eventName" }
            },
            Metrics =
            {
                new Metric { Name = "eventCount" },
                new Metric { Name = "totalUsers" }
            },
            OrderBys =
            {
                new OrderBy { Metric = new OrderBy.Types.MetricOrderBy { MetricName = "eventCount" }, Desc = true }
            }
        };

        // TODO: Re-enable tenant filtering once tenant_slug custom dimension is registered in GA4 Admin
        // For now, showing all data since custom dimension filtering requires GA4 setup
        // if (!string.IsNullOrEmpty(tenantSlug))
        // {
        //     request.DimensionFilter = CreateTenantFilter(tenantSlug);
        // }

        var response = await client.RunReportAsync(request);
        var result = new List<EventDto>();

        foreach (var row in response.Rows)
        {
            result.Add(new EventDto
            {
                EventName = row.DimensionValues[0].Value,
                Count = int.TryParse(row.MetricValues[0].Value, out var count) ? count : 0,
                UniqueUsers = int.TryParse(row.MetricValues[1].Value, out var users) ? users : 0
            });
        }

        return result;
    }

    public async Task<EngagementDto> GetEngagementAsync(int tenantId, string startDate, string endDate)
    {
        var client = GetClient();
        var tenantSlug = await GetTenantSlugAsync(tenantId);

        var request = new RunReportRequest
        {
            Property = _propertyId,
            DateRanges = { new DateRange { StartDate = startDate, EndDate = endDate } },
            Metrics =
            {
                new Metric { Name = "averageSessionDuration" },
                new Metric { Name = "engagementRate" },
                new Metric { Name = "bounceRate" },
                new Metric { Name = "engagedSessions" },
                new Metric { Name = "screenPageViewsPerSession" }
            }
        };

        // TODO: Re-enable tenant filtering once tenant_slug custom dimension is registered in GA4 Admin
        // For now, showing all data since custom dimension filtering requires GA4 setup
        // if (!string.IsNullOrEmpty(tenantSlug))
        // {
        //     request.DimensionFilter = CreateTenantFilter(tenantSlug);
        // }

        var response = await client.RunReportAsync(request);
        var result = new EngagementDto();

        if (response.Rows.Count > 0)
        {
            var row = response.Rows[0];
            result.AvgSessionDuration = double.TryParse(row.MetricValues[0].Value, out var duration) ? Math.Round(duration, 1) : 0;
            result.EngagementRate = double.TryParse(row.MetricValues[1].Value, out var engRate) ? Math.Round(engRate, 2) : 0;
            result.BounceRate = double.TryParse(row.MetricValues[2].Value, out var bounce) ? Math.Round(bounce, 2) : 0;
            result.EngagedSessions = int.TryParse(row.MetricValues[3].Value, out var engaged) ? engaged : 0;
            result.AvgPagesPerSession = double.TryParse(row.MetricValues[4].Value, out var pages) ? Math.Round(pages, 1) : 0;
        }

        return result;
    }

    #region Helper Methods

    private static FilterExpression CreateTenantFilter(int tenantId)
    {
        return new FilterExpression
        {
            Filter = new Filter
            {
                FieldName = "customEvent:tenant_id",
                StringFilter = new Filter.Types.StringFilter
                {
                    Value = tenantId.ToString(),
                    MatchType = Filter.Types.StringFilter.Types.MatchType.Exact
                }
            }
        };
    }

    private static DateTime ParseDate(string dateStr)
    {
        // Handle relative dates
        return dateStr.ToLower() switch
        {
            "today" => DateTime.Today,
            "yesterday" => DateTime.Today.AddDays(-1),
            "7daysago" => DateTime.Today.AddDays(-7),
            "30daysago" => DateTime.Today.AddDays(-30),
            "90daysago" => DateTime.Today.AddDays(-90),
            _ => DateTime.TryParse(dateStr, out var date) ? date : DateTime.Today
        };
    }

    private static double CalculatePercentageChange(double previous, double current)
    {
        if (previous == 0) return current > 0 ? 100 : 0;
        return Math.Round(((current - previous) / previous) * 100, 1);
    }

    #endregion
}
