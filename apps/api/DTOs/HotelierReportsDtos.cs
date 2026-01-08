namespace Hostr.Api.DTOs;

/// <summary>
/// DTOs for Hotelier Intelligence Reports
/// </summary>

#region Service Demand Heatmap

public class ServiceDemandHeatmapDto
{
    public List<ServiceDemandCell> Data { get; set; } = new();
    public string[] Hours { get; set; } = Enumerable.Range(0, 24).Select(h => $"{h:00}:00").ToArray();
    public string[] Days { get; set; } = { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };
    public Dictionary<string, int> TotalByDepartment { get; set; } = new();
    public int PeakHour { get; set; }
    public string PeakDay { get; set; } = string.Empty;
}

public class ServiceDemandCell
{
    public int Hour { get; set; }
    public int DayOfWeek { get; set; } // 0 = Monday, 6 = Sunday
    public string DayName { get; set; } = string.Empty;
    public int Count { get; set; }
    public string Department { get; set; } = string.Empty;
}

#endregion

#region Maintenance Trends

public class MaintenanceTrendsDto
{
    public List<MaintenanceCategoryTrend> ByCategory { get; set; } = new();
    public List<MaintenanceRoomTrend> TopRooms { get; set; } = new();
    public List<MaintenanceFloorTrend> ByFloor { get; set; } = new();
    public List<RepeatIssue> RepeatIssues { get; set; } = new();
    public int TotalIssues { get; set; }
    public double AvgResolutionHours { get; set; }
    public double RepeatRate { get; set; }
}

public class MaintenanceCategoryTrend
{
    public string Category { get; set; } = string.Empty;
    public int Count { get; set; }
    public int PreviousPeriodCount { get; set; }
    public double ChangePercent { get; set; }
    public double AvgResolutionHours { get; set; }
}

public class MaintenanceRoomTrend
{
    public string RoomNumber { get; set; } = string.Empty;
    public int IssueCount { get; set; }
    public string TopIssue { get; set; } = string.Empty;
    public int RepeatCount { get; set; }
}

public class MaintenanceFloorTrend
{
    public string Floor { get; set; } = string.Empty;
    public int IssueCount { get; set; }
    public double PercentOfTotal { get; set; }
}

public class RepeatIssue
{
    public string RoomNumber { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int OccurrenceCount { get; set; }
    public DateTime FirstReported { get; set; }
    public DateTime LastReported { get; set; }
    public string Status { get; set; } = string.Empty;
}

#endregion

#region Guest Journey Funnel

public class GuestJourneyFunnelDto
{
    public List<FunnelStage> Stages { get; set; } = new();
    public double OverallConversionRate { get; set; }
    public string BiggestDropOff { get; set; } = string.Empty;
    public double BiggestDropOffPercent { get; set; }
}

public class FunnelStage
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Count { get; set; }
    public double ConversionRate { get; set; } // % that reach this stage from previous
    public double DropOffRate { get; set; } // % that leave at this stage
    public double PercentOfTotal { get; set; } // % of total who started
}

#endregion

#region Response Time vs Satisfaction Correlation

public class ResponseSatisfactionCorrelationDto
{
    public List<ResponseSatisfactionPoint> DataPoints { get; set; } = new();
    public double CorrelationStrength { get; set; } // -1 to 1
    public string CorrelationDescription { get; set; } = string.Empty; // "Strong negative" = faster response = higher satisfaction
    public double AvgRatingUnder10Min { get; set; }
    public double AvgRatingOver30Min { get; set; }
    public string Insight { get; set; } = string.Empty;
}

public class ResponseSatisfactionPoint
{
    public string ResponseTimeBucket { get; set; } = string.Empty; // "0-10 min", "10-20 min", etc.
    public int MinMinutes { get; set; }
    public int MaxMinutes { get; set; }
    public double AvgRating { get; set; }
    public int SampleSize { get; set; }
    public double PercentOfTotal { get; set; }
}

#endregion

#region WhatsApp Escalation

public class WhatsAppEscalationDto
{
    public int TotalConversations { get; set; }
    public int BotResolved { get; set; }
    public int EscalatedToAgent { get; set; }
    public double EscalationRate { get; set; }
    public double BotSuccessRate { get; set; }
    public List<EscalationByIntent> ByIntent { get; set; } = new();
    public List<EscalationTrend> DailyTrend { get; set; } = new();
    public string TopEscalationReason { get; set; } = string.Empty;
}

public class EscalationByIntent
{
    public string Intent { get; set; } = string.Empty;
    public int TotalCount { get; set; }
    public int EscalatedCount { get; set; }
    public double EscalationRate { get; set; }
}

public class EscalationTrend
{
    public string Date { get; set; } = string.Empty;
    public int Total { get; set; }
    public int Escalated { get; set; }
    public double Rate { get; set; }
}

#endregion

#region Upselling Performance (extends existing)

public class UpsellPerformanceDto
{
    public int TotalSuggestions { get; set; }
    public int TotalAcceptances { get; set; }
    public double ConversionRate { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal AvgOrderValue { get; set; }
    public List<UpsellByCategory> ByCategory { get; set; } = new();
    public List<UpsellByService> TopServices { get; set; } = new();
    public List<UpsellTrend> WeeklyTrend { get; set; } = new();
}

public class UpsellByCategory
{
    public string Category { get; set; } = string.Empty;
    public int Suggestions { get; set; }
    public int Acceptances { get; set; }
    public double ConversionRate { get; set; }
    public decimal Revenue { get; set; }
}

public class UpsellByService
{
    public string ServiceName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Suggestions { get; set; }
    public int Acceptances { get; set; }
    public double ConversionRate { get; set; }
    public decimal Revenue { get; set; }
}

public class UpsellTrend
{
    public string Week { get; set; } = string.Empty;
    public int Suggestions { get; set; }
    public int Acceptances { get; set; }
    public double ConversionRate { get; set; }
    public decimal Revenue { get; set; }
}

#endregion
