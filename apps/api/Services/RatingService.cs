using Microsoft.EntityFrameworkCore;
using Hostr.Api.Data;
using Hostr.Api.Models;
using System.Text.RegularExpressions;

namespace Hostr.Api.Services;

public interface IRatingService
{
    Task<GuestRating?> CollectRatingFromChatAsync(int tenantId, int conversationId, string messageText);
    Task<GuestRating?> SaveTaskRatingAsync(int tenantId, int taskId, int rating, string? comment = null);
    Task<PostStaySurvey> CreatePostStaySurveyAsync(int bookingId);
    Task<PostStaySurvey?> CompletePostStaySurveyAsync(string surveyToken, PostStaySurveySubmission submission);
    Task<RatingsSummary> GetRatingsSummaryAsync(int tenantId, DateTime? startDate = null, DateTime? endDate = null);
    Task SendRatingRequestAsync(int tenantId, int conversationId, int taskId);
    Task SendPostStaySurveyAsync(int bookingId);
}

public class RatingService : IRatingService
{
    private readonly HostrDbContext _context;
    private readonly IWhatsAppService _whatsAppService;
    private readonly ILogger<RatingService> _logger;
    private readonly IGuestLifecycleService _guestLifecycleService;
    private readonly IDataValidationService _dataValidationService;

    public RatingService(
        HostrDbContext context,
        IWhatsAppService whatsAppService,
        ILogger<RatingService> logger,
        IGuestLifecycleService guestLifecycleService,
        IDataValidationService dataValidationService)
    {
        _context = context;
        _whatsAppService = whatsAppService;
        _logger = logger;
        _guestLifecycleService = guestLifecycleService;
        _dataValidationService = dataValidationService;
    }

    public async Task<GuestRating?> CollectRatingFromChatAsync(int tenantId, int conversationId, string messageText)
    {
        // Check if message contains a rating
        var ratingValue = ExtractRatingFromMessage(messageText);
        if (ratingValue == null) return null;

        // Get conversation details
        var conversation = await _context.Conversations
            .Include(c => c.Messages.OrderByDescending(m => m.CreatedAt).Take(10))
            .FirstOrDefaultAsync(c => c.Id == conversationId);

        if (conversation == null) return null;

        // Check if we recently asked for a rating (within last 5 messages)
        var recentMessages = conversation.Messages.Take(5).ToList();
        var askedForRating = recentMessages.Any(m =>
            m.Direction == "Outbound" &&
            (m.Body?.Contains("rate") == true || m.Body?.Contains("⭐") == true));

        if (!askedForRating) return null;

        // Find the related task if any
        var recentTask = await _context.StaffTasks
            .Where(t => t.ConversationId == conversationId)
            .OrderByDescending(t => t.UpdatedAt)
            .FirstOrDefaultAsync();

        // Get booking info
        var booking = await _context.Bookings
            .Where(b => b.Phone == conversation.WaUserPhone)
            .OrderByDescending(b => b.CreatedAt)
            .FirstOrDefaultAsync();

        // Save the rating with proper data validation
        var rating = new GuestRating
        {
            TenantId = tenantId,
            ConversationId = conversationId,
            TaskId = recentTask?.Id,
            BookingId = booking?.Id,
            Rating = ratingValue.Value,
            Comment = _dataValidationService.SanitizeComment(ExtractCommentFromMessage(messageText)),
            Department = _dataValidationService.StandardizeDepartmentName(recentTask?.Department),
            GuestName = booking?.GuestName,
            GuestPhone = _dataValidationService.FormatPhoneNumber(conversation.WaUserPhone),
            RoomNumber = booking?.RoomNumber,
            CollectionMethod = "Chat",
            RatingType = "Service"
        };

        _context.GuestRatings.Add(rating);
        await _context.SaveChangesAsync();

        // Update guest metrics in background
        _ = Task.Run(async () =>
        {
            try
            {
                await _guestLifecycleService.UpdateGuestMetricsAsync(conversation.WaUserPhone);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating guest metrics for {Phone}", conversation.WaUserPhone);
            }
        });

        // Send thank you message
        await SendThankYouMessageAsync(tenantId, conversation.WaUserPhone, ratingValue.Value);

        return rating;
    }

