using Microsoft.EntityFrameworkCore;
using Hostr.Api.Data;
using Hostr.Api.Models;
using System.Diagnostics;

namespace Hostr.Api.Services;

public class BookingStatusUpdateService : IBookingStatusUpdateService
{
    private readonly HostrDbContext _context;
    private readonly ILogger<BookingStatusUpdateService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private static BookingStatusUpdateResult? _lastResult;

    public BookingStatusUpdateService(
        HostrDbContext context,
        ILogger<BookingStatusUpdateService> logger,
        IServiceProvider serviceProvider)
    {
        _context = context;
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public async Task UpdateBookingStatusesAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new BookingStatusUpdateResult
        {
            ExecutedAt = DateTime.UtcNow
        };

        try
        {
            _logger.LogInformation("Starting automated booking status update process");

            // Process pending check-ins (Confirmed -> CheckedIn)
            result.CheckinsProcessed = await ProcessPendingCheckinsAsync();

            // Process pending check-outs (CheckedIn -> CheckedOut)
            result.CheckoutsProcessed = await ProcessPendingCheckoutsAsync();

            _logger.LogInformation("Booking status update completed: {CheckinsProcessed} check-ins, {CheckoutsProcessed} check-outs processed",
                result.CheckinsProcessed, result.CheckoutsProcessed);
        }
        catch (Exception ex)
        {
            result.ErrorsEncountered = 1;
            result.LastError = ex.Message;
            _logger.LogError(ex, "Error during automated booking status update");
        }
        finally
        {
            stopwatch.Stop();
            result.ExecutionDuration = stopwatch.Elapsed;
            _lastResult = result;
        }
    }

    public async Task<int> ProcessPendingCheckinsAsync()
    {
        try
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var currentTime = DateTime.UtcNow;

            // Find confirmed bookings that should be checked in
            // Check-in is allowed from 2 PM on check-in date
            var checkinTime = today.ToDateTime(new TimeOnly(14, 0)); // 2 PM

            var pendingCheckins = await _context.Bookings
                .Where(b => b.Status == "Confirmed" &&
                           b.CheckinDate == today &&
                           currentTime >= checkinTime)
                .ToListAsync();

            if (!pendingCheckins.Any())
            {
                _logger.LogDebug("No bookings ready for automatic check-in");
                return 0;
            }

            _logger.LogInformation("Processing {Count} bookings for automatic check-in", pendingCheckins.Count);

            var checkedInCount = 0;
            foreach (var booking in pendingCheckins)
            {
                try
                {
                    await LogBookingChangeAsync(booking, "Confirmed", "CheckedIn", "Automatic check-in at 2 PM");
                    booking.Status = "CheckedIn";
                    checkedInCount++;

                    _logger.LogDebug("Auto-checked in booking {BookingId} for guest {GuestName}",
                        booking.Id, booking.GuestName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to auto-check in booking {BookingId}", booking.Id);
                }
            }

            await _context.SaveChangesAsync();

            if (checkedInCount > 0)
            {
                _logger.LogInformation("Successfully auto-checked in {Count} bookings", checkedInCount);
            }

            return checkedInCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing pending check-ins");
            throw;
        }
    }

    public async Task<int> ProcessPendingCheckoutsAsync()
    {
        try
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var currentTime = DateTime.UtcNow;

            // Find checked-in bookings that should be checked out
            // Check-out happens at 12 PM (noon) on checkout date
            var checkoutTime = today.ToDateTime(new TimeOnly(12, 0)); // 12 PM

            var pendingCheckouts = await _context.Bookings
                .Where(b => b.Status == "CheckedIn" &&
                           ((b.CheckoutDate < today) ||
                            (b.CheckoutDate == today && currentTime >= checkoutTime)))
                .ToListAsync();

            if (!pendingCheckouts.Any())
            {
                _logger.LogDebug("No bookings ready for automatic check-out");
                return 0;
            }

            _logger.LogInformation("Processing {Count} bookings for automatic check-out", pendingCheckouts.Count);

            var checkedOutCount = 0;
            foreach (var booking in pendingCheckouts)
            {
                try
                {
                    var reason = booking.CheckoutDate < today
                        ? $"Automatic checkout - {(today.DayNumber - booking.CheckoutDate.DayNumber)} days past checkout date"
                        : "Automatic checkout at 12 PM";

                    await LogBookingChangeAsync(booking, "CheckedIn", "CheckedOut", reason);
                    booking.Status = "CheckedOut";
                    checkedOutCount++;

                    _logger.LogDebug("Auto-checked out booking {BookingId} for guest {GuestName}",
                        booking.Id, booking.GuestName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to auto-check out booking {BookingId}", booking.Id);
                }
            }

            await _context.SaveChangesAsync();

            if (checkedOutCount > 0)
            {
                _logger.LogInformation("Successfully auto-checked out {Count} bookings", checkedOutCount);
            }

            return checkedOutCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing pending check-outs");
            throw;
        }
    }

    public Task<BookingStatusUpdateResult> GetLastUpdateResultAsync()
    {
        return Task.FromResult(_lastResult ?? new BookingStatusUpdateResult
        {
            ExecutedAt = DateTime.MinValue,
            CheckinsProcessed = 0,
            CheckoutsProcessed = 0,
            ErrorsEncountered = 0
        });
    }

    private async Task LogBookingChangeAsync(
        object booking,
        string oldStatus,
        string newStatus,
        string reason)
    {
        try
        {
            // Use reflection to get booking properties
            var bookingType = booking.GetType();
            var bookingId = (int)bookingType.GetProperty("Id")?.GetValue(booking)!;
            var tenantId = (int)bookingType.GetProperty("TenantId")?.GetValue(booking)!;

            var changeHistory = new BookingChangeHistory
            {
                TenantId = tenantId,
                BookingId = bookingId,
                ChangeType = "auto_status_update",
                OldValue = oldStatus,
                NewValue = newStatus,
                ChangedBy = "AutomatedService",
                ChangeReason = reason,
                ChangedAt = DateTime.UtcNow
            };

            _context.BookingChangeHistory.Add(changeHistory);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to log booking change for booking {BookingId}",
                booking.GetType().GetProperty("Id")?.GetValue(booking));
        }
    }
}