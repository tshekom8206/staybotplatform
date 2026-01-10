using Microsoft.EntityFrameworkCore;
using Hostr.Api.Data;
using Hostr.Api.Models;
using System.Text;

namespace Hostr.Api.Services;

public class ProactiveMessageService : IProactiveMessageService
{
    private readonly HostrDbContext _context;
    private readonly IWhatsAppService _whatsAppService;
    private readonly ISmsService _smsService;
    private readonly ILogger<ProactiveMessageService> _logger;

    public ProactiveMessageService(
        HostrDbContext context,
        IWhatsAppService whatsAppService,
        ISmsService smsService,
        ILogger<ProactiveMessageService> logger)
    {
        _context = context;
        _whatsAppService = whatsAppService;
        _smsService = smsService;
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

            // Build portal URL for templates (subdomain pattern: {slug}.staybot.co.za)
            var portalUrl = $"https://{tenant?.Slug}.staybot.co.za";

            // 0. Pre-Arrival message (X days before check-in)
            if (settings.PreArrivalEnabled)
            {
                var preArrivalDate = booking.CheckinDate.AddDays(-settings.PreArrivalDaysBefore);
                var preArrivalDateTime = preArrivalDate.ToDateTime(TimeOnly.FromTimeSpan(settings.PreArrivalTime));

                // Only schedule if we have enough days before check-in
                if (preArrivalDateTime > now)
                {
                    var content = BuildMessageFromTemplate(
                        settings.PreArrivalTemplate ?? GetDefaultPreArrivalTemplate(),
                        booking, tenant!, portalUrl);
                    await CreateScheduledMessageAsync(tenantId, booking, ScheduledMessageType.PreArrival,
                        preArrivalDateTime, content, null);
                }
                else
                {
                    _logger.LogInformation("Skipping PreArrival message - scheduled time {Time} already passed (booking made too close to check-in)", preArrivalDateTime);
                }
            }

            // 1. Check-in day message (9 AM on check-in day)
            if (settings.CheckinDayEnabled)
            {
                var checkinDateTime = booking.CheckinDate.ToDateTime(TimeOnly.FromTimeSpan(settings.CheckinDayTime));

                // Skip if scheduled time already passed (same-day booking after 9 AM)
                if (checkinDateTime > now)
                {
                    var content = BuildCheckinDayMessage(hotelName, booking, isRepeatGuest, previousBooking, settings, portalUrl);
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
                    var content = BuildMidStayMessage(hotelName, booking, tenantId, portalUrl);
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
                    var content = BuildPreCheckoutMessage(hotelName, booking, isOneNight, portalUrl);
                    await CreateScheduledMessageAsync(tenantId, booking, ScheduledMessageType.PreCheckout,
                        preCheckoutDateTime, content, null);
                }
            }

            // 4. Post-stay feedback (day after checkout, 10 AM)
            if (settings.PostStayEnabled)
            {
                var postStayDate = booking.CheckoutDate.AddDays(1);
                var postStayDateTime = postStayDate.ToDateTime(TimeOnly.FromTimeSpan(settings.PostStayTime));

                var content = BuildPostStayMessage(hotelName, booking, portalUrl);
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

    public async Task ScheduleWelcomeSettledAsync(Booking booking)
    {
        try
        {
            var settings = await GetOrCreateSettingsAsync(booking.TenantId);
            if (!settings.WelcomeSettledEnabled)
            {
                _logger.LogInformation("WelcomeSettled message disabled for tenant {TenantId}", booking.TenantId);
                return;
            }

            var tenant = await _context.Tenants.FindAsync(booking.TenantId);
            var portalUrl = $"https://{tenant?.Slug}.staybot.co.za";

            // Schedule for X hours after now (actual check-in time)
            var scheduledFor = DateTime.UtcNow.AddHours(settings.WelcomeSettledHoursAfter);

            var content = BuildMessageFromTemplate(
                settings.WelcomeSettledTemplate ?? GetDefaultWelcomeSettledTemplate(),
                booking, tenant!, portalUrl);

            await CreateScheduledMessageAsync(booking.TenantId, booking,
                ScheduledMessageType.WelcomeSettled, scheduledFor, content, null);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Scheduled WelcomeSettled message for booking {BookingId} at {ScheduledFor}",
                booking.Id, scheduledFor);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scheduling WelcomeSettled message for booking {BookingId}", booking.Id);
        }
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

                    // NEW: WhatsApp-first with SMS fallback (using templates if WhatsApp available)
                    var result = await AttemptMessageDeliveryAsync(
                        message.Id,
                        message.TenantId,
                        message.Phone,
                        message.Content,
                        message.MediaUrl,
                        message.AttemptedMethod,
                        message.MessageType,
                        message.Booking);

                    if (result.Success)
                    {
                        message.Status = ScheduledMessageStatus.Sent;
                        message.SentAt = DateTime.UtcNow;
                        message.SuccessfulMethod = result.SuccessfulMethod;
                        message.ErrorMessage = null;
                        message.WhatsAppFailureReason = result.WhatsAppError;

                        _logger.LogInformation("Sent {MessageType} message to {Phone} for booking {BookingId} via {Method}",
                            message.MessageType, message.Phone, message.BookingId, result.SuccessfulMethod);
                    }
                    else
                    {
                        message.RetryCount++;
                        message.ErrorMessage = result.ErrorMessage;
                        message.WhatsAppFailureReason = result.WhatsAppError;

                        if (message.RetryCount >= 3)
                        {
                            message.Status = ScheduledMessageStatus.Failed;
                            _logger.LogWarning("Message {MessageId} failed after 3 retries. Last error: {Error}",
                                message.Id, result.ErrorMessage);
                        }
                        else
                        {
                            _logger.LogWarning("Message {MessageId} failed (attempt {Attempt}/3). Error: {Error}",
                                message.Id, message.RetryCount, result.ErrorMessage);
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

            // Log summary statistics
            var sentCount = dueMessages.Count(m => m.Status == ScheduledMessageStatus.Sent);
            var failedCount = dueMessages.Count(m => m.Status == ScheduledMessageStatus.Failed);
            var whatsAppCount = dueMessages.Count(m => m.SuccessfulMethod == DeliveryMethod.WhatsApp);
            var smsCount = dueMessages.Count(m => m.SuccessfulMethod == DeliveryMethod.SMS);
            var fallbackCount = dueMessages.Count(m => m.SuccessfulMethod == DeliveryMethod.WhatsAppFailedToSMS);

            _logger.LogInformation(
                "Processed {Total} messages: {Sent} sent ({WhatsApp} WhatsApp, {SMS} SMS, {Fallback} fallback), {Failed} failed",
                dueMessages.Count, sentCount, whatsAppCount, smsCount, fallbackCount, failedCount);
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

        // Enable/disable flags
        settings.CheckinDayEnabled = newSettings.CheckinDayEnabled;
        settings.MidStayEnabled = newSettings.MidStayEnabled;
        settings.PreCheckoutEnabled = newSettings.PreCheckoutEnabled;
        settings.PostStayEnabled = newSettings.PostStayEnabled;
        settings.PreArrivalEnabled = newSettings.PreArrivalEnabled;
        settings.WelcomeSettledEnabled = newSettings.WelcomeSettledEnabled;

        // Timing settings
        settings.CheckinDayTime = newSettings.CheckinDayTime;
        settings.MidStayTime = newSettings.MidStayTime;
        settings.PreCheckoutTime = newSettings.PreCheckoutTime;
        settings.PostStayTime = newSettings.PostStayTime;
        settings.PreArrivalTime = newSettings.PreArrivalTime;
        settings.PreArrivalDaysBefore = newSettings.PreArrivalDaysBefore;
        settings.WelcomeSettledHoursAfter = newSettings.WelcomeSettledHoursAfter;

        // Message templates
        settings.PreArrivalTemplate = newSettings.PreArrivalTemplate;
        settings.CheckinDayTemplate = newSettings.CheckinDayTemplate;
        settings.MidStayTemplate = newSettings.MidStayTemplate;
        settings.PreCheckoutTemplate = newSettings.PreCheckoutTemplate;
        settings.PostStayTemplate = newSettings.PostStayTemplate;
        settings.WelcomeSettledTemplate = newSettings.WelcomeSettledTemplate;

        // Media settings
        settings.WelcomeImageUrl = newSettings.WelcomeImageUrl;
        settings.IncludePhotoInWelcome = newSettings.IncludePhotoInWelcome;
        settings.Timezone = newSettings.Timezone;
        settings.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return settings;
    }

    #region Private Helper Methods

    /// <summary>
    /// Build message content from a template with placeholder replacement
    /// </summary>
    private string BuildMessageFromTemplate(string template, Booking booking, Tenant tenant, string portalUrl)
    {
        if (string.IsNullOrEmpty(template)) return string.Empty;

        var nights = (booking.CheckoutDate.ToDateTime(TimeOnly.MinValue) -
                     booking.CheckinDate.ToDateTime(TimeOnly.MinValue)).Days;

        // For PrepareLink: Use booking ID (for pre-arrival when no room assigned yet)
        // For FeedbackLink: Use room number (sent after check-in when room is known)
        var prepareLink = $"{portalUrl}/prepare?booking={booking.Id}";
        var feedbackLink = !string.IsNullOrEmpty(booking.RoomNumber)
            ? $"{portalUrl}/feedback?room={booking.RoomNumber}"
            : $"{portalUrl}/feedback?booking={booking.Id}";

        return template
            .Replace("{GuestFirstName}", booking.GuestName.Split(' ')[0])
            .Replace("{GuestName}", booking.GuestName)
            .Replace("{HotelName}", tenant?.Name ?? "Hotel")
            .Replace("{CheckInDate}", booking.CheckinDate.ToString("dddd, MMMM d"))
            .Replace("{CheckOutDate}", booking.CheckoutDate.ToString("dddd, MMMM d"))
            .Replace("{RoomNumber}", booking.RoomNumber ?? "your room")
            .Replace("{PrepareLink}", prepareLink)
            .Replace("{FeedbackLink}", feedbackLink)
            .Replace("{Nights}", nights.ToString());
    }

    /// <summary>
    /// Default template for Pre-Arrival message (seeded when not customized)
    /// </summary>
    private static string GetDefaultPreArrivalTemplate()
    {
        return @"Hi {GuestFirstName}!

Your stay at {HotelName} is coming up on {CheckInDate}. We're excited to welcome you!

Prepare for your arrival:
{PrepareLink}

Safe travels!";
    }

    /// <summary>
    /// Default template for Welcome Settled message (seeded when not customized)
    /// </summary>
    private static string GetDefaultWelcomeSettledTemplate()
    {
        return @"Hi {GuestFirstName}!

Hope you're settling in well! How's room {RoomNumber}?

Let us know how we're doing:
{FeedbackLink}

We're here for anything you need!";
    }

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
        ProactiveMessageSettings settings,
        string portalUrl)
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
        sb.AppendLine($"Guest Portal: {portalUrl}");
        sb.AppendLine();
        sb.AppendLine("Need early check-in or have special requests? Just reply to this message!");
        sb.AppendLine();
        sb.AppendLine("Safe travels - we look forward to welcoming you!");

        return sb.ToString();
    }

    private string BuildMidStayMessage(string hotelName, Booking booking, int tenantId, string portalUrl)
    {
        var firstName = booking.GuestName.Split(' ')[0];
        var feedbackUrl = !string.IsNullOrEmpty(booking.RoomNumber)
            ? $"{portalUrl}/feedback?room={booking.RoomNumber}"
            : $"{portalUrl}/feedback?booking={booking.Id}";

        var sb = new System.Text.StringBuilder();

        sb.AppendLine($"Good morning {firstName}!");
        sb.AppendLine();
        sb.AppendLine("How's your stay so far? We'd love to hear if there's anything we can do to make it even better.");
        sb.AppendLine();
        sb.AppendLine($"Share your feedback: {feedbackUrl}");
        sb.AppendLine();
        sb.AppendLine("While you're here, don't forget to explore our amenities and services - just ask if you need any recommendations!");
        sb.AppendLine();
        sb.AppendLine("Reply anytime - we're here for you!");

        return sb.ToString();
    }

    private string BuildPreCheckoutMessage(string hotelName, Booking booking, bool isOneNight, string portalUrl)
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
        sb.AppendLine($"Manage your checkout: {portalUrl}");
        sb.AppendLine();
        sb.AppendLine("Need help with luggage storage or airport transfer? Just ask!");
        sb.AppendLine();
        sb.AppendLine("Thank you for staying with us!");

        return sb.ToString();
    }

