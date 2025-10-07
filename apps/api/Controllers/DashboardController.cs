using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Hostr.Api.Data;
using Hostr.Api.Models;
using Hostr.Contracts.DTOs.Common;

namespace Hostr.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly HostrDbContext _context;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(HostrDbContext context, ILogger<DashboardController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetDashboardStats([FromQuery] DateTime? date = null)
    {
        try
        {
            var tenantId = int.Parse(HttpContext.Items["TenantId"]?.ToString() ?? "0");
            var targetDate = DateTime.SpecifyKind(date?.Date ?? DateTime.UtcNow.Date, DateTimeKind.Utc);

            _logger.LogInformation("Getting dashboard stats for tenant {TenantId} on date {Date}", tenantId, targetDate);

            // Active guests (current bookings with CheckedIn status)
            var activeGuests = await _context.Bookings
                .Where(b => b.TenantId == tenantId &&
                           b.Status == "CheckedIn" &&
                           b.CheckinDate <= DateOnly.FromDateTime(targetDate) &&
                           b.CheckoutDate >= DateOnly.FromDateTime(targetDate))
                .CountAsync();

            // Pending tasks
            var pendingTasksEndDate = DateTime.SpecifyKind(targetDate.AddDays(1), DateTimeKind.Utc);
            var pendingTasks = await _context.StaffTasks
                .Where(t => t.TenantId == tenantId && t.Status == "Open" && t.CreatedAt < pendingTasksEndDate)
                .CountAsync();

            // Today's check-ins
            var todayCheckins = await _context.Bookings
                .Where(b => b.TenantId == tenantId && b.CheckinDate == DateOnly.FromDateTime(targetDate))
                .CountAsync();

            // Today's check-outs
            var todayCheckouts = await _context.Bookings
                .Where(b => b.TenantId == tenantId && b.CheckoutDate == DateOnly.FromDateTime(targetDate))
                .CountAsync();

            // Emergency incidents (active) - count only truly active incidents
            // Only count incidents that are actively being handled
            var emergencyIncidents = await _context.EmergencyIncidents
                .Where(e => e.TenantId == tenantId && (e.Status == "Active" || e.Status == "InProgress") && e.ReportedAt.Date <= targetDate)
                .CountAsync();

            // Average response time (in minutes) - calculate from completed tasks
            var completedTasks = await _context.StaffTasks
                .Where(t => t.TenantId == tenantId &&
                           t.Status == "Completed" &&
                           t.CreatedAt.Date == targetDate &&
                           t.CompletedAt.HasValue)
                .Select(t => new {
                    CreatedAt = t.CreatedAt,
                    CompletedAt = t.CompletedAt.Value
                })
                .ToListAsync();

            double averageResponseTime = 0;
            if (completedTasks.Any())
            {
                averageResponseTime = completedTasks
                    .Average(t => (t.CompletedAt - t.CreatedAt).TotalMinutes);
            }

            // Calculate trend data (compare with previous period)
            var previousDate = DateTime.SpecifyKind(targetDate.AddDays(-7), DateTimeKind.Utc); // 1 week ago

            // Previous active guests
            var previousActiveGuests = await _context.Bookings
                .Where(b => b.TenantId == tenantId &&
                           b.Status == "CheckedIn" &&
                           b.CheckinDate <= DateOnly.FromDateTime(previousDate) &&
                           b.CheckoutDate >= DateOnly.FromDateTime(previousDate))
                .CountAsync();

            // Previous pending tasks
            var previousPendingTasks = await _context.StaffTasks
                .Where(t => t.TenantId == tenantId && t.Status == "Open" && t.CreatedAt < DateTime.SpecifyKind(previousDate.AddDays(1), DateTimeKind.Utc))
                .CountAsync();

            // Previous check-ins
            var previousCheckins = await _context.Bookings
                .Where(b => b.TenantId == tenantId && b.CheckinDate == DateOnly.FromDateTime(previousDate))
                .CountAsync();

            // Previous average response time
            var previousCompletedTasks = await _context.StaffTasks
                .Where(t => t.TenantId == tenantId &&
                           t.Status == "Completed" &&
                           t.CreatedAt.Date == previousDate &&
                           t.CompletedAt.HasValue)
                .Select(t => new {
                    CreatedAt = t.CreatedAt,
                    CompletedAt = t.CompletedAt.Value
                })
                .ToListAsync();

            double previousAverageResponseTime = 0;
            if (previousCompletedTasks.Any())
            {
                previousAverageResponseTime = previousCompletedTasks
                    .Average(t => (t.CompletedAt - t.CreatedAt).TotalMinutes);
            }

            // Calculate percentage changes
            double activeGuestsTrend = previousActiveGuests == 0 ? 0 :
                Math.Round(((double)(activeGuests - previousActiveGuests) / previousActiveGuests) * 100, 1);

            double pendingTasksTrend = previousPendingTasks == 0 ? 0 :
                Math.Round(((double)(pendingTasks - previousPendingTasks) / previousPendingTasks) * 100, 1);

            double checkinsTrend = previousCheckins == 0 ? 0 :
                Math.Round(((double)(todayCheckins - previousCheckins) / previousCheckins) * 100, 1);

            double responseTimeTrend = previousAverageResponseTime == 0 ? 0 :
                Math.Round(((averageResponseTime - previousAverageResponseTime) / previousAverageResponseTime) * 100, 1);

            var stats = new
            {
                activeGuests,
                pendingTasks,
                todayCheckins,
                todayCheckouts,
                emergencyIncidents,
                averageResponseTime = Math.Round(averageResponseTime, 1),
                trends = new
                {
                    activeGuestsTrend,
                    pendingTasksTrend,
                    checkinsTrend,
                    responseTimeTrend
                }
            };

            return Ok(new ApiResponse<object>
            {
                Success = true,
                Data = stats
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting dashboard stats");
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Error = "Error retrieving dashboard statistics"
            });
        }
    }

    [HttpGet("activities")]
    public async Task<IActionResult> GetRecentActivities([FromQuery] int limit = 10)
    {
        try
        {
            var tenantId = int.Parse(HttpContext.Items["TenantId"]?.ToString() ?? "0");

            _logger.LogInformation("Getting recent activities for tenant {TenantId}", tenantId);

            var activities = new List<object>();

            // Get recent tasks
            var recentTasks = await _context.StaffTasks
                .Where(t => t.TenantId == tenantId)
                .OrderByDescending(t => t.CreatedAt)
                .Take(limit / 2)
                .Select(t => new
                {
                    id = t.Id,
                    type = t.Status == "Completed" ? "task_completed" : "task_created",
                    title = t.Status == "Completed" ? "Task Completed" : "New Task Created",
                    description = t.Notes ?? "No description",
                    timestamp = t.CreatedAt,
                    icon = t.Status == "Completed" ? "check-circle" : "clipboard",
                    color = t.Status == "Completed" ? "success" : "primary"
                })
                .ToListAsync();

            activities.AddRange(recentTasks);

            // Get recent messages/conversations
            var recentMessages = await _context.Messages
                .Where(m => m.TenantId == tenantId && m.Direction == "Inbound") // Guest messages
                .OrderByDescending(m => m.CreatedAt)
                .Take(limit / 2)
                .Select(m => new
                {
                    id = m.Id,
                    type = "guest_message",
                    title = "Guest Message",
                    description = m.Body.Length > 50 ? m.Body.Substring(0, 50) + "..." : m.Body,
                    timestamp = m.CreatedAt,
                    icon = "message-circle",
                    color = "info"
                })
                .ToListAsync();

            activities.AddRange(recentMessages);

            // Sort by timestamp and take the most recent
            var sortedActivities = activities
                .OrderByDescending(a => ((DateTime)a.GetType().GetProperty("timestamp").GetValue(a)))
                .Take(limit)
                .ToList();

            return Ok(new ApiResponse<object>
            {
                Success = true,
                Data = sortedActivities
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting recent activities");
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Error = "Error retrieving recent activities"
            });
        }
    }

    [HttpGet("department-tasks")]
    public async Task<IActionResult> GetDepartmentTasks([FromQuery] DateTime? date = null)
    {
        try
        {
            var tenantId = int.Parse(HttpContext.Items["TenantId"]?.ToString() ?? "0");
            var targetDate = DateTime.SpecifyKind(date?.Date ?? DateTime.UtcNow.Date, DateTimeKind.Utc);

            _logger.LogInformation("Getting department tasks for tenant {TenantId} on date {Date}", tenantId, targetDate);

            // Group tasks by their request item category as department
            var startDate = DateTime.SpecifyKind(targetDate, DateTimeKind.Utc);
            var endDate = DateTime.SpecifyKind(targetDate.AddDays(1), DateTimeKind.Utc);

            var departmentTasks = await _context.StaffTasks
                .Include(t => t.RequestItem)
                .Where(t => t.TenantId == tenantId && t.CreatedAt >= startDate && t.CreatedAt < endDate)
                .GroupBy(t => t.RequestItem.Category)
                .Select(g => new
                {
                    department = g.Key,
                    count = g.Count(),
                    color = g.Key == "Housekeeping" ? "#25D466" :
                           g.Key == "Maintenance" ? "#FFC107" :
                           g.Key == "Front Desk" ? "#0DCAF0" :
                           g.Key == "Food & Beverage" ? "#0DCAF0" :
                           g.Key == "Concierge" ? "#6C757D" : "#6C757D"
                })
                .ToListAsync();

            return Ok(new ApiResponse<object>
            {
                Success = true,
                Data = departmentTasks
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting department tasks");
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Error = "Error retrieving department task distribution"
            });
        }
    }

    [HttpGet("hourly-activity")]
    public async Task<IActionResult> GetHourlyActivity([FromQuery] DateTime? date = null, [FromQuery] string period = "day")
    {
        try
        {
            var tenantId = int.Parse(HttpContext.Items["TenantId"]?.ToString() ?? "0");
            var targetDate = DateTime.SpecifyKind(date?.Date ?? DateTime.UtcNow.Date, DateTimeKind.Utc);

            _logger.LogInformation("Getting hourly activity for tenant {TenantId} on date {Date} with period {Period}", tenantId, targetDate, period);

            // Handle different periods
            if (period.ToLower() == "week")
            {
                // Get last 7 days of activity (ending with today or selected date, whichever is earlier)
                var weekData = new List<int>();
                var labels = new List<string>();
                var weekLocalNow = DateTime.Now.Date;
                var weekLocalTargetDate = targetDate.ToLocalTime().Date;

                // Use today if target date is in the future
                if (weekLocalTargetDate > weekLocalNow)
                {
                    weekLocalTargetDate = weekLocalNow;
                }

                for (int i = 6; i >= 0; i--)
                {
                    var dayDate = weekLocalTargetDate.AddDays(-i);
                    var localDayDate = dayDate;

                    // Since we're calculating from weekLocalTargetDate which is <= today,
                    // we shouldn't have future dates, but check just in case
                    if (localDayDate > weekLocalNow)
                    {
                        weekData.Add(0);
                        labels.Add(localDayDate.ToString("MMM dd"));
                        continue;
                    }

                    var utcDayDate = DateTime.SpecifyKind(localDayDate.ToUniversalTime().Date, DateTimeKind.Utc);
                    var dayStart = utcDayDate;
                    var dayEnd = utcDayDate.AddDays(1);

                    var dayCount = await _context.Messages
                        .Where(m => m.TenantId == tenantId && m.CreatedAt >= dayStart && m.CreatedAt < dayEnd)
                        .CountAsync();

                    weekData.Add(dayCount);
                    labels.Add(localDayDate.ToString("MMM dd"));
                }

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Data = new
                    {
                        data = weekData,
                        labels = labels,
                        period = "week"
                    }
                });
            }
            else if (period.ToLower() == "month")
            {
                // Get last 30 days of activity (ending with today or selected date, whichever is earlier)
                var monthData = new List<int>();
                var monthLabels = new List<string>();
                var monthLocalNow = DateTime.Now.Date;
                var monthLocalTargetDate = targetDate.ToLocalTime().Date;

                _logger.LogInformation("Backend MONTH - monthLocalNow: {Now}, monthLocalTargetDate: {Target}", monthLocalNow, monthLocalTargetDate);

                // Use today if target date is in the future
                if (monthLocalTargetDate > monthLocalNow)
                {
                    monthLocalTargetDate = monthLocalNow;
                    _logger.LogInformation("Backend MONTH - Adjusted monthLocalTargetDate to: {Target}", monthLocalTargetDate);
                }

                _logger.LogInformation("Backend MONTH - Starting loop for 30 days from: {Start}", monthLocalTargetDate);

                for (int i = 29; i >= 0; i--)
                {
                    var dayDate = monthLocalTargetDate.AddDays(-i);
                    var localDayDate = dayDate;

                    if (i == 29 || i == 0) // Log first and last iterations
                    {
                        _logger.LogInformation("Backend MONTH - Day {DayIndex}: dayDate={DayDate}, localDayDate={LocalDayDate}", i, dayDate, localDayDate);
                    }

                    // Since we're calculating from monthLocalTargetDate which is <= today,
                    // we shouldn't have future dates, but check just in case
                    if (localDayDate > monthLocalNow)
                    {
                        monthData.Add(0);
                        monthLabels.Add(localDayDate.ToString("MMM dd"));
                        continue;
                    }

                    var utcDayDate = DateTime.SpecifyKind(localDayDate.ToUniversalTime().Date, DateTimeKind.Utc);
                    var dayStart = utcDayDate;
                    var dayEnd = utcDayDate.AddDays(1);

                    var dayCount = await _context.Messages
                        .Where(m => m.TenantId == tenantId && m.CreatedAt >= dayStart && m.CreatedAt < dayEnd)
                        .CountAsync();

                    monthData.Add(dayCount);
                    monthLabels.Add(localDayDate.ToString("MMM dd"));
                }

                _logger.LogInformation("Backend MONTH - Returning {DataCount} data points, first 5 labels: {Labels}", monthData.Count, string.Join(", ", monthLabels.Take(5)));
                _logger.LogInformation("Backend MONTH - Last 5 labels: {Labels}", string.Join(", ", monthLabels.TakeLast(5)));

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Data = new
                    {
                        data = monthData,
                        labels = monthLabels,
                        period = "month"
                    }
                });
            }

            // Default: Get message activity by hour for the specified date
            var hourlyData = new int[24];
            var hourlyStartDate = DateTime.SpecifyKind(targetDate, DateTimeKind.Utc);
            var hourlyEndDate = DateTime.SpecifyKind(targetDate.AddDays(1), DateTimeKind.Utc);

            var messages = await _context.Messages
                .Where(m => m.TenantId == tenantId && m.CreatedAt >= hourlyStartDate && m.CreatedAt < hourlyEndDate)
                .Select(m => m.CreatedAt.Hour)
                .ToListAsync();

            foreach (var hour in messages)
            {
                if (hour >= 0 && hour < 24)
                {
                    hourlyData[hour]++;
                }
            }

            // If this is today's data, zero out future hours
            // Use local time for comparison since the UI displays local time
            var localNow = DateTime.Now;
            var localTargetDate = targetDate.ToLocalTime().Date;
            _logger.LogInformation("Target date: {TargetDate}, Local today: {LocalToday}, Local now: {LocalNow}",
                localTargetDate, localNow.Date, localNow);

            if (localTargetDate == localNow.Date)
            {
                var currentHour = localNow.Hour;
                _logger.LogInformation("Zeroing out future hours after current hour: {CurrentHour}", currentHour);
                for (int i = currentHour + 1; i < 24; i++)
                {
                    hourlyData[i] = 0;
                }
            }

            return Ok(new ApiResponse<object>
            {
                Success = true,
                Data = new
                {
                    data = hourlyData,
                    labels = Enumerable.Range(0, 24).Select(i => $"{i:00}:00").ToList(),
                    period = "day"
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting hourly activity");
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Error = "Error retrieving hourly activity data"
            });
        }
    }

    [HttpGet("task-completion-trend")]
    public async Task<IActionResult> GetTaskCompletionTrend([FromQuery] DateTime? endDate = null, [FromQuery] int days = 7)
    {
        try
        {
            var tenantId = int.Parse(HttpContext.Items["TenantId"]?.ToString() ?? "0");
            var end = DateTime.SpecifyKind(endDate?.Date ?? DateTime.UtcNow.Date, DateTimeKind.Utc);
            var start = DateTime.SpecifyKind(end.AddDays(-days + 1), DateTimeKind.Utc);

            _logger.LogInformation("Getting task completion trend for tenant {TenantId} from {Start} to {End}", tenantId, start, end);

            var completed = new List<int>();
            var created = new List<int>();

            for (var date = start; date <= end; date = DateTime.SpecifyKind(date.AddDays(1), DateTimeKind.Utc))
            {
                var dailyEndDate = DateTime.SpecifyKind(date.AddDays(1), DateTimeKind.Utc);

                var dailyCompleted = await _context.StaffTasks
                    .Where(t => t.TenantId == tenantId && t.Status == "Completed" && t.CompletedAt.HasValue &&
                               t.CompletedAt.Value >= date && t.CompletedAt.Value < dailyEndDate)
                    .CountAsync();

                var dailyCreated = await _context.StaffTasks
                    .Where(t => t.TenantId == tenantId && t.CreatedAt >= date && t.CreatedAt < dailyEndDate)
                    .CountAsync();

                completed.Add(dailyCompleted);
                created.Add(dailyCreated);
            }

            var trendData = new
            {
                completed,
                created
            };

            return Ok(new ApiResponse<object>
            {
                Success = true,
                Data = trendData
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting task completion trend");
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Error = "Error retrieving task completion trend"
            });
        }
    }

    [HttpGet("room-occupancy")]
    public async Task<IActionResult> GetRoomOccupancy([FromQuery] DateTime? date = null)
    {
        try
        {
            var tenantId = int.Parse(HttpContext.Items["TenantId"]?.ToString() ?? "0");
            var targetDate = DateTime.SpecifyKind(date?.Date ?? DateTime.UtcNow.Date, DateTimeKind.Utc);

            _logger.LogInformation("Getting room occupancy for tenant {TenantId} on date {Date}", tenantId, targetDate);

            // Current occupancy (CheckedIn bookings for the target date)
            var currentOccupied = await _context.Bookings
                .Where(b => b.TenantId == tenantId &&
                           b.Status == "CheckedIn" &&
                           b.CheckinDate <= DateOnly.FromDateTime(targetDate) &&
                           b.CheckoutDate >= DateOnly.FromDateTime(targetDate))
                .CountAsync();

            // Total rooms (using distinct room numbers from all bookings as proxy)
            var totalRooms = await _context.Bookings
                .Where(b => b.TenantId == tenantId && !string.IsNullOrEmpty(b.RoomNumber))
                .Select(b => b.RoomNumber)
                .Distinct()
                .CountAsync();

            // If no room data, use a default capacity
            if (totalRooms == 0) totalRooms = 50;

            var currentOccupancy = totalRooms > 0 ? (double)currentOccupied / totalRooms * 100 : 0;

            // Last month comparison
            var lastMonth = DateTime.SpecifyKind(targetDate.AddMonths(-1), DateTimeKind.Utc);
            var lastMonthOccupied = await _context.Bookings
                .Where(b => b.TenantId == tenantId &&
                           b.Status == "CheckedIn" &&
                           b.CheckinDate <= DateOnly.FromDateTime(lastMonth) &&
                           b.CheckoutDate >= DateOnly.FromDateTime(lastMonth))
                .CountAsync();

            var lastMonthOccupancy = totalRooms > 0 ? (double)lastMonthOccupied / totalRooms * 100 : 0;

            // Calculate percentage change (not absolute difference)
            double occupancyChange = lastMonthOccupancy == 0 ? 0 :
                Math.Round(((currentOccupancy - lastMonthOccupancy) / lastMonthOccupancy) * 100, 1);

            var result = new
            {
                occupancyPercentage = Math.Round(currentOccupancy, 1),
                changeFromLastMonth = occupancyChange, // Already rounded in calculation
                occupiedRooms = currentOccupied,
                totalRooms = totalRooms
            };

            return Ok(new ApiResponse<object>
            {
                Success = true,
                Data = result
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting room occupancy");
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Error = "Error retrieving room occupancy data"
            });
        }
    }

    [HttpGet("completed-tasks-summary")]
    public async Task<IActionResult> GetCompletedTasksSummary([FromQuery] DateTime? date = null)
    {
        try
        {
            var tenantId = int.Parse(HttpContext.Items["TenantId"]?.ToString() ?? "0");
            var targetDate = DateTime.SpecifyKind(date?.Date ?? DateTime.UtcNow.Date, DateTimeKind.Utc);
            var endDate = DateTime.SpecifyKind(targetDate.AddDays(1), DateTimeKind.Utc);

            _logger.LogInformation("Getting completed tasks summary for tenant {TenantId} on date {Date}", tenantId, targetDate);

            // Total completed tasks for the date
            var completedTasksCount = await _context.StaffTasks
                .Where(t => t.TenantId == tenantId &&
                           t.Status == "Completed" &&
                           t.CompletedAt.HasValue &&
                           t.CompletedAt.Value >= targetDate &&
                           t.CompletedAt.Value < endDate)
                .CountAsync();

            // Total tasks created on the target date
            var totalTasksCount = await _context.StaffTasks
                .Where(t => t.TenantId == tenantId && t.CreatedAt >= targetDate && t.CreatedAt < endDate)
                .CountAsync();

            var completionRate = totalTasksCount > 0 ? (double)completedTasksCount / totalTasksCount * 100 : 0;

            // Last month comparison for completed tasks
            var lastMonth = DateTime.SpecifyKind(targetDate.AddMonths(-1), DateTimeKind.Utc);
            var lastMonthEnd = DateTime.SpecifyKind(lastMonth.AddDays(1), DateTimeKind.Utc);

            var lastMonthCompletedCount = await _context.StaffTasks
                .Where(t => t.TenantId == tenantId &&
                           t.Status == "Completed" &&
                           t.CompletedAt.HasValue &&
                           t.CompletedAt.Value >= lastMonth &&
                           t.CompletedAt.Value < lastMonthEnd)
                .CountAsync();

            var lastMonthTotalCount = await _context.StaffTasks
                .Where(t => t.TenantId == tenantId && t.CreatedAt >= lastMonth && t.CreatedAt < lastMonthEnd)
                .CountAsync();

            var lastMonthCompletionRate = lastMonthTotalCount > 0 ? (double)lastMonthCompletedCount / lastMonthTotalCount * 100 : 0;
            var completionRateChange = completionRate - lastMonthCompletionRate;

            var result = new
            {
                completedToday = completedTasksCount,
                totalToday = totalTasksCount,
                completionRate = Math.Round(completionRate, 1),
                changeFromLastMonth = Math.Round(completionRateChange, 1)
            };

            return Ok(new ApiResponse<object>
            {
                Success = true,
                Data = result
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting completed tasks summary");
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Error = "Error retrieving completed tasks summary"
            });
        }
    }

    [HttpGet("guest-satisfaction")]
    public async Task<IActionResult> GetGuestSatisfaction([FromQuery] DateTime? startDate = null, [FromQuery] DateTime? endDate = null)
    {
        try
        {
            var tenantId = int.Parse(HttpContext.Items["TenantId"]?.ToString() ?? "0");

            // Default to last 30 days if no dates provided
            var end = DateTime.SpecifyKind(endDate?.Date ?? DateTime.UtcNow.Date, DateTimeKind.Utc);
            var start = DateTime.SpecifyKind(startDate?.Date ?? end.AddDays(-30), DateTimeKind.Utc);
            var endDateExclusive = DateTime.SpecifyKind(end.AddDays(1), DateTimeKind.Utc);

            _logger.LogInformation("Getting guest satisfaction for tenant {TenantId} from {Start} to {End}", tenantId, start, end);

            // Get ratings from the new GuestRatings table
            var ratings = await _context.GuestRatings
                .Where(r => r.TenantId == tenantId &&
                           r.CreatedAt >= start &&
                           r.CreatedAt < endDateExclusive)
                .Select(r => r.Rating)
                .ToListAsync();

            double averageRating = 0;
            int totalRatings = ratings.Count;

            if (totalRatings > 0)
            {
                averageRating = ratings.Average();
            }

            // Last month comparison for guest satisfaction
            var lastMonthEnd = DateTime.SpecifyKind(start.AddDays(-1), DateTimeKind.Utc);
            var lastMonthStart = DateTime.SpecifyKind(lastMonthEnd.AddDays(-30), DateTimeKind.Utc);
            var lastMonthEndExclusive = DateTime.SpecifyKind(lastMonthEnd.AddDays(1), DateTimeKind.Utc);

            var lastMonthRatings = await _context.GuestRatings
                .Where(r => r.TenantId == tenantId &&
                           r.CreatedAt >= lastMonthStart &&
                           r.CreatedAt < lastMonthEndExclusive)
                .Select(r => r.Rating)
                .ToListAsync();

            double lastMonthAverageRating = 0;
            if (lastMonthRatings.Count > 0)
            {
                lastMonthAverageRating = lastMonthRatings.Average();
            }

            var satisfactionChange = averageRating - lastMonthAverageRating;

            // Calculate distribution
            var ratingDistribution = new Dictionary<int, int>
            {
                { 1, 0 }, { 2, 0 }, { 3, 0 }, { 4, 0 }, { 5, 0 }
            };

            foreach (var score in ratings)
            {
                if (score >= 1 && score <= 5)
                {
                    ratingDistribution[score]++;
                }
            }

            var result = new
            {
                averageRating = Math.Round(averageRating, 1),
                totalRatings = totalRatings,
                changeFromLastMonth = Math.Round(satisfactionChange, 1),
                ratingDistribution = ratingDistribution,
                period = new
                {
                    startDate = start.ToString("yyyy-MM-dd"),
                    endDate = end.ToString("yyyy-MM-dd")
                }
            };

            return Ok(new ApiResponse<object>
            {
                Success = true,
                Data = result
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting guest satisfaction");
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Error = "Error retrieving guest satisfaction data"
            });
        }
    }

    [HttpGet("satisfaction-trend")]
    public async Task<IActionResult> GetSatisfactionTrend([FromQuery] DateTime? endDate = null)
    {
        try
        {
            var tenantId = int.Parse(HttpContext.Items["TenantId"]?.ToString() ?? "0");
            var end = DateTime.SpecifyKind(endDate?.Date ?? DateTime.UtcNow.Date, DateTimeKind.Utc);

            _logger.LogInformation("Getting satisfaction trend for tenant {TenantId} ending on date {Date}", tenantId, end);

            var satisfactionScores = new List<double>();

            // Get satisfaction scores for the last 30 days
            for (int i = 29; i >= 0; i--)
            {
                var dayDate = DateTime.SpecifyKind(end.AddDays(-i), DateTimeKind.Utc);
                var dayEnd = DateTime.SpecifyKind(dayDate.AddDays(1), DateTimeKind.Utc);

                // Get ratings from the new GuestRatings table for this day
                var dailyRatings = await _context.GuestRatings
                    .Where(r => r.TenantId == tenantId &&
                               r.CreatedAt >= dayDate &&
                               r.CreatedAt < dayEnd)
                    .Select(r => r.Rating)
                    .ToListAsync();

                // Calculate average satisfaction score for the day (convert 1-5 scale to 0-100 scale)
                double dailyScore = 0;
                if (dailyRatings.Count > 0)
                {
                    var averageRating = dailyRatings.Average();
                    // Convert 1-5 scale to 0-100 scale: (rating - 1) / 4 * 100
                    dailyScore = Math.Round((averageRating - 1) / 4 * 100, 1);
                }

                satisfactionScores.Add(dailyScore);
            }

            // Convert to array of integers as expected by frontend
            var satisfactionArray = satisfactionScores.Select(s => (int)Math.Round(s)).ToArray();

            return Ok(new ApiResponse<int[]>
            {
                Success = true,
                Data = satisfactionArray
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting satisfaction trend");
            return StatusCode(500, new ApiResponse<int[]>
            {
                Success = false,
                Error = "Error retrieving satisfaction trend data"
            });
        }
    }

    [HttpGet("immediate-actions")]
    public async Task<IActionResult> GetImmediateActions([FromQuery] DateTime? date = null)
    {
        try
        {
            var tenantId = int.Parse(HttpContext.Items["TenantId"]?.ToString() ?? "0");
            var targetDate = DateTime.SpecifyKind(date?.Date ?? DateTime.UtcNow.Date, DateTimeKind.Utc);
            var last24Hours = DateTime.SpecifyKind(targetDate.AddDays(-1), DateTimeKind.Utc);
            var last7Days = DateTime.SpecifyKind(targetDate.AddDays(-7), DateTimeKind.Utc);

            _logger.LogInformation("Getting immediate actions for tenant {TenantId} on date {Date}", tenantId, targetDate);

            var actions = new List<object>();

            // 1. Low satisfaction alerts (ratings 1-2 in last 24 hours)
            var lowRatings = await _context.GuestRatings
                .Where(r => r.TenantId == tenantId &&
                           r.CreatedAt >= last24Hours &&
                           r.Rating <= 2)
                .Select(r => new { r.Department, r.Rating, r.GuestName, r.RoomNumber, r.Comment })
                .ToListAsync();

            foreach (var rating in lowRatings)
            {
                actions.Add(new
                {
                    severity = "critical",
                    title = $"Low Satisfaction Alert - {rating.Department ?? "General"}",
                    description = $"Guest {rating.GuestName ?? "Unknown"} (Room {rating.RoomNumber ?? "N/A"}) rated {rating.Rating}/5. Comment: {rating.Comment ?? "No comment"}",
                    recommendation = "Immediate follow-up required. Contact guest to resolve issue.",
                    department = rating.Department ?? "General",
                    priority = 1
                });
            }

            // 2. Overdue tasks (open tasks older than 4 hours)
            var overdueThreshold = DateTime.SpecifyKind(targetDate.AddHours(-4), DateTimeKind.Utc);
            var overdueTasks = await _context.StaffTasks
                .Include(t => t.RequestItem)
                .Where(t => t.TenantId == tenantId &&
                           t.Status == "Open" &&
                           t.CreatedAt < overdueThreshold)
                .Select(t => new {
                    t.Id,
                    t.Notes,
                    Category = t.RequestItem.Category,
                    t.CreatedAt,
                    AgeHours = (int)(targetDate - t.CreatedAt).TotalHours
                })
                .ToListAsync();

            foreach (var task in overdueTasks.Take(5)) // Limit to top 5
            {
                actions.Add(new
                {
                    severity = task.AgeHours > 8 ? "critical" : "high",
                    title = $"Overdue Task - {task.Category}",
                    description = $"Task #{task.Id} pending for {task.AgeHours} hours. {task.Notes}",
                    recommendation = "Escalate to supervisor. Assign additional staff if needed.",
                    department = task.Category,
                    priority = task.AgeHours > 8 ? 1 : 2
                });
            }

            // 3. Department performance issues (avg satisfaction < 3.5 in last 7 days)
            var departmentPerformance = await _context.GuestRatings
                .Where(r => r.TenantId == tenantId &&
                           r.CreatedAt >= last7Days &&
                           r.Department != null)
                .GroupBy(r => r.Department)
                .Select(g => new {
                    Department = g.Key,
                    AvgRating = g.Average(r => r.Rating),
                    Count = g.Count()
                })
                .Where(d => d.AvgRating < 3.5 && d.Count >= 5) // Min 5 ratings
                .ToListAsync();

            foreach (var dept in departmentPerformance)
            {
                actions.Add(new
                {
                    severity = dept.AvgRating < 2.5 ? "critical" : "high",
                    title = $"Department Performance Alert - {dept.Department}",
                    description = $"Average rating: {dept.AvgRating:F1}/5 over last 7 days ({dept.Count} ratings)",
                    recommendation = "Schedule team meeting. Review processes and provide additional training.",
                    department = dept.Department,
                    priority = dept.AvgRating < 2.5 ? 1 : 2
                });
            }

            // 4. High task volume (more than 10 open tasks in any department)
            var taskVolume = await _context.StaffTasks
                .Include(t => t.RequestItem)
                .Where(t => t.TenantId == tenantId && t.Status == "Open")
                .GroupBy(t => t.RequestItem.Category)
                .Select(g => new {
                    Department = g.Key,
                    OpenTasks = g.Count()
                })
                .Where(d => d.OpenTasks > 10)
                .ToListAsync();

            foreach (var volume in taskVolume)
            {
                actions.Add(new
                {
                    severity = "medium",
                    title = $"High Task Volume - {volume.Department}",
                    description = $"{volume.OpenTasks} open tasks currently pending",
                    recommendation = "Consider reallocating staff resources. Prioritize urgent tasks.",
                    department = volume.Department,
                    priority = 3
                });
            }

            // Sort by priority (1 = highest)
            var sortedActions = actions
                .OrderBy(a => (int)a.GetType().GetProperty("priority")!.GetValue(a)!)
                .Take(10) // Limit to top 10 most critical
                .ToList();

            return Ok(new ApiResponse<object>
            {
                Success = true,
                Data = new
                {
                    actions = sortedActions,
                    totalActions = sortedActions.Count,
                    criticalCount = sortedActions.Count(a => ((string)a.GetType().GetProperty("severity")!.GetValue(a)!) == "critical"),
                    timestamp = targetDate
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting immediate actions");
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Error = "Error retrieving immediate actions"
            });
        }
    }
}