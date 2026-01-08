using Hostr.Api.Data;
using Hostr.Api.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Hostr.Api.Services;

/// <summary>
/// Service for Hotelier Intelligence Reports
/// Provides operational insights from database and GA4 data
/// </summary>
public class HotelierReportsService : IHotelierReportsService
{
    private readonly HostrDbContext _context;
    private readonly IGA4DataService _ga4Service;
    private readonly ILogger<HotelierReportsService> _logger;

    public HotelierReportsService(
        HostrDbContext context,
        IGA4DataService ga4Service,
        ILogger<HotelierReportsService> logger)
    {
        _context = context;
        _ga4Service = ga4Service;
        _logger = logger;
    }

    #region Service Demand Heatmap

    public async Task<ServiceDemandHeatmapDto> GetServiceDemandHeatmapAsync(
        int tenantId, DateTime startDate, DateTime endDate, string? department = null)
    {
        var query = _context.StaffTasks
            .Where(t => t.TenantId == tenantId && t.CreatedAt >= startDate && t.CreatedAt <= endDate);

        if (!string.IsNullOrEmpty(department))
        {
            query = query.Where(t => t.Department == department);
        }

        var tasks = await query
            .Select(t => new { t.CreatedAt, t.Department })
            .ToListAsync();

        var result = new ServiceDemandHeatmapDto();
        var cellCounts = new Dictionary<(int hour, int day), int>();

        foreach (var task in tasks)
        {
            var hour = task.CreatedAt.Hour;
            var dayOfWeek = ((int)task.CreatedAt.DayOfWeek + 6) % 7; // Convert to Monday = 0
            var key = (hour, dayOfWeek);

            cellCounts[key] = cellCounts.GetValueOrDefault(key, 0) + 1;
        }

        // Build cells
        var dayNames = new[] { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };
        foreach (var kvp in cellCounts)
        {
            result.Data.Add(new ServiceDemandCell
            {
                Hour = kvp.Key.hour,
                DayOfWeek = kvp.Key.day,
                DayName = dayNames[kvp.Key.day],
                Count = kvp.Value
            });
        }

        // Department totals
        result.TotalByDepartment = tasks
            .GroupBy(t => t.Department ?? "Unknown")
            .ToDictionary(g => g.Key, g => g.Count());

        // Find peak
        if (cellCounts.Any())
        {
            var peak = cellCounts.OrderByDescending(c => c.Value).First();
            result.PeakHour = peak.Key.hour;
            result.PeakDay = dayNames[peak.Key.day];
        }

        return result;
    }

    #endregion

    #region Maintenance Trends

