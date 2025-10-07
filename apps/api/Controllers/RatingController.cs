using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Hostr.Api.Data;
using Hostr.Api.Services;
using Hostr.Api.Models;
using System.ComponentModel.DataAnnotations;

namespace Hostr.Api.Controllers;

[ApiController]
[Route("api/ratings")]
public class RatingController : ControllerBase
{
    private readonly HostrDbContext _context;
    private readonly IRatingService _ratingService;
    private readonly ILogger<RatingController> _logger;

    public RatingController(
        HostrDbContext context,
        IRatingService ratingService,
        ILogger<RatingController> logger)
    {
        _context = context;
        _ratingService = ratingService;
        _logger = logger;
    }

    /// <summary>
    /// Get rating summary for the tenant
    /// </summary>
    [HttpGet("summary")]
    public async Task<ActionResult<RatingsSummary>> GetRatingsSummary(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        var tenantId = HttpContext.Items["TenantId"] as int? ?? 0;

        var summary = await _ratingService.GetRatingsSummaryAsync(tenantId, startDate, endDate);

        return Ok(summary);
    }

    /// <summary>
    /// Get all ratings for a specific department
    /// </summary>
    [HttpGet("department/{department}")]
    public async Task<ActionResult<IEnumerable<GuestRating>>> GetDepartmentRatings(
        string department,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        var tenantId = HttpContext.Items["TenantId"] as int? ?? 0;

        var query = _context.GuestRatings
            .Where(r => r.TenantId == tenantId && r.Department == department);

        if (startDate.HasValue)
            query = query.Where(r => r.CreatedAt >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(r => r.CreatedAt <= endDate.Value);

        var ratings = await query
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        return Ok(ratings);
    }

    /// <summary>
    /// Submit a rating for a task
    /// </summary>
    [HttpPost("tasks/{taskId}")]
    public async Task<ActionResult<GuestRating>> SubmitTaskRating(
        int taskId,
        [FromBody] CreateRatingRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var tenantId = HttpContext.Items["TenantId"] as int? ?? 0;
            if (tenantId == 0)
            {
                return BadRequest("Invalid tenant context");
            }

            // Verify task belongs to tenant
            var taskExists = await _context.StaffTasks
                .AnyAsync(t => t.Id == taskId && t.TenantId == tenantId);

            if (!taskExists)
            {
                return NotFound("Task not found");
            }

            var rating = await _ratingService.SaveTaskRatingAsync(
                tenantId,
                taskId,
                request.Rating,
                request.Comment);

            if (rating == null)
            {
                return BadRequest("Failed to save rating");
            }

            _logger.LogInformation("Rating {Rating} submitted for task {TaskId} by tenant {TenantId}",
                request.Rating, taskId, tenantId);

            // Return a completely simple response to avoid any circular reference issues
            var response = new
            {
                success = true,
                rating = request.Rating,
                comment = request.Comment ?? "",
                taskId = taskId,
                message = "Rating saved successfully",
                timestamp = DateTime.UtcNow
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting rating for task {TaskId}", taskId);
            return StatusCode(500, "An error occurred while saving the rating");
        }
    }

    /// <summary>
    /// Get recent ratings with details
    /// </summary>
    [HttpGet("recent")]
    public async Task<ActionResult<IEnumerable<object>>> GetRecentRatings(
        [FromQuery] int limit = 10)
    {
        var tenantId = HttpContext.Items["TenantId"] as int? ?? 0;

        var ratings = await _context.GuestRatings
            .Where(r => r.TenantId == tenantId)
            .Include(r => r.Task)
            .OrderByDescending(r => r.CreatedAt)
            .Take(limit)
            .Select(r => new
            {
                r.Id,
                r.Rating,
                r.Department,
                r.Comment,
                r.GuestName,
                r.RoomNumber,
                r.CreatedAt,
                r.CollectionMethod,
                TaskTitle = r.Task != null ? r.Task.Title : null,
                TaskId = r.TaskId
            })
            .ToListAsync();

        return Ok(ratings);
    }

    /// <summary>
    /// Create a post-stay survey for a booking
    /// </summary>
    [HttpPost("surveys/create/{bookingId}")]
    public async Task<ActionResult<PostStaySurvey>> CreateSurvey(int bookingId)
    {
        try
        {
            var survey = await _ratingService.CreatePostStaySurveyAsync(bookingId);
            await _ratingService.SendPostStaySurveyAsync(bookingId);

            return Ok(survey);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Submit a completed post-stay survey
    /// </summary>
    [HttpPost("surveys/submit/{token}")]
    public async Task<ActionResult<PostStaySurvey>> SubmitSurvey(
        string token,
        [FromBody] PostStaySurveySubmissionRequest submission)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (string.IsNullOrWhiteSpace(token))
            {
                return BadRequest("Survey token is required");
            }

            var surveySubmission = new PostStaySurveySubmission
            {
                OverallRating = submission.OverallRating,
                CleanlinessRating = submission.CleanlinessRating,
                ServiceRating = submission.ServiceRating,
                AmenitiesRating = submission.AmenitiesRating,
                ValueRating = submission.ValueRating,
                FrontDeskRating = submission.FrontDeskRating,
                HousekeepingRating = submission.HousekeepingRating,
                MaintenanceRating = submission.MaintenanceRating,
                FoodServiceRating = submission.FoodServiceRating,
                NpsScore = submission.NpsScore,
                WhatWentWell = submission.WhatWentWell,
                WhatCouldImprove = submission.WhatCouldImprove,
                AdditionalComments = submission.AdditionalComments
            };

            var survey = await _ratingService.CompletePostStaySurveyAsync(token, surveySubmission);

            if (survey == null)
            {
                return NotFound("Survey not found or already completed");
            }

            _logger.LogInformation("Post-stay survey completed with token {Token}, NPS: {NpsScore}",
                token, submission.NpsScore);

            return Ok(survey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting survey with token {Token}", token);
            return StatusCode(500, "An error occurred while saving the survey");
        }
    }

    /// <summary>
    /// Get survey completion rate
    /// </summary>
    [HttpGet("surveys/stats")]
    public async Task<ActionResult<object>> GetSurveyStats()
    {
        var tenantId = HttpContext.Items["TenantId"] as int? ?? 0;

        var totalSurveys = await _context.PostStaySurveys
            .Where(s => s.TenantId == tenantId)
            .CountAsync();

        var completedSurveys = await _context.PostStaySurveys
            .Where(s => s.TenantId == tenantId && s.IsCompleted)
            .CountAsync();

        var avgNps = await _context.PostStaySurveys
            .Where(s => s.TenantId == tenantId && s.IsCompleted)
            .Select(s => s.NpsScore)
            .DefaultIfEmpty(0)
            .AverageAsync();

        return Ok(new
        {
            totalSurveys,
            completedSurveys,
            completionRate = totalSurveys > 0 ? (double)completedSurveys / totalSurveys * 100 : 0,
            averageNpsScore = avgNps
        });
    }

    /// <summary>
    /// Trigger rating request for a specific task
    /// </summary>
    [HttpPost("request/{taskId}")]
    public async Task<IActionResult> RequestRating(int taskId)
    {
        var tenantId = HttpContext.Items["TenantId"] as int? ?? 0;

        var task = await _context.StaffTasks
            .Include(t => t.Conversation)
            .FirstOrDefaultAsync(t => t.Id == taskId && t.TenantId == tenantId);

        if (task == null || !task.ConversationId.HasValue)
        {
            return BadRequest("Task not found or no conversation associated");
        }

        await _ratingService.SendRatingRequestAsync(tenantId, task.ConversationId.Value, taskId);

        return Ok(new { message = "Rating request sent successfully" });
    }
}

