using Microsoft.EntityFrameworkCore;
using Hostr.Api.Data;
using Hostr.Api.Models;

namespace Hostr.Api.Services;

public interface IGuestLifecycleService
{
    Task UpdateGuestMetricsAsync(string phoneNumber);
    Task UpdateAllGuestMetricsAsync();
    Task<GuestBusinessMetrics?> GetGuestMetricsAsync(string phoneNumber);
    Task MarkRepeatGuestsAsync();
}

public class GuestLifecycleService : IGuestLifecycleService
{
    private readonly HostrDbContext _context;
    private readonly ILogger<GuestLifecycleService> _logger;

    public GuestLifecycleService(HostrDbContext context, ILogger<GuestLifecycleService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task UpdateGuestMetricsAsync(string phoneNumber)
    {
        try
        {
            _logger.LogDebug("Updating guest metrics for phone {PhoneNumber}", phoneNumber);

            // Get all bookings for this phone number
            var bookings = await _context.Bookings
                .Where(b => b.Phone == phoneNumber)
                .OrderBy(b => b.CheckInDate ?? b.CreatedAt)
                .ToListAsync();

            if (!bookings.Any())
            {
                _logger.LogDebug("No bookings found for phone {PhoneNumber}", phoneNumber);
                return;
            }

            // Get all ratings for this phone number
            var ratings = await _context.GuestRatings
                .Where(r => r.GuestPhone == phoneNumber)
                .ToListAsync();

            // Get completed surveys for this phone number
            var surveys = await _context.PostStaySurveys
                .Include(s => s.Booking)
                .Where(s => s.Booking.Phone == phoneNumber && s.IsCompleted)
                .ToListAsync();

            // Get or create guest metrics record
            var tenantId = bookings.First().TenantId;
            var metrics = await _context.GuestBusinessMetrics
                .FirstOrDefaultAsync(m => m.PhoneNumber == phoneNumber && m.TenantId == tenantId);

            if (metrics == null)
            {
                metrics = new GuestBusinessMetrics
                {
                    TenantId = tenantId,
                    PhoneNumber = phoneNumber,
                    CreatedAt = DateTime.UtcNow
                };
                _context.GuestBusinessMetrics.Add(metrics);
            }

            // Update metrics
            metrics.TotalStays = bookings.Count;
            metrics.FirstStayDate = bookings.FirstOrDefault()?.CheckInDate?.ToUniversalTime() ?? bookings.First().CreatedAt;
            metrics.LastStayDate = bookings.LastOrDefault()?.CheckOutDate?.ToUniversalTime() ?? bookings.Last().CreatedAt;
            metrics.LifetimeValue = bookings.Sum(b => b.TotalRevenue ?? 0);

            // Calculate average satisfaction from both ratings and surveys
            var allRatings = new List<int>();
            allRatings.AddRange(ratings.Select(r => r.Rating));
            allRatings.AddRange(surveys.Select(s => s.OverallRating));

            if (allRatings.Any())
            {
                metrics.AverageSatisfaction = (decimal)allRatings.Average();
            }

            // Calculate days since last stay
            if (metrics.LastStayDate.HasValue)
            {
                metrics.DaysSinceLastStay = (int)(DateTime.UtcNow - metrics.LastStayDate.Value).TotalDays;
            }

            // Set will return based on latest survey NPS scores
            var latestSurvey = surveys.OrderByDescending(s => s.CompletedAt).FirstOrDefault();
            if (latestSurvey != null)
            {
                metrics.WillReturn = latestSurvey.NpsScore >= 7; // NPS score 7+ indicates likely to return
            }

            metrics.UpdatedAt = DateTime.UtcNow;

            // Mark repeat guest status on bookings
            await MarkRepeatGuestStatusAsync(phoneNumber, bookings);

            await _context.SaveChangesAsync();

            _logger.LogDebug("Updated guest metrics for {PhoneNumber}: {TotalStays} stays, ${LifetimeValue} LTV",
                phoneNumber, metrics.TotalStays, metrics.LifetimeValue);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating guest metrics for phone {PhoneNumber}", phoneNumber);
        }
    }

    public async Task UpdateAllGuestMetricsAsync()
    {
        try
        {
            _logger.LogInformation("Starting update of all guest metrics");

            // Get all unique phone numbers from bookings
            var phoneNumbers = await _context.Bookings
                .Where(b => !string.IsNullOrEmpty(b.Phone))
                .Select(b => b.Phone)
                .Distinct()
                .ToListAsync();

            _logger.LogInformation("Found {Count} unique phone numbers to process", phoneNumbers.Count);

            var processed = 0;
            foreach (var phoneNumber in phoneNumbers)
            {
                await UpdateGuestMetricsAsync(phoneNumber);
                processed++;

                if (processed % 100 == 0)
                {
                    _logger.LogInformation("Processed {Processed}/{Total} guest metrics", processed, phoneNumbers.Count);
                }
            }

            _logger.LogInformation("Completed updating all guest metrics: {Total} processed", processed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating all guest metrics");
        }
    }

    public async Task<GuestBusinessMetrics?> GetGuestMetricsAsync(string phoneNumber)
    {
        return await _context.GuestBusinessMetrics
            .FirstOrDefaultAsync(m => m.PhoneNumber == phoneNumber);
    }

    public async Task MarkRepeatGuestsAsync()
    {
        try
        {
            _logger.LogInformation("Starting repeat guest marking process");

            // Get all guests with multiple bookings
            var multipleBookingGuests = await _context.Bookings
                .GroupBy(b => b.Phone)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToListAsync();

            _logger.LogInformation("Found {Count} guests with multiple bookings", multipleBookingGuests.Count);

            foreach (var phoneNumber in multipleBookingGuests)
            {
                var bookings = await _context.Bookings
                    .Where(b => b.Phone == phoneNumber)
                    .OrderBy(b => b.CheckInDate ?? b.CreatedAt)
                    .ToListAsync();

                await MarkRepeatGuestStatusAsync(phoneNumber, bookings);
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Completed repeat guest marking process");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking repeat guests");
        }
    }

    private async Task MarkRepeatGuestStatusAsync(string phoneNumber, List<Booking> bookings)
    {
        // Mark first booking as not repeat, subsequent ones as repeat
        for (int i = 0; i < bookings.Count; i++)
        {
            bookings[i].IsRepeatGuest = i > 0;
            if (i > 0)
            {
                bookings[i].PreviousBookingId = bookings[i - 1].Id;
            }
        }
    }
}