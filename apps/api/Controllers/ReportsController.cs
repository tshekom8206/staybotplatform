using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Hostr.Api.Data;

namespace Hostr.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReportsController : ControllerBase
{
    private readonly HostrDbContext _context;
    private readonly ILogger<ReportsController> _logger;

    public ReportsController(HostrDbContext context, ILogger<ReportsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet("tasks")]
    public async Task<ActionResult<object>> GetTaskPerformanceReport()
    {
        try
        {
            var tenantIdString = HttpContext.Items["TenantId"]?.ToString();
            if (string.IsNullOrEmpty(tenantIdString) || !int.TryParse(tenantIdString, out var tenantId))
            {
                _logger.LogWarning("Invalid tenant ID in request context: {TenantId}", tenantIdString);
                return BadRequest("Invalid tenant context");
            }

        // REAL DATA: Task Performance Metrics
        var totalTasks = await _context.StaffTasks
            .Where(t => t.TenantId == tenantId)
            .CountAsync();

        var completedTasks = await _context.StaffTasks
            .Where(t => t.TenantId == tenantId && t.Status == "Completed")
            .CountAsync();

        var pendingTasks = await _context.StaffTasks
            .Where(t => t.TenantId == tenantId && t.Status == "Open")
            .CountAsync();

        var inProgressTasks = await _context.StaffTasks
            .Where(t => t.TenantId == tenantId && t.Status == "InProgress")
            .CountAsync();

        var overdueTasks = await _context.StaffTasks
            .Where(t => t.TenantId == tenantId &&
                   t.EstimatedCompletionTime.HasValue &&
                   t.EstimatedCompletionTime < DateTime.UtcNow &&
                   t.Status != "Completed")
            .CountAsync();

        var completionRate = totalTasks > 0 ? (double)completedTasks / totalTasks * 100 : 0.0;

        // REAL DATA: Tasks by Department
        var tasksByDepartment = await _context.StaffTasks
            .Where(t => t.TenantId == tenantId)
            .GroupBy(t => t.Department)
            .Select(g => new { Department = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Department, x => x.Count);

        // REAL DATA: Tasks by Priority
        var tasksByPriority = await _context.StaffTasks
            .Where(t => t.TenantId == tenantId)
            .GroupBy(t => t.Priority)
            .Select(g => new { Priority = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Priority, x => x.Count);

        // REAL DATA: Tasks by Status Over Time (last 30 days)
        var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
        var tasksByDay = await _context.StaffTasks
            .Where(t => t.TenantId == tenantId && t.CreatedAt >= thirtyDaysAgo)
            .GroupBy(t => t.CreatedAt.Date)
            .Select(g => new { Date = g.Key.ToString("yyyy-MM-dd"), Count = g.Count() })
            .ToDictionaryAsync(x => x.Date, x => x.Count);

        // REAL DATA: Average Completion Time
        var completionTimeTasks = await _context.StaffTasks
            .Where(t => t.TenantId == tenantId &&
                   t.Status == "Completed" &&
                   t.CompletedAt.HasValue)
            .Select(t => new { CreatedAt = t.CreatedAt, CompletedAt = t.CompletedAt!.Value })
            .ToListAsync();

        var completionTimes = completionTimeTasks.Select(t => (t.CompletedAt - t.CreatedAt).TotalHours);
        var averageCompletionHours = completionTimes.Any() ? completionTimes.Average() : 2.0; // Default 2 hours

        // REAL DATA: Top Performing Staff
        var topPerformers = await _context.StaffTasks
            .Where(t => t.TenantId == tenantId &&
                   t.Status == "Completed" &&
                   t.CompletedBy.HasValue)
            .Include(t => t.CompletedByUser)
            .GroupBy(t => new { t.CompletedBy, t.CompletedByUser!.Email })
            .Select(g => new {
                userId = g.Key.CompletedBy,
                email = g.Key.Email,
                completedCount = g.Count()
            })
            .OrderByDescending(x => x.completedCount)
            .Take(10)
            .ToListAsync();

        var report = new
        {
            // Summary Statistics
            totalTasks,
            completedTasks,
            pendingTasks,
            inProgressTasks,
            overdueTasks,
            completionRate,
            averageCompletionHours,

            // Breakdown Analysis
            tasksByDepartment,
            tasksByPriority,
            tasksByDay,
            topPerformers
        };

        // Prevent caching to ensure fresh data
        Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
        Response.Headers["Pragma"] = "no-cache";
        Response.Headers["Expires"] = "0";

        return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating task performance report for tenant {TenantId}",
                HttpContext.Items["TenantId"]?.ToString());
            return StatusCode(500, "An error occurred while generating the task performance report");
        }
    }

    [HttpGet("satisfaction")]
    public async Task<ActionResult<object>> GetGuestSatisfactionReport()
    {
        try
        {
            var tenantIdString = HttpContext.Items["TenantId"]?.ToString();
            if (string.IsNullOrEmpty(tenantIdString) || !int.TryParse(tenantIdString, out var tenantId))
            {
                _logger.LogWarning("Invalid tenant ID in request context: {TenantId}", tenantIdString);
                return BadRequest("Invalid tenant context");
            }

        // REAL DATA: Guest Satisfaction Metrics from new GuestRatings table
        var ratings = await _context.GuestRatings
            .Where(r => r.TenantId == tenantId)
            .ToListAsync();

        var surveys = await _context.PostStaySurveys
            .Where(s => s.TenantId == tenantId && s.IsCompleted)
            .ToListAsync();

        // DEBUG LOGGING
        _logger.LogInformation("=== SATISFACTION REPORT DEBUG ===");
        _logger.LogInformation("TenantId: {TenantId}", tenantId);
        _logger.LogInformation("GuestRatings count: {RatingsCount}", ratings.Count);
        _logger.LogInformation("PostStaySurveys count: {SurveysCount}", surveys.Count);

        if (ratings.Any())
        {
            _logger.LogInformation("Sample rating: {@SampleRating}", ratings.First());
        }
        else
        {
            _logger.LogInformation("No ratings found in GuestRatings table for tenant {TenantId}", tenantId);
        }

        var totalRatings = ratings.Count + surveys.Count;
        var averageRating = totalRatings > 0 ?
            ratings.Select(r => (double)r.Rating).Concat(surveys.Select(s => (double)s.OverallRating)).Average() : 0.0;

        // REAL DATA: Ratings Distribution
        var allRatingValues = ratings.Select(r => r.Rating).Concat(surveys.Select(s => s.OverallRating)).ToList();
        var ratingsDistribution = new Dictionary<string, int>
        {
            {"1", allRatingValues.Count(r => r == 1)},
            {"2", allRatingValues.Count(r => r == 2)},
            {"3", allRatingValues.Count(r => r == 3)},
            {"4", allRatingValues.Count(r => r == 4)},
            {"5", allRatingValues.Count(r => r == 5)}
        };

        // REAL DATA: Ratings by Source (Collection Method)
        var ratingsBySourceTemp = new[]
        {
            new { source = "Chat", count = ratings.Count(r => r.CollectionMethod == "Chat"), avgScore = ratings.Where(r => r.CollectionMethod == "Chat").Any() ? ratings.Where(r => r.CollectionMethod == "Chat").Average(r => (double)r.Rating) : 0.0 },
            new { source = "Survey", count = ratings.Count(r => r.CollectionMethod == "Survey"), avgScore = ratings.Where(r => r.CollectionMethod == "Survey").Any() ? ratings.Where(r => r.CollectionMethod == "Survey").Average(r => (double)r.Rating) : 0.0 },
            new { source = "Manual", count = ratings.Count(r => r.CollectionMethod == "Manual"), avgScore = ratings.Where(r => r.CollectionMethod == "Manual").Any() ? ratings.Where(r => r.CollectionMethod == "Manual").Average(r => (double)r.Rating) : 0.0 }
        };
        var ratingsBySource = ratingsBySourceTemp.Where(x => x.count > 0).ToList();

        // REAL DATA: Ratings Over Time (last 7 days)
        var ratingsByDay = new Dictionary<string, object>();
        for (int i = 6; i >= 0; i--)
        {
            var date = DateTime.UtcNow.Date.AddDays(-i);
            var dayRatings = ratings.Where(r => r.CreatedAt.Date == date).ToList();
            var daySurveys = surveys.Where(s => s.CompletedAt?.Date == date).ToList();

            var dayCount = dayRatings.Count + daySurveys.Count;
            var dayAvgScore = dayCount > 0 ?
                dayRatings.Select(r => (double)r.Rating).Concat(daySurveys.Select(s => (double)s.OverallRating)).Average() : 0.0;

            ratingsByDay[date.ToString("yyyy-MM-dd")] = new { count = dayCount, avgScore = Math.Round(dayAvgScore, 1) };
        }

        // REAL DATA: Recent Guest Feedback (all ratings, not just those with comments)
        var recentFeedback = ratings
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new {
                score = r.Rating,
                comment = string.IsNullOrEmpty(r.Comment) ? $"{r.Rating} star rating" : r.Comment,
                source = r.CollectionMethod,
                guestPhone = r.GuestPhone,
                guestName = r.GuestName,
                roomNumber = r.RoomNumber,
                receivedAt = r.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ssZ")
            })
            .Take(20)
            .ToList();

        // REAL DATA: Response Rate (surveys completed / surveys sent)
        // Count total surveys sent
        var surveysSent = await _context.PostStaySurveys
            .Where(s => s.TenantId == tenantId)
            .CountAsync();

        // Count completed surveys
        var surveysCompleted = await _context.PostStaySurveys
            .Where(s => s.TenantId == tenantId && s.IsCompleted)
            .CountAsync();

        // Calculate actual response rate based on survey completion
        var responseRate = surveysSent > 0 ? (double)surveysCompleted / surveysSent * 100 : 0.0;

        // REAL DATA: Satisfaction by Department (from GuestRatings.Department)
        var satisfactionByBookingSource = ratings
            .Where(r => !string.IsNullOrEmpty(r.Department))
            .GroupBy(r => r.Department)
            .Select(g => new {
                source = g.Key,
                count = g.Count(),
                avgScore = g.Average(r => (double)r.Rating)
            })
            .ToList();

        var report = new
        {
            // Summary Statistics
            totalRatings,
            averageRating = Math.Round(averageRating, 1),
            responseRate = Math.Round(responseRate, 1),

            // Distribution Analysis
            ratingsDistribution,
            ratingsBySource,
            ratingsByDay,
            satisfactionByBookingSource,

            // Qualitative Data
            recentFeedback
        };

        // DEBUG LOGGING
        _logger.LogInformation("Report created with {TotalRatings} total ratings, {FeedbackCount} recent feedback items",
            totalRatings, recentFeedback.Count);

        if (recentFeedback.Any())
        {
            _logger.LogInformation("Sample feedback: {@SampleFeedback}", recentFeedback.First());
        }

        // Prevent caching to ensure fresh data
        Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
        Response.Headers["Pragma"] = "no-cache";
        Response.Headers["Expires"] = "0";

        return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating guest satisfaction report for tenant {TenantId}",
                HttpContext.Items["TenantId"]?.ToString());
            return StatusCode(500, "An error occurred while generating the satisfaction report");
        }
    }

    [HttpGet("usage")]
    public async Task<ActionResult<object>> GetServiceUsageReport()
    {
        try
        {
            var tenantIdString = HttpContext.Items["TenantId"]?.ToString();
            if (string.IsNullOrEmpty(tenantIdString) || !int.TryParse(tenantIdString, out var tenantId))
            {
                _logger.LogWarning("Invalid tenant ID in request context: {TenantId}", tenantIdString);
                return BadRequest("Invalid tenant context");
            }

        // REAL DATA: Service Usage from Staff Tasks
        var totalServiceRequests = await _context.StaffTasks
            .Where(t => t.TenantId == tenantId && t.RequestItemId.HasValue)
            .CountAsync();

        // DEBUG LOGGING
        _logger.LogInformation("=== SERVICE USAGE REPORT DEBUG ===");
        _logger.LogInformation("TenantId: {TenantId}", tenantId);
        _logger.LogInformation("Total service requests with RequestItemId: {TotalServiceRequests}", totalServiceRequests);

        // REAL DATA: All Services (for calculating accurate department totals)
        var allServices = await _context.StaffTasks
            .Where(t => t.TenantId == tenantId && t.RequestItemId.HasValue)
            .Include(t => t.RequestItem)
            .GroupBy(t => new { t.RequestItemId, t.RequestItem!.Name, t.RequestItem.Category })
            .Select(g => new {
                serviceId = g.Key.RequestItemId,
                serviceName = g.Key.Name,
                category = g.Key.Category,
                requestCount = g.Count(),
                completedCount = g.Count(x => x.Status == "Completed")
            })
            .OrderByDescending(x => x.requestCount)
            .ToListAsync();

        // REAL DATA: Top Requested Services (for UI display - limited to top 20)
        var topRequestedServices = allServices.Take(20).ToList();

        // REAL DATA: Service Usage by Department (calculated from allServices to ensure alignment)
        // First get department mapping from the staff tasks
        var departmentMapping = await _context.StaffTasks
            .Where(t => t.TenantId == tenantId && t.RequestItemId.HasValue)
            .GroupBy(t => new { t.Department, t.RequestItemId })
            .Select(g => new {
                Department = g.Key.Department,
                RequestItemId = g.Key.RequestItemId,
                TaskCount = g.Count(),
                CompletedCount = g.Count(x => x.Status == "Completed")
            })
            .ToListAsync();

        // Now aggregate by department to match the service totals
        var usageByDepartment = departmentMapping
            .GroupBy(d => d.Department)
            .Select(g => new {
                Department = g.Key,
                RequestCount = g.Sum(x => x.TaskCount),
                CompletedCount = g.Sum(x => x.CompletedCount)
            })
            .ToList();


        // REAL DATA: Service Usage Over Time (last 30 days)
        var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
        var usageByDay = await _context.StaffTasks
            .Where(t => t.TenantId == tenantId &&
                   t.RequestItemId.HasValue &&
                   t.CreatedAt >= thirtyDaysAgo)
            .GroupBy(t => t.CreatedAt.Date)
            .Select(g => new {
                Date = g.Key.ToString("yyyy-MM-dd"),
                Count = g.Count()
            })
            .ToDictionaryAsync(x => x.Date, x => x.Count);

        // REAL DATA: Service Completion Times by Category
        var completionTimesByCategory = await _context.StaffTasks
            .Where(t => t.TenantId == tenantId &&
                   t.RequestItemId.HasValue &&
                   t.Status == "Completed" &&
                   t.CompletedAt.HasValue)
            .Include(t => t.RequestItem)
            .Select(t => new {
                Category = t.RequestItem!.Category,
                CompletionTime = (t.CompletedAt!.Value - t.CreatedAt).TotalMinutes
            })
            .ToListAsync();

        var avgCompletionByCategory = completionTimesByCategory
            .GroupBy(x => x.Category)
            .Select(g => new {
                Category = g.Key,
                AvgCompletionMinutes = g.Average(x => x.CompletionTime)
            })
            .ToDictionary(x => x.Category, x => x.AvgCompletionMinutes);

        // REAL DATA: Peak Usage Hours
        var usageByHour = await _context.StaffTasks
            .Where(t => t.TenantId == tenantId && t.RequestItemId.HasValue)
            .GroupBy(t => t.CreatedAt.Hour)
            .Select(g => new { Hour = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Hour.ToString(), x => x.Count);

        // REAL DATA: Guest Interaction Volume
        var totalGuestRequests = await _context.Messages
            .Where(m => m.TenantId == tenantId && m.Direction == "Inbound")
            .CountAsync();

        var serviceRequestRate = totalGuestRequests > 0 ?
            (double)totalServiceRequests / totalGuestRequests * 100 : 0.0;

        var report = new
        {
            // Summary Statistics
            totalServiceRequests,
            totalGuestRequests,
            serviceRequestRate,

            // Service Analysis
            topRequestedServices,
            usageByDepartment,
            avgCompletionByCategory,

            // Temporal Analysis
            usageByDay,
            usageByHour
        };

        // DEBUG LOGGING
        _logger.LogInformation("Service usage report created with {TotalServiceRequests} service requests, {TopServicesCount} top services",
            totalServiceRequests, topRequestedServices.Count);

        if (topRequestedServices.Any())
        {
            _logger.LogInformation("Top requested service: {@TopService}", topRequestedServices.First());
        }

        // Prevent caching to ensure fresh data
        Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
        Response.Headers["Pragma"] = "no-cache";
        Response.Headers["Expires"] = "0";

        return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating service usage report for tenant {TenantId}",
                HttpContext.Items["TenantId"]?.ToString());
            return StatusCode(500, "An error occurred while generating the service usage report");
        }
    }
}