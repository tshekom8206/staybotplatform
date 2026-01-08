using Microsoft.EntityFrameworkCore;
using Hostr.Api.Data;
using Hostr.Api.Models;

namespace Hostr.Api.Services;

public class ProactiveMessageService : IProactiveMessageService
{
    private readonly HostrDbContext _context;
    private readonly IWhatsAppService _whatsAppService;
    private readonly ILogger<ProactiveMessageService> _logger;

    public ProactiveMessageService(
        HostrDbContext context,
        IWhatsAppService whatsAppService,
        ILogger<ProactiveMessageService> logger)
    {
        _context = context;
        _whatsAppService = whatsAppService;
        _logger = logger;
    }

    public async Task ScheduleMessagesForBookingAsync(int tenantId, Booking booking)
    {
        try
        {
            var settings = await GetOrCreateSettingsAsync(tenantId);
            var tenant = await _context.Tenants.FindAsync(tenantId);
            var hotelName = tenant?.Name ?? "Hotel";

            // Calculate stay duration
            var nights = (booking.CheckoutDate.ToDateTime(TimeOnly.MinValue) -
                         booking.CheckinDate.ToDateTime(TimeOnly.MinValue)).Days;
            var isOneNight = nights == 1;

            // Get timezone for scheduling
            var tz = GetTimezone(settings.Timezone);
            var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);

            _logger.LogInformation("Scheduling proactive messages for booking {BookingId}: {Nights} nights, isOneNight={IsOneNight}",
                booking.Id, nights, isOneNight);

            // Check for repeat guest
            var isRepeatGuest = booking.IsRepeatGuest || await IsRepeatGuestAsync(booking.Phone, booking.Id);
            var previousBooking = isRepeatGuest ? await GetPreviousBookingAsync(booking.Phone, booking.Id) : null;

            // 1. Check-in day message (9 AM on check-in day)
            if (settings.CheckinDayEnabled)
            {
                var checkinDateTime = booking.CheckinDate.ToDateTime(TimeOnly.FromTimeSpan(settings.CheckinDayTime));

                // Skip if scheduled time already passed (same-day booking after 9 AM)
                if (checkinDateTime > now)
                {
                    var content = BuildCheckinDayMessage(hotelName, booking, isRepeatGuest, previousBooking, settings);
                    await CreateScheduledMessageAsync(tenantId, booking, ScheduledMessageType.CheckinDay,
                        checkinDateTime, content, settings.WelcomeImageUrl);
                }
                else
                {
                    _logger.LogInformation("Skipping CheckinDay message - scheduled time {Time} already passed", checkinDateTime);
                }
            }

            // 2. Mid-stay check (Day 2, 10 AM) - SKIP for 1-night stays
            if (settings.MidStayEnabled && !isOneNight)
            {
                var midStayDate = booking.CheckinDate.AddDays(1);
                var midStayDateTime = midStayDate.ToDateTime(TimeOnly.FromTimeSpan(settings.MidStayTime));

                if (midStayDateTime > now)
                {
                    var content = BuildMidStayMessage(hotelName, booking, tenantId);
                    await CreateScheduledMessageAsync(tenantId, booking, ScheduledMessageType.MidStay,
                        midStayDateTime, content, null);
                }
            }

            // 3. Pre-checkout reminder
            if (settings.PreCheckoutEnabled)
            {
                DateTime preCheckoutDateTime;

                if (isOneNight)
                {
                    // For 1-night stays: same day evening (6 PM on check-in day)
                    preCheckoutDateTime = booking.CheckinDate.ToDateTime(TimeOnly.FromTimeSpan(settings.PreCheckoutTime));
                }
                else
                {
                    // For multi-night stays: day before checkout (6 PM)
                    var preCheckoutDate = booking.CheckoutDate.AddDays(-1);
                    preCheckoutDateTime = preCheckoutDate.ToDateTime(TimeOnly.FromTimeSpan(settings.PreCheckoutTime));
                }

                if (preCheckoutDateTime > now)
                {
                    var content = BuildPreCheckoutMessage(hotelName, booking, isOneNight);
                    await CreateScheduledMessageAsync(tenantId, booking, ScheduledMessageType.PreCheckout,
                        preCheckoutDateTime, content, null);
                }
            }

            // 4. Post-stay feedback (day after checkout, 10 AM)
            if (settings.PostStayEnabled)
            {
                var postStayDate = booking.CheckoutDate.AddDays(1);
                var postStayDateTime = postStayDate.ToDateTime(TimeOnly.FromTimeSpan(settings.PostStayTime));

                var content = BuildPostStayMessage(hotelName, booking);
                await CreateScheduledMessageAsync(tenantId, booking, ScheduledMessageType.PostStay,
                    postStayDateTime, content, null);
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Scheduled proactive messages for booking {BookingId}", booking.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scheduling proactive messages for booking {BookingId}", booking.Id);
        }
    }

    public async Task CancelMessagesForBookingAsync(int bookingId)
    {
        try
        {
            var pendingMessages = await _context.ScheduledMessages
                .Where(m => m.BookingId == bookingId && m.Status == ScheduledMessageStatus.Pending)
                .ToListAsync();

            foreach (var message in pendingMessages)
            {
                message.Status = ScheduledMessageStatus.Cancelled;
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Cancelled {Count} pending messages for booking {BookingId}",
                pendingMessages.Count, bookingId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling messages for booking {BookingId}", bookingId);
        }
    }

    public async Task RescheduleMessagesForBookingAsync(int tenantId, Booking booking)
    {
        // Cancel existing messages and reschedule
        await CancelMessagesForBookingAsync(booking.Id);
        await ScheduleMessagesForBookingAsync(tenantId, booking);
    }

    public async Task ProcessDueMessagesAsync()
    {
        try
        {
            var now = DateTime.UtcNow;

            // Get all pending messages that are due
            var dueMessages = await _context.ScheduledMessages
                .Include(m => m.Booking)
                .Where(m => m.Status == ScheduledMessageStatus.Pending &&
                           m.ScheduledFor <= now &&
                           m.RetryCount < 3)
                .OrderBy(m => m.ScheduledFor)
                .Take(50) // Process in batches
                .ToListAsync();

            _logger.LogInformation("Processing {Count} due scheduled messages", dueMessages.Count);

            foreach (var message in dueMessages)
            {
                try
                {
                    // Check if booking is still valid
                    if (message.Booking.Status == "Cancelled")
                    {
                        message.Status = ScheduledMessageStatus.Cancelled;
                        continue;
                    }

                    // Send the message
                    bool sent;
                    if (!string.IsNullOrEmpty(message.MediaUrl))
                    {
                        sent = await _whatsAppService.SendImageAsync(
                            message.TenantId, message.Phone, message.MediaUrl, message.Content);
                    }
                    else
                    {
                        sent = await _whatsAppService.SendTextMessageAsync(
                            message.TenantId, message.Phone, message.Content);
                    }

                    if (sent)
                    {
                        message.Status = ScheduledMessageStatus.Sent;
                        message.SentAt = DateTime.UtcNow;
                        _logger.LogInformation("Sent {MessageType} message to {Phone} for booking {BookingId}",
                            message.MessageType, message.Phone, message.BookingId);
                    }
                    else
                    {
                        message.RetryCount++;
                        message.ErrorMessage = "WhatsApp send failed";

                        if (message.RetryCount >= 3)
                        {
                            message.Status = ScheduledMessageStatus.Failed;
                            _logger.LogWarning("Message {MessageId} failed after 3 retries", message.Id);
                        }
                    }
                }
                catch (Exception ex)
                {
                    message.RetryCount++;
                    message.ErrorMessage = ex.Message;

                    if (message.RetryCount >= 3)
                    {
                        message.Status = ScheduledMessageStatus.Failed;
                    }

                    _logger.LogError(ex, "Error sending scheduled message {MessageId}", message.Id);
                }
            }

            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing due messages");
        }
    }

    public async Task<ProactiveMessageSettings> GetOrCreateSettingsAsync(int tenantId)
    {
        var settings = await _context.ProactiveMessageSettings
            .FirstOrDefaultAsync(s => s.TenantId == tenantId);

        if (settings == null)
        {
            settings = new ProactiveMessageSettings
            {
                TenantId = tenantId
            };
            _context.ProactiveMessageSettings.Add(settings);
            await _context.SaveChangesAsync();
        }

        return settings;
    }

    public async Task<ProactiveMessageSettings> UpdateSettingsAsync(int tenantId, ProactiveMessageSettings newSettings)
    {
        var settings = await GetOrCreateSettingsAsync(tenantId);

        settings.CheckinDayEnabled = newSettings.CheckinDayEnabled;
        settings.MidStayEnabled = newSettings.MidStayEnabled;
        settings.PreCheckoutEnabled = newSettings.PreCheckoutEnabled;
        settings.PostStayEnabled = newSettings.PostStayEnabled;
        settings.CheckinDayTime = newSettings.CheckinDayTime;
        settings.MidStayTime = newSettings.MidStayTime;
        settings.PreCheckoutTime = newSettings.PreCheckoutTime;
        settings.PostStayTime = newSettings.PostStayTime;
        settings.WelcomeImageUrl = newSettings.WelcomeImageUrl;
        settings.IncludePhotoInWelcome = newSettings.IncludePhotoInWelcome;
        settings.Timezone = newSettings.Timezone;
        settings.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return settings;
    }

    #region Private Helper Methods

    private async Task CreateScheduledMessageAsync(
        int tenantId,
        Booking booking,
        ScheduledMessageType messageType,
        DateTime scheduledFor,
        string content,
        string? mediaUrl)
    {
        var message = new ScheduledMessage
        {
            TenantId = tenantId,
            BookingId = booking.Id,
            Phone = booking.Phone,
            MessageType = messageType,
            ScheduledFor = scheduledFor,
            Content = content,
            MediaUrl = mediaUrl,
            Status = ScheduledMessageStatus.Pending
        };

        _context.ScheduledMessages.Add(message);
        _logger.LogInformation("Created {MessageType} message scheduled for {ScheduledFor}",
            messageType, scheduledFor);
    }

    private async Task<bool> IsRepeatGuestAsync(string phone, int currentBookingId)
    {
        return await _context.Bookings
            .AnyAsync(b => b.Phone == phone && b.Id != currentBookingId);
    }

    private async Task<Booking?> GetPreviousBookingAsync(string phone, int currentBookingId)
    {
        return await _context.Bookings
            .Where(b => b.Phone == phone && b.Id != currentBookingId)
            .OrderByDescending(b => b.CheckoutDate)
            .FirstOrDefaultAsync();
    }

    private string BuildCheckinDayMessage(
        string hotelName,
        Booking booking,
        bool isRepeatGuest,
        Booking? previousBooking,
        ProactiveMessageSettings settings)
    {
        var firstName = booking.GuestName.Split(' ')[0];
        var checkInDate = booking.CheckinDate.ToString("dddd, MMMM d");

        var sb = new System.Text.StringBuilder();

        sb.AppendLine($"Good morning {firstName}!");
        sb.AppendLine();

        if (isRepeatGuest && previousBooking != null)
        {
            sb.AppendLine($"Welcome back to {hotelName}! We're delighted to see you again.");
            if (!string.IsNullOrEmpty(previousBooking.RoomNumber))
            {
                sb.AppendLine($"Last time you stayed in room {previousBooking.RoomNumber} - we've noted your preferences.");
            }
        }
        else
        {
            sb.AppendLine($"Your room at {hotelName} is being prepared for your arrival today.");
        }

        sb.AppendLine();
        sb.AppendLine($"*Check-in Details*");
        sb.AppendLine($"Date: {checkInDate}");
        sb.AppendLine($"Time: From 3:00 PM");

        if (!string.IsNullOrEmpty(booking.RoomNumber))
        {
            sb.AppendLine($"Room: {booking.RoomNumber}");
        }

        sb.AppendLine();
        sb.AppendLine("Need early check-in or have special requests? Just reply to this message!");
        sb.AppendLine();
        sb.AppendLine("Safe travels - we look forward to welcoming you!");

        return sb.ToString();
    }

    private string BuildMidStayMessage(string hotelName, Booking booking, int tenantId)
    {
        var firstName = booking.GuestName.Split(' ')[0];

        var sb = new System.Text.StringBuilder();

        sb.AppendLine($"Good morning {firstName}!");
        sb.AppendLine();
        sb.AppendLine("How's your stay so far? We'd love to hear if there's anything we can do to make it even better.");
        sb.AppendLine();

        // TODO: Add featured services from tenant's service list
        sb.AppendLine("While you're here, don't forget to explore our amenities and services - just ask if you need any recommendations!");
        sb.AppendLine();
        sb.AppendLine("Reply anytime - we're here for you!");

        return sb.ToString();
    }

    private string BuildPreCheckoutMessage(string hotelName, Booking booking, bool isOneNight)
    {
        var firstName = booking.GuestName.Split(' ')[0];
        var checkoutDate = booking.CheckoutDate.ToString("dddd, MMMM d");

        var sb = new System.Text.StringBuilder();

        sb.AppendLine($"Hi {firstName},");
        sb.AppendLine();

        if (isOneNight)
        {
            sb.AppendLine($"Just a reminder that checkout is tomorrow at 11:00 AM.");
            sb.AppendLine();
            sb.AppendLine("Would you like to extend your stay? We'd be happy to check availability!");
        }
        else
        {
            sb.AppendLine($"Just a reminder that checkout is on {checkoutDate} at 11:00 AM.");
            sb.AppendLine();
            sb.AppendLine("Need a late checkout? Let us know and we'll check availability.");
        }

        sb.AppendLine();
        sb.AppendLine("Need help with luggage storage or airport transfer? Just ask!");
        sb.AppendLine();
        sb.AppendLine("Thank you for staying with us!");

        return sb.ToString();
    }

    private string BuildPostStayMessage(string hotelName, Booking booking)
    {
        var firstName = booking.GuestName.Split(' ')[0];

        var sb = new System.Text.StringBuilder();

        sb.AppendLine($"Hi {firstName},");
        sb.AppendLine();
        sb.AppendLine($"Thank you for staying with us at {hotelName}!");
        sb.AppendLine();
        sb.AppendLine("We'd love to hear about your experience. How was your stay?");
        sb.AppendLine();
        sb.AppendLine("Simply reply with a number from 1-10 (10 being excellent).");
        sb.AppendLine();
        sb.AppendLine("We hope to welcome you back soon!");

        return sb.ToString();
    }

    private TimeZoneInfo GetTimezone(string ianaTimezone)
    {
        // Try IANA timezone first (Linux/Azure)
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(ianaTimezone);
        }
        catch (TimeZoneNotFoundException)
        {
            // Fall back to Windows timezone IDs
            var windowsId = ianaTimezone switch
            {
                "Africa/Johannesburg" => "South Africa Standard Time",
                "Europe/London" => "GMT Standard Time",
                "America/New_York" => "Eastern Standard Time",
                "America/Los_Angeles" => "Pacific Standard Time",
                "Asia/Dubai" => "Arabian Standard Time",
                "Australia/Sydney" => "AUS Eastern Standard Time",
                _ => "South Africa Standard Time"
            };

            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(windowsId);
            }
            catch
            {
                // Ultimate fallback - use UTC
                return TimeZoneInfo.Utc;
            }
        }
    }


    #endregion
}