    private string BuildPostStayMessage(string hotelName, Booking booking, string portalUrl)
    {
        var firstName = booking.GuestName.Split(' ')[0];
        var feedbackUrl = !string.IsNullOrEmpty(booking.RoomNumber)
            ? $"{portalUrl}/feedback?room={booking.RoomNumber}"
            : $"{portalUrl}/feedback?booking={booking.Id}";

        var sb = new System.Text.StringBuilder();

        sb.AppendLine($"Hi {firstName},");
        sb.AppendLine();
        sb.AppendLine($"Thank you for staying with us at {hotelName}!");
        sb.AppendLine();
        sb.AppendLine("We'd love to hear about your experience. How was your stay?");
        sb.AppendLine();
        sb.AppendLine($"Share your feedback: {feedbackUrl}");
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

    #region WhatsApp-First Delivery with SMS Fallback

    /// <summary>
    /// Result of attempting to deliver a message via WhatsApp and/or SMS
    /// </summary>
    private record MessageDeliveryResult(
        bool Success,
        DeliveryMethod? SuccessfulMethod,
        string? ErrorMessage,
        string? WhatsAppError);

    /// <summary>
    /// Template information for a message type
    /// </summary>
    /// <summary>
    /// Generates a redirect token for WhatsApp button URLs
    /// </summary>
    private string GenerateRedirectToken(string tenantSlug, string path)
    {
        var json = $"{{\"t\":\"{tenantSlug}\",\"p\":\"{path}\"}}";
        var bytes = Encoding.UTF8.GetBytes(json);
        return Convert.ToBase64String(bytes);
    }

    private record TemplateInfo(
        string TemplateName,
        List<string> BodyParameters,
        string? ButtonUrlParameter);

    /// <summary>
    /// Builds WhatsApp template parameters based on message type and booking data
    /// </summary>
    private async Task<TemplateInfo?> BuildTemplateParametersAsync(
        ScheduledMessageType messageType,
        int tenantId,
        Booking booking)
    {
        try
        {
            // Get tenant info for URLs and display name
            var tenant = await _context.Tenants.FindAsync(tenantId);
            if (tenant == null)
            {
                _logger.LogWarning("Tenant {TenantId} not found for template building", tenantId);
                return null;
            }

            var tenantSlug = tenant.Slug ?? tenant.Name.ToLower().Replace(" ", "-");
            var hotelName = tenant.Name;
            var guestName = booking.GuestName;
            var roomNumber = booking.RoomNumber ?? "your room";

            // Build template based on message type
            switch (messageType)
            {
                case ScheduledMessageType.PreArrival:
                    var checkinDate = booking.CheckinDate.ToString("dddd, MMMM d");
                    var token = GenerateRedirectToken(tenantSlug, "prepare");
                    return new TemplateInfo(
                        "pre_arrival_welcome_v04",
                        new List<string> { guestName, hotelName, roomNumber, checkinDate },
                        token);

                case ScheduledMessageType.CheckinDay:
                    var checkinTime = "2:00 PM"; // Default check-in time
                    token = GenerateRedirectToken(tenantSlug, "prepare");
                    return new TemplateInfo(
                        "checkin_day_ready_v04",
                        new List<string> { guestName, roomNumber, hotelName, checkinTime },
                        token);

                case ScheduledMessageType.WelcomeSettled:
                    token = GenerateRedirectToken(tenantSlug, "services");
                    return new TemplateInfo(
                        "welcome_settled_v05",
                        new List<string> { roomNumber },
                        token);

                case ScheduledMessageType.MidStay:
                    token = GenerateRedirectToken(tenantSlug, "housekeeping");
                    return new TemplateInfo(
                        "mid_stay_checkup_v04",
                        new List<string> { guestName, hotelName, roomNumber },
                        token);

                case ScheduledMessageType.PreCheckout:
                    var checkoutTime = "11:00 AM"; // Default checkout time
                    token = GenerateRedirectToken(tenantSlug, "checkout");
                    return new TemplateInfo(
                        "pre_checkout_reminder_v03",
                        new List<string> { guestName, hotelName, roomNumber, checkoutTime },
                        token);

                case ScheduledMessageType.PostStay:
                    token = GenerateRedirectToken(tenantSlug, $"feedback/{booking.Id}");
                    return new TemplateInfo(
                        "post_stay_survey_v03",
                        new List<string> { guestName, hotelName, roomNumber },
                        token);

                default:
                    _logger.LogWarning("No template mapping for message type {MessageType}", messageType);
                    return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error building template parameters for {MessageType}", messageType);
            return null;
        }
    }

    /// <summary>
    /// Attempts to deliver a message using WhatsApp templates first, falling back to SMS on failure
    /// </summary>
    private async Task<MessageDeliveryResult> AttemptMessageDeliveryAsync(
        int messageId,
        int tenantId,
        string phone,
        string content,
        string? mediaUrl,
        DeliveryMethod attemptedMethod,
        ScheduledMessageType messageType,
        Booking booking)
    {
        // If configured for SMS-only, skip WhatsApp
        if (attemptedMethod == DeliveryMethod.SMS)
        {
            return await SendViaSmsAsync(phone, content);
        }

        // Check if shared WhatsApp number is configured (TenantId is null for shared number)
        var hasWhatsApp = await _context.WhatsAppNumbers
            .AnyAsync(w => w.TenantId == null && w.Status == "Active");

        if (!hasWhatsApp)
        {
            _logger.LogInformation("Message {Id}: No WhatsApp for tenant {TenantId}, using SMS",
                messageId, tenantId);
            return await SendViaSmsAsync(phone, content);
        }

        // Try WhatsApp first
        _logger.LogInformation("Message {Id}: Attempting WhatsApp to {Phone}", messageId, phone);

        // Handle media messages (no templates for media yet)
        if (!string.IsNullOrEmpty(mediaUrl))
        {
            var (success, error) = await _whatsAppService.SendImageWithDetailsAsync(tenantId, phone, mediaUrl, content);
            if (success)
            {
                _logger.LogInformation("Message {Id}: WhatsApp image success", messageId);
                return new MessageDeliveryResult(true, DeliveryMethod.WhatsApp, null, null);
            }

            // WhatsApp failed - fallback to SMS (text only, no image)
            _logger.LogWarning("Message {Id}: WhatsApp image failed ({Error}), SMS fallback", messageId, error);
            var smsResult = await SendViaSmsAsync(phone, content);
            if (smsResult.Success)
            {
                _logger.LogInformation("Message {Id}: SMS fallback success (text only, image not sent)", messageId);
                return new MessageDeliveryResult(true, DeliveryMethod.WhatsAppFailedToSMS, null, error);
            }

            return new MessageDeliveryResult(false, null,
                $"Both failed. WA: {error}, SMS: {smsResult.ErrorMessage}", error);
        }

        // Try sending via WhatsApp template
        var templateInfo = await BuildTemplateParametersAsync(messageType, tenantId, booking);

        if (templateInfo != null)
        {
            _logger.LogInformation("Message {Id}: Sending template {TemplateName} to {Phone}",
                messageId, templateInfo.TemplateName, phone);

            var (templateSuccess, templateError) = await _whatsAppService.SendTemplateWithParametersAsync(
                tenantId,
                phone,
                templateInfo.TemplateName,
                templateInfo.BodyParameters,
                templateInfo.ButtonUrlParameter);

            if (templateSuccess)
            {
                _logger.LogInformation("Message {Id}: WhatsApp template success", messageId);
                return new MessageDeliveryResult(true, DeliveryMethod.WhatsApp, null, null);
            }

            // Template failed - try SMS fallback
            _logger.LogWarning("Message {Id}: WhatsApp template failed ({Error}), SMS fallback",
                messageId, templateError);
            var (smsSuccess, smsError) = await _smsService.SendMessageWithDetailsAsync(phone, content);

            if (smsSuccess)
            {
                _logger.LogInformation("Message {Id}: SMS fallback success", messageId);
                return new MessageDeliveryResult(true, DeliveryMethod.WhatsAppFailedToSMS, null, templateError);
            }

            // Both failed
            return new MessageDeliveryResult(false, null,
                $"Both failed. Template: {templateError}, SMS: {smsError}", templateError);
        }

        // No template available - fall back to regular text message (should not happen for guest journey messages)
        _logger.LogWarning("Message {Id}: No template for {MessageType}, using free-form text",
            messageId, messageType);

        var (waSuccess, waError) = await _whatsAppService.SendTextMessageWithDetailsAsync(
            tenantId, phone, content);

        if (waSuccess)
        {
            _logger.LogInformation("Message {Id}: WhatsApp text success", messageId);
            return new MessageDeliveryResult(true, DeliveryMethod.WhatsApp, null, null);
        }

        // WhatsApp failed - try SMS
        _logger.LogWarning("Message {Id}: WhatsApp text failed ({Error}), SMS fallback", messageId, waError);
        var (smsFinalSuccess, smsFinalError) = await _smsService.SendMessageWithDetailsAsync(phone, content);

        if (smsFinalSuccess)
        {
            _logger.LogInformation("Message {Id}: SMS fallback success", messageId);
            return new MessageDeliveryResult(true, DeliveryMethod.WhatsAppFailedToSMS, null, waError);
        }

        // Both failed
        return new MessageDeliveryResult(false, null,
            $"Both failed. WA: {waError}, SMS: {smsFinalError}", waError);
    }

    /// <summary>
    /// Sends a message via SMS only (for SMS-only mode)
    /// </summary>
    private async Task<MessageDeliveryResult> SendViaSmsAsync(string phone, string content)
    {
        var (success, error) = await _smsService.SendMessageWithDetailsAsync(phone, content);
        return success
            ? new MessageDeliveryResult(true, DeliveryMethod.SMS, null, null)
            : new MessageDeliveryResult(false, null, $"SMS failed: {error}", null);
    }

    #endregion


    #endregion
}
