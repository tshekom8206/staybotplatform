namespace Hostr.Api.DTOs;

/// <summary>
/// GA4 Analytics Overview Data Transfer Object
/// </summary>
public class GA4OverviewDto
{
    public int Sessions { get; set; }
    public int ActiveUsers { get; set; }
    public int PageViews { get; set; }
    public double EngagementRate { get; set; }
    public double AvgSessionDuration { get; set; }
    public double BounceRate { get; set; }

    // Comparison with previous period
    public double SessionsChange { get; set; }
    public double UsersChange { get; set; }
    public double PageViewsChange { get; set; }
}

/// <summary>
/// Page view data for a specific page
/// </summary>
public class PageViewDto
{
    public string PagePath { get; set; } = string.Empty;
    public string PageTitle { get; set; } = string.Empty;
    public int Views { get; set; }
    public int UniqueViews { get; set; }
    public double AvgTimeOnPage { get; set; }
}

/// <summary>
/// Page views over time for charting
/// </summary>
public class PageViewTimeSeriesDto
{
    public string Date { get; set; } = string.Empty;
    public int PageViews { get; set; }
    public int Sessions { get; set; }
    public int Users { get; set; }
}

/// <summary>
/// Custom event data from GA4
/// </summary>
public class EventDto
{
    public string EventName { get; set; } = string.Empty;
    public int Count { get; set; }
    public int UniqueUsers { get; set; }
}

/// <summary>
/// Detailed event breakdown with parameters
/// </summary>
public class EventDetailDto
{
    public string EventName { get; set; } = string.Empty;
    public int Count { get; set; }
    public Dictionary<string, string> Parameters { get; set; } = new();
}

/// <summary>
/// User engagement metrics
/// </summary>
public class EngagementDto
{
    public double AvgSessionDuration { get; set; }
    public double EngagementRate { get; set; }
    public double BounceRate { get; set; }
    public int EngagedSessions { get; set; }
    public double AvgPagesPerSession { get; set; }
}

/// <summary>
/// Request parameters for GA4 queries
/// </summary>
public class GA4QueryParams
{
    public string StartDate { get; set; } = "7daysAgo";
    public string EndDate { get; set; } = "today";
}