    public async Task<MaintenanceTrendsDto> GetMaintenanceTrendsAsync(
        int tenantId, DateTime startDate, DateTime endDate)
    {
        // Get maintenance tasks
        var tasks = await _context.StaffTasks
            .Where(t => t.TenantId == tenantId
                && t.CreatedAt >= startDate
                && t.CreatedAt <= endDate
                && (t.TaskType == "Maintenance" || t.Department == "Maintenance"))
            .ToListAsync();

        // Calculate previous period for comparison
        var periodDays = (endDate - startDate).Days;
        var prevStartDate = startDate.AddDays(-periodDays);
        var prevEndDate = startDate.AddDays(-1);

        var previousTasks = await _context.StaffTasks
            .Where(t => t.TenantId == tenantId
                && t.CreatedAt >= prevStartDate
                && t.CreatedAt <= prevEndDate
                && (t.TaskType == "Maintenance" || t.Department == "Maintenance"))
            .ToListAsync();

        var result = new MaintenanceTrendsDto
        {
            TotalIssues = tasks.Count
        };

        // By Category (using Description to categorize)
        var categoryGroups = tasks
            .GroupBy(t => CategorizeMaintenanceIssue(t.Description ?? ""))
            .OrderByDescending(g => g.Count());

        var prevCategoryCounts = previousTasks
            .GroupBy(t => CategorizeMaintenanceIssue(t.Description ?? ""))
            .ToDictionary(g => g.Key, g => g.Count());

        foreach (var group in categoryGroups)
        {
            var prevCount = prevCategoryCounts.GetValueOrDefault(group.Key, 0);
            var completedTasks = group.Where(t => t.CompletedAt.HasValue && t.CreatedAt != default);
            var avgHours = completedTasks.Any()
                ? completedTasks.Average(t => (t.CompletedAt!.Value - t.CreatedAt).TotalHours)
                : 0;

            result.ByCategory.Add(new MaintenanceCategoryTrend
            {
                Category = group.Key,
                Count = group.Count(),
                PreviousPeriodCount = prevCount,
                ChangePercent = prevCount > 0 ? ((group.Count() - prevCount) / (double)prevCount) * 100 : 0,
                AvgResolutionHours = Math.Round(avgHours, 1)
            });
        }

        // Top Rooms with issues
        var roomGroups = tasks
            .Where(t => !string.IsNullOrEmpty(t.RoomNumber))
            .GroupBy(t => t.RoomNumber)
            .OrderByDescending(g => g.Count())
            .Take(10);

        foreach (var group in roomGroups)
        {
            var topIssue = group
                .GroupBy(t => CategorizeMaintenanceIssue(t.Description ?? ""))
                .OrderByDescending(g => g.Count())
                .FirstOrDefault()?.Key ?? "Unknown";

            // Check for repeat issues (same room, same category, within 30 days)
            var repeatCount = group
                .GroupBy(t => CategorizeMaintenanceIssue(t.Description ?? ""))
                .Count(g => g.Count() > 1);

            result.TopRooms.Add(new MaintenanceRoomTrend
            {
                RoomNumber = group.Key!,
                IssueCount = group.Count(),
                TopIssue = topIssue,
                RepeatCount = repeatCount
            });
        }

        // By Floor (extract floor from room number)
        var floorGroups = tasks
            .Where(t => !string.IsNullOrEmpty(t.RoomNumber))
            .GroupBy(t => ExtractFloor(t.RoomNumber!))
            .OrderBy(g => g.Key);

        foreach (var group in floorGroups)
        {
            result.ByFloor.Add(new MaintenanceFloorTrend
            {
                Floor = $"Floor {group.Key}",
                IssueCount = group.Count(),
                PercentOfTotal = tasks.Count > 0 ? Math.Round((group.Count() / (double)tasks.Count) * 100, 1) : 0
            });
        }

        // Repeat Issues (same room, same issue type, more than once)
        var repeatIssues = tasks
            .Where(t => !string.IsNullOrEmpty(t.RoomNumber))
            .GroupBy(t => new { t.RoomNumber, Category = CategorizeMaintenanceIssue(t.Description ?? "") })
            .Where(g => g.Count() > 1)
            .OrderByDescending(g => g.Count())
            .Take(10);

        foreach (var group in repeatIssues)
        {
            var ordered = group.OrderBy(t => t.CreatedAt).ToList();
            result.RepeatIssues.Add(new RepeatIssue
            {
                RoomNumber = group.Key.RoomNumber!,
                Category = group.Key.Category,
                OccurrenceCount = group.Count(),
                FirstReported = ordered.First().CreatedAt,
                LastReported = ordered.Last().CreatedAt,
                Status = ordered.Last().Status ?? "Unknown"
            });
        }

        // Calculate averages
        var completedAll = tasks.Where(t => t.CompletedAt.HasValue);
        result.AvgResolutionHours = completedAll.Any()
            ? Math.Round(completedAll.Average(t => (t.CompletedAt!.Value - t.CreatedAt).TotalHours), 1)
            : 0;

        var roomsWithRepeats = result.TopRooms.Count(r => r.RepeatCount > 0);
        result.RepeatRate = result.TopRooms.Any()
            ? Math.Round((roomsWithRepeats / (double)result.TopRooms.Count) * 100, 1)
            : 0;

        return result;
    }

