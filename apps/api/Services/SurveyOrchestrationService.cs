using Microsoft.EntityFrameworkCore;
using Hostr.Api.Data;
using Hostr.Api.Models;

namespace Hostr.Api.Services;

public interface ISurveyOrchestrationService
{
    Task ProcessCheckoutsForSurveysAsync();
    Task<bool> ShouldSendSurveyAsync(Booking booking);
    Task SendSurveyWithRateLimitAsync(Booking booking);
}

public class SurveyOrchestrationService : ISurveyOrchestrationService
{
    private readonly HostrDbContext _context;
    private readonly IRatingService _ratingService;
    private readonly ILogger<SurveyOrchestrationService> _logger;
    private readonly IWhatsAppRateLimiter _rateLimiter;

    public SurveyOrchestrationService(
        HostrDbContext context,
        IRatingService ratingService,
        ILogger<SurveyOrchestrationService> logger,
        IWhatsAppRateLimiter rateLimiter)
    {
        _context = context;
        _ratingService = ratingService;
        _logger = logger;
        _rateLimiter = rateLimiter;
    }

    public async Task ProcessCheckoutsForSurveysAsync()
    {
        try
        {
            _logger.LogInformation("Starting survey processing for recent checkouts");

            // Get checkouts from 2-4 hours ago that are eligible for surveys
            var eligibleCheckouts = await GetEligibleCheckoutsAsync();

            _logger.LogInformation("Found {Count} eligible checkouts for survey processing", eligibleCheckouts.Count);

            foreach (var checkout in eligibleCheckouts)
            {
                if (await ShouldSendSurveyAsync(checkout))
                {
                    await SendSurveyWithRateLimitAsync(checkout);
                }
            }

            _logger.LogInformation("Completed survey processing");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing checkouts for surveys");
        }
    }

    private async Task<List<Booking>> GetEligibleCheckoutsAsync()
    {
        var now = DateTime.UtcNow;
        var twoHoursAgo = now.AddHours(-2);
        var fourHoursAgo = now.AddHours(-4);

        return await _context.Bookings
            .Where(b =>
                // Checked out between 2-4 hours ago
                b.CheckOutDate >= fourHoursAgo &&
                b.CheckOutDate <= twoHoursAgo &&
                // Has a valid phone number
                !string.IsNullOrEmpty(b.Phone) &&
                // Not a staff booking
                !b.IsStaff &&
                // Not opted out of surveys
                !b.SurveyOptOut)
            .ToListAsync();
    }

    public async Task<bool> ShouldSendSurveyAsync(Booking booking)
    {
        try
        {
            // Skip if opt-out
            if (booking.SurveyOptOut)
            {
                _logger.LogDebug("Skipping survey for booking {BookingId} - guest opted out", booking.Id);
                return false;
            }

            // Skip if staff booking
            if (booking.IsStaff)
            {
                _logger.LogDebug("Skipping survey for booking {BookingId} - staff booking", booking.Id);
                return false;
            }

            // Skip if no phone number
            if (string.IsNullOrEmpty(booking.Phone))
            {
                _logger.LogDebug("Skipping survey for booking {BookingId} - no phone number", booking.Id);
                return false;
            }

            // Skip if already sent
            var existingSurvey = await _context.PostStaySurveys
                .AnyAsync(s => s.BookingId == booking.Id);
            if (existingSurvey)
            {
                _logger.LogDebug("Skipping survey for booking {BookingId} - survey already exists", booking.Id);
                return false;
            }

            // Skip if extended stay continuation (only send for final checkout)
            if (booking.ExtendedFromBookingId != null)
            {
                var hasContinuation = await _context.Bookings
                    .AnyAsync(b => b.ExtendedFromBookingId == booking.Id);
                if (hasContinuation)
                {
                    _logger.LogDebug("Skipping survey for booking {BookingId} - extended stay with continuation", booking.Id);
                    return false;
                }
            }

            // Check for recent surveys to same phone number (avoid spam)
            var recentSurveyToSameGuest = await _context.PostStaySurveys
                .Include(s => s.Booking)
                .Where(s => s.Booking.Phone == booking.Phone &&
                           s.SentAt >= DateTime.UtcNow.AddDays(-7))
                .AnyAsync();

            if (recentSurveyToSameGuest)
            {
                _logger.LogDebug("Skipping survey for booking {BookingId} - recent survey sent to same phone number", booking.Id);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if survey should be sent for booking {BookingId}", booking.Id);
            return false;
        }
    }

    public async Task SendSurveyWithRateLimitAsync(Booking booking)
    {
        try
        {
            // Check rate limit
            if (!await _rateLimiter.CanSendMessageAsync())
            {
                _logger.LogWarning("Rate limit exceeded, skipping survey for booking {BookingId}", booking.Id);
                return;
            }

            await _ratingService.SendPostStaySurveyAsync(booking.Id);

            _logger.LogInformation("Survey sent successfully for booking {BookingId}, guest {GuestName}",
                booking.Id, booking.GuestName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending survey for booking {BookingId}", booking.Id);
        }
    }
}

public interface IWhatsAppRateLimiter
{
    Task<bool> CanSendMessageAsync();
}

public class WhatsAppRateLimiter : IWhatsAppRateLimiter
{
    private const int MAX_MESSAGES_PER_MINUTE = 20;
    private readonly Queue<DateTime> _sentTimes = new();
    private readonly object _lock = new();

    public Task<bool> CanSendMessageAsync()
    {
        lock (_lock)
        {
            // Remove timestamps older than 1 minute
            var oneMinuteAgo = DateTime.UtcNow.AddMinutes(-1);
            while (_sentTimes.Count > 0 && _sentTimes.Peek() < oneMinuteAgo)
            {
                _sentTimes.Dequeue();
            }

            // Check if under limit
            if (_sentTimes.Count >= MAX_MESSAGES_PER_MINUTE)
            {
                return Task.FromResult(false);
            }

            _sentTimes.Enqueue(DateTime.UtcNow);
            return Task.FromResult(true);
        }
    }
}