    public async Task<GuestRating?> SaveTaskRatingAsync(int tenantId, int taskId, int rating, string? comment = null)
    {
        try
        {
            var task = await _context.StaffTasks
                .FirstOrDefaultAsync(t => t.Id == taskId);

            if (task == null)
            {
                _logger.LogWarning("Task {TaskId} not found for rating submission", taskId);
                return null;
            }

            // Validate rating value
            if (!_dataValidationService.IsValidRating(rating))
            {
                _logger.LogWarning("Invalid rating value {Rating} for task {TaskId}", rating, taskId);
                return null;
            }

            var guestRating = new GuestRating
            {
                TenantId = tenantId,
                TaskId = taskId,
                ConversationId = task.ConversationId,
                Rating = rating,
                Comment = _dataValidationService.SanitizeComment(comment),
                Department = _dataValidationService.StandardizeDepartmentName(task.Department),
                GuestName = task.GuestName,
                GuestPhone = _dataValidationService.FormatPhoneNumber(task.GuestPhone),
                RoomNumber = task.RoomNumber,
                CollectionMethod = "Manual",
                RatingType = "Service",
                WouldRecommend = rating >= 4 // Set to true for ratings 4-5, false otherwise
            };

            _context.GuestRatings.Add(guestRating);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Rating {Rating} saved for task {TaskId} by tenant {TenantId}",
                rating, taskId, tenantId);

            // Create a clean copy without navigation properties to avoid circular reference issues
            var cleanRating = new GuestRating
            {
                Id = guestRating.Id,
                TenantId = guestRating.TenantId,
                TaskId = guestRating.TaskId,
                ConversationId = guestRating.ConversationId,
                BookingId = guestRating.BookingId,
                Rating = guestRating.Rating,
                Comment = guestRating.Comment,
                Department = guestRating.Department,
                GuestName = guestRating.GuestName,
                GuestPhone = guestRating.GuestPhone,
                RoomNumber = guestRating.RoomNumber,
                CollectionMethod = guestRating.CollectionMethod,
                RatingType = guestRating.RatingType,
                WouldRecommend = guestRating.WouldRecommend,
                NpsScore = guestRating.NpsScore,
                CreatedAt = guestRating.CreatedAt
            };

            return cleanRating;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving rating for task {TaskId}, tenant {TenantId}", taskId, tenantId);
            return null;
        }
    }

    public async Task SendRatingRequestAsync(int tenantId, int conversationId, int taskId)
    {
        var task = await _context.StaffTasks
            .Include(t => t.Conversation)
            .FirstOrDefaultAsync(t => t.Id == taskId);

        if (task?.Conversation == null) return;

        var message = $@"Thank you for using our {task.Department} service! 🙏

We hope your request for '{task.Title}' was handled to your satisfaction.

How would you rate this service?
Please reply with a rating:

⭐⭐⭐⭐⭐ - Excellent
⭐⭐⭐⭐ - Good
⭐⭐⭐ - Average
⭐⭐ - Below Average
⭐ - Poor

Or simply reply with a number from 1 to 5.

Your feedback helps us improve our service! 💝";

        await _whatsAppService.SendTextMessageAsync(tenantId, task.Conversation.WaUserPhone, message);
    }

    public async Task<PostStaySurvey> CreatePostStaySurveyAsync(int bookingId)
    {
        var booking = await _context.Bookings
            .FirstOrDefaultAsync(b => b.Id == bookingId);

        if (booking == null)
            throw new ArgumentException("Booking not found", nameof(bookingId));

        var survey = new PostStaySurvey
        {
            TenantId = booking.TenantId,
            BookingId = bookingId,
            GuestName = booking.GuestName,
            GuestEmail = "", // Email would need to be collected separately as Booking doesn't have Email field
            GuestPhone = booking.Phone,
            SentAt = DateTime.UtcNow,
            SurveyToken = Guid.NewGuid().ToString()
        };

        _context.PostStaySurveys.Add(survey);
        await _context.SaveChangesAsync();

        return survey;
    }

    public async Task SendPostStaySurveyAsync(int bookingId)
    {
        var booking = await _context.Bookings
            .FirstOrDefaultAsync(b => b.Id == bookingId);

        if (booking == null || string.IsNullOrEmpty(booking.Phone))
            return;

        var survey = await CreatePostStaySurveyAsync(bookingId);

        var surveyLink = $"https://hotelsurvey.com/s/{survey.SurveyToken}"; // In production, use real URL

        var message = $@"Dear {booking.GuestName},

Thank you for staying with us! 🏨

We hope you had a wonderful experience. Your feedback is invaluable to us and helps us continually improve our services.

Please take 2 minutes to complete our guest satisfaction survey:
{surveyLink}

As a token of our appreciation, completing the survey enters you into our monthly draw for a free weekend stay! 🎁

Thank you for choosing us.

Warm regards,
Hotel Management Team";

        try
        {
            await _whatsAppService.SendTextMessageAsync(booking.TenantId, booking.Phone, message);

            // Mark survey as sent successfully
            survey.SentSuccessfully = true;
            survey.SentAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Post-stay survey sent successfully to {Phone} for booking {BookingId}",
                booking.Phone, bookingId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send post-stay survey to {Phone} for booking {BookingId}",
                booking.Phone, bookingId);

            // Mark survey as failed to send
            survey.SentSuccessfully = false;
            await _context.SaveChangesAsync();
        }
    }

    public async Task<PostStaySurvey?> CompletePostStaySurveyAsync(string surveyToken, PostStaySurveySubmission submission)
    {
        var survey = await _context.PostStaySurveys
            .FirstOrDefaultAsync(s => s.SurveyToken == surveyToken && !s.IsCompleted);

        if (survey == null) return null;

        // Update survey with submission data
        survey.OverallRating = submission.OverallRating;
        survey.CleanlinessRating = submission.CleanlinessRating;
        survey.ServiceRating = submission.ServiceRating;
        survey.AmenitiesRating = submission.AmenitiesRating;
        survey.ValueRating = submission.ValueRating;
        survey.FrontDeskRating = submission.FrontDeskRating;
        survey.HousekeepingRating = submission.HousekeepingRating;
        survey.MaintenanceRating = submission.MaintenanceRating;
        survey.FoodServiceRating = submission.FoodServiceRating;
        survey.NpsScore = submission.NpsScore;
        survey.WhatWentWell = submission.WhatWentWell;
        survey.WhatCouldImprove = submission.WhatCouldImprove;
        survey.AdditionalComments = submission.AdditionalComments;
        survey.IsCompleted = true;
        survey.CompletedAt = DateTime.UtcNow;

        // Also create individual department ratings for easier analysis
        await CreateDepartmentRatingsFromSurvey(survey);

        await _context.SaveChangesAsync();

        // Update guest metrics in background after survey completion
        _ = Task.Run(async () =>
        {
            try
            {
                await _guestLifecycleService.UpdateGuestMetricsAsync(survey.GuestPhone ?? "");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating guest metrics for survey completion {SurveyId}", survey.Id);
            }
        });

        return survey;
    }

    public async Task<RatingsSummary> GetRatingsSummaryAsync(int tenantId, DateTime? startDate = null, DateTime? endDate = null)
    {
        var query = _context.GuestRatings.Where(r => r.TenantId == tenantId);

        if (startDate.HasValue)
            query = query.Where(r => r.CreatedAt >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(r => r.CreatedAt <= endDate.Value);

        var ratings = await query.ToListAsync();

        var surveyQuery = _context.PostStaySurveys
            .Where(s => s.TenantId == tenantId && s.IsCompleted);

        if (startDate.HasValue)
            surveyQuery = surveyQuery.Where(s => s.CompletedAt >= startDate.Value);

        if (endDate.HasValue)
            surveyQuery = surveyQuery.Where(s => s.CompletedAt <= endDate.Value);

        var surveys = await surveyQuery.ToListAsync();

        var summary = new RatingsSummary
        {
            Period = GetPeriodDescription(startDate, endDate),
            TotalRatings = ratings.Count + surveys.Count,
            AverageRating = ratings.Any() ? ratings.Average(r => r.Rating) : 0
        };

        // Calculate rating distribution
        for (int i = 1; i <= 5; i++)
        {
            summary.RatingDistribution[i] = ratings.Count(r => r.Rating == i) +
                                           surveys.Count(s => s.OverallRating == i);
        }

        // Calculate NPS
        if (surveys.Any())
        {
            summary.PromoterCount = surveys.Count(s => s.NpsScore >= 9);
            summary.PassiveCount = surveys.Count(s => s.NpsScore >= 7 && s.NpsScore <= 8);
            summary.DetractorCount = surveys.Count(s => s.NpsScore <= 6);

            var totalNpsResponses = summary.PromoterCount + summary.PassiveCount + summary.DetractorCount;
            if (totalNpsResponses > 0)
            {
                summary.NpsScore = ((double)summary.PromoterCount / totalNpsResponses * 100) -
                                  ((double)summary.DetractorCount / totalNpsResponses * 100);
            }
        }

        // Department breakdowns
        var departments = new[] { "FrontDesk", "Housekeeping", "Maintenance", "Food & Beverage" };
        foreach (var dept in departments)
        {
            var deptRatings = ratings.Where(r => r.Department == dept).ToList();
            if (deptRatings.Any())
            {
                summary.DepartmentRatings[dept] = new DepartmentRating
                {
                    Department = dept,
                    Count = deptRatings.Count,
                    AverageRating = deptRatings.Average(r => r.Rating)
                };
            }
        }

        return summary;
    }

    private int? ExtractRatingFromMessage(string message)
    {
        // Check for star ratings (count the stars)
        var starCount = Regex.Matches(message, "⭐").Count;
        if (starCount >= 1 && starCount <= 5)
            return starCount;

        // Check for numeric ratings
        var match = Regex.Match(message, @"\b([1-5])\b");
        if (match.Success)
            return int.Parse(match.Groups[1].Value);

        // Check for word ratings
        var lowerMessage = message.ToLower();
        if (lowerMessage.Contains("excellent") || lowerMessage.Contains("perfect"))
            return 5;
        if (lowerMessage.Contains("good") || lowerMessage.Contains("great"))
            return 4;
        if (lowerMessage.Contains("average") || lowerMessage.Contains("okay") || lowerMessage.Contains("ok"))
            return 3;
        if (lowerMessage.Contains("below average") || lowerMessage.Contains("not good"))
            return 2;
        if (lowerMessage.Contains("poor") || lowerMessage.Contains("terrible") || lowerMessage.Contains("awful"))
            return 1;

        return null;
    }

    private string? ExtractCommentFromMessage(string message)
    {
        // Remove rating indicators and return the rest as comment
        var cleaned = Regex.Replace(message, @"⭐+", "").Trim();
        cleaned = Regex.Replace(cleaned, @"\b[1-5]\b", "").Trim();

        return string.IsNullOrWhiteSpace(cleaned) ? null : cleaned;
    }

    private async Task SendThankYouMessageAsync(int tenantId, string phoneNumber, int rating)
    {
        string message;

        if (rating >= 4)
        {
            message = "Thank you so much for your wonderful feedback! 😊 We're delighted to hear you had a great experience with our service.";
        }
        else if (rating == 3)
        {
            message = "Thank you for your feedback. We appreciate your honesty and will work to improve our service.";
        }
        else
        {
            message = "Thank you for your feedback. We sincerely apologize that we didn't meet your expectations. A manager will follow up with you shortly to address your concerns.";
        }

        await _whatsAppService.SendTextMessageAsync(tenantId, phoneNumber, message);
    }

    private async Task CreateDepartmentRatingsFromSurvey(PostStaySurvey survey)
    {
        var departmentRatings = new Dictionary<string, int?>
        {
            { "FrontDesk", survey.FrontDeskRating },
            { "Housekeeping", survey.HousekeepingRating },
            { "Maintenance", survey.MaintenanceRating },
            { "Food & Beverage", survey.FoodServiceRating }
        };

        foreach (var (department, rating) in departmentRatings)
        {
            if (rating.HasValue)
            {
                var guestRating = new GuestRating
                {
                    TenantId = survey.TenantId,
                    BookingId = survey.BookingId,
                    Rating = rating.Value,
                    Department = department,
                    GuestName = survey.GuestName,
                    GuestPhone = survey.GuestPhone,
                    CollectionMethod = "Survey",
                    RatingType = "Department"
                };

                _context.GuestRatings.Add(guestRating);
            }
        }
    }

    private string GetPeriodDescription(DateTime? startDate, DateTime? endDate)
    {
        if (!startDate.HasValue && !endDate.HasValue)
            return "All Time";

        if (startDate.HasValue && endDate.HasValue)
        {
            var days = (endDate.Value - startDate.Value).TotalDays;
            if (days <= 1) return "Today";
            if (days <= 7) return "This Week";
            if (days <= 30) return "This Month";
        }

        return "Custom Period";
    }
}

public class PostStaySurveySubmission
{
    public int OverallRating { get; set; }
    public int CleanlinessRating { get; set; }
    public int ServiceRating { get; set; }
    public int AmenitiesRating { get; set; }
    public int ValueRating { get; set; }
    public int? FrontDeskRating { get; set; }
    public int? HousekeepingRating { get; set; }
    public int? MaintenanceRating { get; set; }
    public int? FoodServiceRating { get; set; }
    public int NpsScore { get; set; }
    public string? WhatWentWell { get; set; }
    public string? WhatCouldImprove { get; set; }
    public string? AdditionalComments { get; set; }
}