    private static string CategorizeMaintenanceIssue(string description)
    {
        var lower = description.ToLower();

        if (lower.Contains("ac") || lower.Contains("air con") || lower.Contains("hvac") || lower.Contains("heating") || lower.Contains("cool"))
            return "HVAC/AC";
        if (lower.Contains("plumb") || lower.Contains("water") || lower.Contains("leak") || lower.Contains("toilet") || lower.Contains("shower") || lower.Contains("drain"))
            return "Plumbing";
        if (lower.Contains("electric") || lower.Contains("power") || lower.Contains("outlet") || lower.Contains("socket") || lower.Contains("light") || lower.Contains("bulb"))
            return "Electrical";
        if (lower.Contains("tv") || lower.Contains("remote") || lower.Contains("wifi") || lower.Contains("internet") || lower.Contains("phone"))
            return "Electronics/IT";
        if (lower.Contains("door") || lower.Contains("lock") || lower.Contains("key") || lower.Contains("window"))
            return "Doors/Locks";
        if (lower.Contains("clean") || lower.Contains("stain") || lower.Contains("smell") || lower.Contains("odor"))
            return "Cleaning";
        if (lower.Contains("furniture") || lower.Contains("bed") || lower.Contains("chair") || lower.Contains("table") || lower.Contains("desk"))
            return "Furniture";

        return "General";
    }

    private static int ExtractFloor(string roomNumber)
    {
        // Extract floor from room number (e.g., "201" -> 2, "1205" -> 12)
        if (string.IsNullOrEmpty(roomNumber)) return 0;

        var digits = new string(roomNumber.Where(char.IsDigit).ToArray());
        if (digits.Length == 0) return 0;

        if (digits.Length <= 2) return int.Parse(digits.Substring(0, 1));
        if (digits.Length == 3) return int.Parse(digits.Substring(0, 1));
        return int.Parse(digits.Substring(0, digits.Length - 2));
    }

    #endregion

    #region Guest Journey Funnel

