using Microsoft.EntityFrameworkCore;
using Quartz;
using Hostr.Workers.Data;

namespace Hostr.Workers.Services;

public interface IRatingsService
{
    Task SendRatingRequestAsync(int bookingId, string guestPhone, bool useFreeform);
}

public class RatingsService : IRatingsService
{
    private readonly ILogger<RatingsService> _logger;

    public RatingsService(ILogger<RatingsService> logger)
    {
        _logger = logger;
    }

    public async Task SendRatingRequestAsync(int bookingId, string guestPhone, bool useFreeform)
    {
        try
        {
            // Simulate sending WhatsApp message
            await Task.Delay(100);
            
            var message = useFreeform 
                ? "Hi! We hope you enjoyed your stay. Could you share your feedback about your experience?"
                : "Hi! We hope you enjoyed your stay. Please rate your experience from 1-5 stars: ⭐⭐⭐⭐⭐";
            
            _logger.LogInformation("Sent rating request to {Phone} for booking {BookingId}: {Message}", 
                guestPhone, bookingId, message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending rating request to {Phone}", guestPhone);
        }
    }
}

[DisallowConcurrentExecution]
public class RatingsScheduler : IJob
{
    private readonly WorkersDbContext _context;
    private readonly IRatingsService _ratingsService;
    private readonly ILogger<RatingsScheduler> _logger;

    public RatingsScheduler(
        WorkersDbContext context,
        IRatingsService ratingsService,
        ILogger<RatingsScheduler> logger)
    {
        _context = context;
        _ratingsService = ratingsService;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("Starting ratings scheduler");

        try
        {
            // Find bookings that checked out in the last 24 hours and don't have rating requests
            var cutoffDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
            
            var bookingsToRate = await _context.Bookings
                .Where(b => b.Status == "CheckedOut" && 
                           b.CheckoutDate >= cutoffDate &&
                           !b.Ratings.Any(r => r.Status != "pending"))
                .Include(b => b.Tenant)
                .Take(20) // Process in batches
                .ToListAsync();

            foreach (var booking in bookingsToRate)
            {
                // Determine if guest is still in 24-hour session window
                var isWithin24Hours = booking.CheckoutDate >= DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-24));
                var useFreeform = isWithin24Hours;

                // Create rating record
                var rating = new Models.Rating
                {
                    TenantId = booking.TenantId,
                    BookingId = booking.Id,
                    GuestPhone = booking.Phone,
                    Source = "checkout",
                    Status = "asked",
                    AskedAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Ratings.Add(rating);

                // Send the rating request
                await _ratingsService.SendRatingRequestAsync(booking.Id, booking.Phone, useFreeform);

                _logger.LogInformation("Created rating request for booking {BookingId} (guest: {Phone})", 
                    booking.Id, booking.Phone);
            }

            // Mark expired rating requests as expired
            var expiredRatings = await _context.Ratings
                .Where(r => r.Status == "asked" && 
                           r.AskedAt.HasValue && 
                           r.AskedAt.Value < DateTime.UtcNow.AddDays(-7))
                .ToListAsync();

            foreach (var rating in expiredRatings)
            {
                rating.Status = "expired";
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Processed {NewRatings} new rating requests and expired {ExpiredRatings} requests", 
                bookingsToRate.Count, expiredRatings.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ratings scheduler");
        }
    }
}