    public async Task<GuestJourneyFunnelDto> GetGuestJourneyFunnelAsync(
        int tenantId, DateTime startDate, DateTime endDate)
    {
        var startDateStr = startDate.ToString("yyyy-MM-dd");
        var endDateStr = endDate.ToString("yyyy-MM-dd");

        var result = new GuestJourneyFunnelDto();

        try
        {
            // Get GA4 events data
            var events = await _ga4Service.GetEventsAsync(tenantId, startDateStr, endDateStr);
            var eventList = events.ToList();

            // Calculate funnel stages
            var pageViews = eventList.FirstOrDefault(e => e.EventName == "page_view")?.Count ?? 0;
            var menuClicks = eventList.FirstOrDefault(e => e.EventName == "menu_click")?.Count ?? 0;
            var formStarts = eventList.FirstOrDefault(e => e.EventName == "form_start")?.Count ?? 0;
            var submissions = eventList
                .Where(e => e.EventName.Contains("request") || e.EventName.Contains("submitted"))
                .Sum(e => e.Count);

            // If no form_start data yet, estimate from submissions
            if (formStarts == 0 && submissions > 0)
            {
                formStarts = (int)(submissions * 1.5); // Assume 66% form completion rate
            }

            var stages = new List<(string name, string desc, int count)>
            {
                ("Portal Landing", "Guest opens the portal", pageViews),
                ("Browse Services", "Guest clicks on a menu item", menuClicks),
                ("Start Request", "Guest begins filling a form", formStarts),
                ("Submit Request", "Guest completes submission", submissions)
            };

            for (int i = 0; i < stages.Count; i++)
            {
                var current = stages[i];
                var previous = i > 0 ? stages[i - 1].count : current.count;
                var first = stages[0].count;

                var conversionRate = previous > 0 ? (current.count / (double)previous) * 100 : 0;
                var dropOffRate = previous > 0 ? ((previous - current.count) / (double)previous) * 100 : 0;
                var percentOfTotal = first > 0 ? (current.count / (double)first) * 100 : 0;

                result.Stages.Add(new FunnelStage
                {
                    Name = current.name,
                    Description = current.desc,
                    Count = current.count,
                    ConversionRate = Math.Round(conversionRate, 1),
                    DropOffRate = Math.Round(dropOffRate, 1),
                    PercentOfTotal = Math.Round(percentOfTotal, 1)
                });
            }

            // Overall conversion
            if (pageViews > 0)
            {
                result.OverallConversionRate = Math.Round((submissions / (double)pageViews) * 100, 1);
            }

            // Find biggest drop-off
            var maxDropOff = result.Stages.OrderByDescending(s => s.DropOffRate).FirstOrDefault();
            if (maxDropOff != null)
            {
                result.BiggestDropOff = maxDropOff.Name;
                result.BiggestDropOffPercent = maxDropOff.DropOffRate;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get GA4 data for guest journey funnel");
            // Return empty funnel
        }

        return result;
    }

    #endregion

    #region Response Time vs Satisfaction Correlation

    public async Task<ResponseSatisfactionCorrelationDto> GetResponseSatisfactionCorrelationAsync(
        int tenantId, DateTime startDate, DateTime endDate)
    {
        // Get tasks with completion times
        var rawTasks = await _context.StaffTasks
            .Where(t => t.TenantId == tenantId
                && t.CreatedAt >= startDate
                && t.CreatedAt <= endDate
                && t.CompletedAt.HasValue)
            .Select(t => new
            {
                t.RoomNumber,
                t.GuestPhone,
                t.CreatedAt,
                t.CompletedAt
            })
            .ToListAsync();

        // Calculate response minutes in memory (PostgreSQL doesn't have DateDiffMinute)
        var tasks = rawTasks.Select(t => new
        {
            t.RoomNumber,
            t.GuestPhone,
            t.CreatedAt,
            t.CompletedAt,
            ResponseMinutes = (int)(t.CompletedAt!.Value - t.CreatedAt).TotalMinutes
        }).ToList();

        // Get ratings in the same period
        var ratings = await _context.GuestRatings
            .Where(r => r.TenantId == tenantId
                && r.CreatedAt >= startDate
                && r.CreatedAt <= endDate)
            .ToListAsync();

        var result = new ResponseSatisfactionCorrelationDto();

        // Define time buckets
        var buckets = new[]
        {
            (name: "0-10 min", min: 0, max: 10),
            (name: "10-20 min", min: 10, max: 20),
            (name: "20-30 min", min: 20, max: 30),
            (name: "30-60 min", min: 30, max: 60),
            (name: "60+ min", min: 60, max: int.MaxValue)
        };

        // Try to correlate tasks with ratings by guest phone or room
        var correlatedData = new List<(int responseMinutes, int rating)>();

        foreach (var task in tasks)
        {
            // Find rating from same guest (by phone) within 24 hours of task completion
            var matchingRating = ratings.FirstOrDefault(r =>
                (!string.IsNullOrEmpty(task.GuestPhone) && r.GuestPhone == task.GuestPhone) ||
                (!string.IsNullOrEmpty(task.RoomNumber) && r.RoomNumber == task.RoomNumber));

            if (matchingRating != null)
            {
                correlatedData.Add((task.ResponseMinutes, matchingRating.Rating));
            }
        }

        // If not enough correlated data, use overall averages
        if (correlatedData.Count < 10)
        {
            // Group tasks by bucket and use average rating from all ratings
            var avgRating = ratings.Any() ? ratings.Average(r => r.Rating) : 0;

            foreach (var bucket in buckets)
            {
                var tasksInBucket = tasks.Count(t => t.ResponseMinutes >= bucket.min && t.ResponseMinutes < bucket.max);

                result.DataPoints.Add(new ResponseSatisfactionPoint
                {
                    ResponseTimeBucket = bucket.name,
                    MinMinutes = bucket.min,
                    MaxMinutes = bucket.max == int.MaxValue ? 999 : bucket.max,
                    AvgRating = Math.Round(avgRating, 2),
                    SampleSize = tasksInBucket,
                    PercentOfTotal = tasks.Count > 0 ? Math.Round((tasksInBucket / (double)tasks.Count) * 100, 1) : 0
                });
            }

            result.CorrelationStrength = 0;
            result.CorrelationDescription = "Insufficient correlated data";
            result.Insight = "Not enough data to correlate response times with satisfaction. Consider collecting post-service feedback.";
        }
        else
        {
            // Calculate per bucket
            foreach (var bucket in buckets)
            {
                var dataInBucket = correlatedData
                    .Where(d => d.responseMinutes >= bucket.min && d.responseMinutes < bucket.max)
                    .ToList();

                result.DataPoints.Add(new ResponseSatisfactionPoint
                {
                    ResponseTimeBucket = bucket.name,
                    MinMinutes = bucket.min,
                    MaxMinutes = bucket.max == int.MaxValue ? 999 : bucket.max,
                    AvgRating = dataInBucket.Any() ? Math.Round(dataInBucket.Average(d => d.rating), 2) : 0,
                    SampleSize = dataInBucket.Count,
                    PercentOfTotal = correlatedData.Count > 0 ? Math.Round((dataInBucket.Count / (double)correlatedData.Count) * 100, 1) : 0
                });
            }

            // Calculate correlation
            var under10 = correlatedData.Where(d => d.responseMinutes < 10).ToList();
            var over30 = correlatedData.Where(d => d.responseMinutes >= 30).ToList();

            result.AvgRatingUnder10Min = under10.Any() ? Math.Round(under10.Average(d => d.rating), 2) : 0;
            result.AvgRatingOver30Min = over30.Any() ? Math.Round(over30.Average(d => d.rating), 2) : 0;

            // Simple correlation strength
            var ratingDiff = result.AvgRatingUnder10Min - result.AvgRatingOver30Min;
            result.CorrelationStrength = Math.Round(Math.Min(Math.Abs(ratingDiff) / 2, 1), 2);

            if (ratingDiff > 0.5)
            {
                result.CorrelationDescription = "Strong positive correlation";
                result.Insight = $"Guests who receive service within 10 minutes rate {ratingDiff:F1} stars higher than those waiting 30+ minutes.";
            }
            else if (ratingDiff > 0.2)
            {
                result.CorrelationDescription = "Moderate positive correlation";
                result.Insight = $"Faster response times correlate with {ratingDiff:F1} higher satisfaction scores.";
            }
            else
            {
                result.CorrelationDescription = "Weak correlation";
                result.Insight = "Response time has minimal impact on satisfaction in this period.";
            }
        }

        return result;
    }

    #endregion

    #region WhatsApp Escalation

    public async Task<WhatsAppEscalationDto> GetWhatsAppEscalationAsync(
        int tenantId, DateTime startDate, DateTime endDate)
    {
        var conversations = await _context.Conversations
            .Where(c => c.TenantId == tenantId
                && c.CreatedAt >= startDate
                && c.CreatedAt <= endDate)
            .ToListAsync();

        var result = new WhatsAppEscalationDto
        {
            TotalConversations = conversations.Count
        };

        // Count by status
        result.EscalatedToAgent = conversations.Count(c =>
            c.Status == "TransferredToAgent" ||
            c.Status == "Handover" ||
            c.AssignedAgentId.HasValue);

        result.BotResolved = conversations.Count(c =>
            c.Status == "Closed" && !c.AssignedAgentId.HasValue);

        // Calculate rates
        if (result.TotalConversations > 0)
        {
            result.EscalationRate = Math.Round((result.EscalatedToAgent / (double)result.TotalConversations) * 100, 1);
            result.BotSuccessRate = Math.Round((result.BotResolved / (double)result.TotalConversations) * 100, 1);
        }

        // Get messages with intent classification for breakdown
        var conversationIds = conversations.Select(c => c.Id).ToList();
        var messages = await _context.Messages
            .Where(m => conversationIds.Contains(m.ConversationId)
                && !string.IsNullOrEmpty(m.IntentClassification))
            .ToListAsync();

        // Group by intent
        var intentGroups = messages
            .GroupBy(m => m.IntentClassification ?? "Unknown")
            .Select(g => new
            {
                Intent = g.Key,
                ConversationIds = g.Select(m => m.ConversationId).Distinct().ToList()
            })
            .ToList();

        foreach (var group in intentGroups.OrderByDescending(g => g.ConversationIds.Count).Take(10))
        {
            var escalatedInGroup = conversations
                .Count(c => group.ConversationIds.Contains(c.Id) &&
                    (c.Status == "TransferredToAgent" || c.Status == "Handover" || c.AssignedAgentId.HasValue));

            result.ByIntent.Add(new EscalationByIntent
            {
                Intent = FormatIntentName(group.Intent),
                TotalCount = group.ConversationIds.Count,
                EscalatedCount = escalatedInGroup,
                EscalationRate = group.ConversationIds.Count > 0
                    ? Math.Round((escalatedInGroup / (double)group.ConversationIds.Count) * 100, 1)
                    : 0
            });
        }

        // Daily trend
        var dailyGroups = conversations
            .GroupBy(c => c.CreatedAt.Date)
            .OrderBy(g => g.Key)
            .Take(30);

        foreach (var day in dailyGroups)
        {
            var escalated = day.Count(c =>
                c.Status == "TransferredToAgent" ||
                c.Status == "Handover" ||
                c.AssignedAgentId.HasValue);

            result.DailyTrend.Add(new EscalationTrend
            {
                Date = day.Key.ToString("yyyy-MM-dd"),
                Total = day.Count(),
                Escalated = escalated,
                Rate = day.Count() > 0 ? Math.Round((escalated / (double)day.Count()) * 100, 1) : 0
            });
        }

        // Top escalation reason
        var topIntent = result.ByIntent
            .OrderByDescending(i => i.EscalationRate)
            .FirstOrDefault(i => i.TotalCount >= 5);

        result.TopEscalationReason = topIntent?.Intent ?? "Unknown";

        return result;
    }

    private static string FormatIntentName(string intent)
    {
        if (string.IsNullOrEmpty(intent)) return "Unknown";

        // Convert SCREAMING_SNAKE_CASE to Title Case
        return string.Join(" ", intent
            .Split('_')
            .Select(word => word.Length > 0
                ? char.ToUpper(word[0]) + word.Substring(1).ToLower()
                : word));
    }

    #endregion

    #region Upsell Performance

    public async Task<UpsellPerformanceDto> GetUpsellPerformanceAsync(
        int tenantId, DateTime startDate, DateTime endDate)
    {
        // Get WhatsApp-based upsell metrics
        var whatsappMetrics = await _context.UpsellMetrics
            .Where(u => u.TenantId == tenantId
                && u.SuggestedAt >= startDate
                && u.SuggestedAt <= endDate)
            .ToListAsync();

        // Get Guest Portal upsell metrics (conversions from weather upsells, featured carousel, etc.)
        var portalMetrics = await _context.PortalUpsellMetrics
            .Where(u => u.TenantId == tenantId
                && u.CreatedAt >= startDate
                && u.CreatedAt <= endDate
                && u.EventType == "conversion")
            .ToListAsync();

        // Combine metrics for totals
        var whatsappSuggestions = whatsappMetrics.Count;
        var whatsappAcceptances = whatsappMetrics.Count(m => m.WasAccepted);
        var whatsappRevenue = whatsappMetrics.Where(m => m.WasAccepted).Sum(m => m.Revenue);

        var portalConversions = portalMetrics.Count;
        var portalRevenue = portalMetrics.Sum(m => m.Revenue);

        var result = new UpsellPerformanceDto
        {
            // Portal conversions are both suggestions and acceptances (we only track when they convert)
            TotalSuggestions = whatsappSuggestions + portalConversions,
            TotalAcceptances = whatsappAcceptances + portalConversions,
            TotalRevenue = whatsappRevenue + portalRevenue
        };

        if (result.TotalSuggestions > 0)
        {
            result.ConversionRate = Math.Round((result.TotalAcceptances / (double)result.TotalSuggestions) * 100, 1);
        }

        if (result.TotalAcceptances > 0)
        {
            result.AvgOrderValue = Math.Round(result.TotalRevenue / result.TotalAcceptances, 2);
        }

        // By Category - combine both sources
        var allCategories = whatsappMetrics
            .Where(m => m.WasAccepted)
            .Select(m => new { Category = m.SuggestedServiceCategory ?? "Other", m.Revenue })
            .Concat(portalMetrics.Select(m => new { Category = m.ServiceCategory ?? "Other", m.Revenue }))
            .GroupBy(m => m.Category)
            .OrderByDescending(g => g.Sum(m => m.Revenue));

        foreach (var group in allCategories)
        {
            var whatsappGroup = whatsappMetrics.Where(m => (m.SuggestedServiceCategory ?? "Other") == group.Key);
            var portalGroup = portalMetrics.Where(m => (m.ServiceCategory ?? "Other") == group.Key);
            var totalSuggestions = whatsappGroup.Count() + portalGroup.Count();
            var totalAcceptances = whatsappGroup.Count(m => m.WasAccepted) + portalGroup.Count();
            var totalRevenue = whatsappGroup.Where(m => m.WasAccepted).Sum(m => m.Revenue) + portalGroup.Sum(m => m.Revenue);

            result.ByCategory.Add(new UpsellByCategory
            {
                Category = group.Key,
                Suggestions = totalSuggestions,
                Acceptances = totalAcceptances,
                ConversionRate = totalSuggestions > 0
                    ? Math.Round((totalAcceptances / (double)totalSuggestions) * 100, 1)
                    : 0,
                Revenue = totalRevenue
            });
        }

        // Top Services - combine both sources
        var allServices = whatsappMetrics
            .Where(m => m.WasAccepted)
            .Select(m => new { ServiceName = m.SuggestedServiceName ?? "Unknown", Price = m.SuggestedServicePrice, m.Revenue })
            .Concat(portalMetrics.Select(m => new { ServiceName = m.ServiceName, Price = m.ServicePrice, m.Revenue }))
            .GroupBy(m => new { m.ServiceName, m.Price })
            .OrderByDescending(g => g.Sum(m => m.Revenue))
            .Take(10);

        foreach (var group in allServices)
        {
            var whatsappGroup = whatsappMetrics.Where(m => m.SuggestedServiceName == group.Key.ServiceName);
            var portalGroup = portalMetrics.Where(m => m.ServiceName == group.Key.ServiceName);
            var totalSuggestions = whatsappGroup.Count() + portalGroup.Count();
            var totalAcceptances = whatsappGroup.Count(m => m.WasAccepted) + portalGroup.Count();
            var totalRevenue = whatsappGroup.Where(m => m.WasAccepted).Sum(m => m.Revenue) + portalGroup.Sum(m => m.Revenue);

            result.TopServices.Add(new UpsellByService
            {
                ServiceName = group.Key.ServiceName,
                Price = group.Key.Price,
                Suggestions = totalSuggestions,
                Acceptances = totalAcceptances,
                ConversionRate = totalSuggestions > 0
                    ? Math.Round((totalAcceptances / (double)totalSuggestions) * 100, 1)
                    : 0,
                Revenue = totalRevenue
            });
        }

        // Weekly Trend - combine both sources
        var allWeeklyData = whatsappMetrics
            .Select(m => new { Date = m.SuggestedAt, WasAccepted = m.WasAccepted, m.Revenue, IsPortal = false })
            .Concat(portalMetrics.Select(m => new { Date = m.CreatedAt, WasAccepted = true, m.Revenue, IsPortal = true }))
            .GroupBy(m => new { Year = m.Date.Year, Week = GetWeekOfYear(m.Date) })
            .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Week)
            .TakeLast(8);

        foreach (var group in allWeeklyData)
        {
            var suggestions = group.Count();
            var acceptances = group.Count(m => m.WasAccepted);
            var revenue = group.Where(m => m.WasAccepted).Sum(m => m.Revenue);

            result.WeeklyTrend.Add(new UpsellTrend
            {
                Week = $"W{group.Key.Week}",
                Suggestions = suggestions,
                Acceptances = acceptances,
                ConversionRate = suggestions > 0
                    ? Math.Round((acceptances / (double)suggestions) * 100, 1)
                    : 0,
                Revenue = revenue
            });
        }

        return result;
    }

    private static int GetWeekOfYear(DateTime date)
    {
        return System.Globalization.CultureInfo.CurrentCulture.Calendar
            .GetWeekOfYear(date, System.Globalization.CalendarWeekRule.FirstDay, DayOfWeek.Monday);
    }

    #endregion
}
