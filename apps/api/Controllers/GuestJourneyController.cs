using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Hostr.Api.Data;
using Hostr.Api.Models;
using Hostr.Api.Services;

namespace Hostr.Api.Controllers;

/// <summary>
/// Controller for managing Guest Journey (proactive messaging) settings
/// </summary>
[Route("api/[controller]")]
[ApiController]
public class GuestJourneyController : ControllerBase
{
    private readonly HostrDbContext _context;
    private readonly IProactiveMessageService _proactiveMessageService;

    public GuestJourneyController(HostrDbContext context, IProactiveMessageService proactiveMessageService)
    {
        _context = context;
        _proactiveMessageService = proactiveMessageService;
    }

    /// <summary>
    /// Get proactive message settings for the current tenant
    /// </summary>
    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings()
    {
        try
        {
            var tenantId = HttpContext.Items["TenantId"] as int?;
            if (!tenantId.HasValue)
            {
                return BadRequest("Tenant context not found");
            }

            var settings = await _proactiveMessageService.GetOrCreateSettingsAsync(tenantId.Value);

            return Ok(new GuestJourneySettingsDto
            {
                // Pre-Arrival
                PreArrivalEnabled = settings.PreArrivalEnabled,
                PreArrivalDaysBefore = settings.PreArrivalDaysBefore,
                PreArrivalTime = settings.PreArrivalTime.ToString(@"hh\:mm"),
                PreArrivalTemplate = settings.PreArrivalTemplate ?? GetDefaultPreArrivalTemplate(),

                // Check-in Day
                CheckinDayEnabled = settings.CheckinDayEnabled,
                CheckinDayTime = settings.CheckinDayTime.ToString(@"hh\:mm"),
                CheckinDayTemplate = settings.CheckinDayTemplate ?? GetDefaultCheckinDayTemplate(),

                // Welcome Settled
                WelcomeSettledEnabled = settings.WelcomeSettledEnabled,
                WelcomeSettledHoursAfter = settings.WelcomeSettledHoursAfter,
                WelcomeSettledTemplate = settings.WelcomeSettledTemplate ?? GetDefaultWelcomeSettledTemplate(),

                // Mid-Stay
                MidStayEnabled = settings.MidStayEnabled,
                MidStayTime = settings.MidStayTime.ToString(@"hh\:mm"),
                MidStayTemplate = settings.MidStayTemplate ?? GetDefaultMidStayTemplate(),

                // Pre-Checkout
                PreCheckoutEnabled = settings.PreCheckoutEnabled,
                PreCheckoutTime = settings.PreCheckoutTime.ToString(@"hh\:mm"),
                PreCheckoutTemplate = settings.PreCheckoutTemplate ?? GetDefaultPreCheckoutTemplate(),

                // Post-Stay
                PostStayEnabled = settings.PostStayEnabled,
                PostStayTime = settings.PostStayTime.ToString(@"hh\:mm"),
                PostStayTemplate = settings.PostStayTemplate ?? GetDefaultPostStayTemplate(),

                // Media & Other
                WelcomeImageUrl = settings.WelcomeImageUrl,
                IncludePhotoInWelcome = settings.IncludePhotoInWelcome,
                Timezone = settings.Timezone
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Failed to load settings", error = ex.Message });
        }
    }

    /// <summary>
    /// Update proactive message settings for the current tenant
    /// </summary>
    [HttpPut("settings")]
    public async Task<IActionResult> UpdateSettings([FromBody] GuestJourneySettingsDto dto)
    {
        try
        {
            var tenantId = HttpContext.Items["TenantId"] as int?;
            if (!tenantId.HasValue)
            {
                return BadRequest("Tenant context not found");
            }

            var settings = await _proactiveMessageService.GetOrCreateSettingsAsync(tenantId.Value);

            // Update Pre-Arrival settings
            settings.PreArrivalEnabled = dto.PreArrivalEnabled;
            settings.PreArrivalDaysBefore = dto.PreArrivalDaysBefore;
            if (TimeSpan.TryParse(dto.PreArrivalTime, out var preArrivalTime))
                settings.PreArrivalTime = preArrivalTime;
            settings.PreArrivalTemplate = dto.PreArrivalTemplate;

            // Update Check-in Day settings
            settings.CheckinDayEnabled = dto.CheckinDayEnabled;
            if (TimeSpan.TryParse(dto.CheckinDayTime, out var checkinDayTime))
                settings.CheckinDayTime = checkinDayTime;
            settings.CheckinDayTemplate = dto.CheckinDayTemplate;

            // Update Welcome Settled settings
            settings.WelcomeSettledEnabled = dto.WelcomeSettledEnabled;
            settings.WelcomeSettledHoursAfter = dto.WelcomeSettledHoursAfter;
            settings.WelcomeSettledTemplate = dto.WelcomeSettledTemplate;

            // Update Mid-Stay settings
            settings.MidStayEnabled = dto.MidStayEnabled;
            if (TimeSpan.TryParse(dto.MidStayTime, out var midStayTime))
                settings.MidStayTime = midStayTime;
            settings.MidStayTemplate = dto.MidStayTemplate;

            // Update Pre-Checkout settings
            settings.PreCheckoutEnabled = dto.PreCheckoutEnabled;
            if (TimeSpan.TryParse(dto.PreCheckoutTime, out var preCheckoutTime))
                settings.PreCheckoutTime = preCheckoutTime;
            settings.PreCheckoutTemplate = dto.PreCheckoutTemplate;

            // Update Post-Stay settings
            settings.PostStayEnabled = dto.PostStayEnabled;
            if (TimeSpan.TryParse(dto.PostStayTime, out var postStayTime))
                settings.PostStayTime = postStayTime;
            settings.PostStayTemplate = dto.PostStayTemplate;

            // Update Media & Other settings
            settings.WelcomeImageUrl = dto.WelcomeImageUrl;
            settings.IncludePhotoInWelcome = dto.IncludePhotoInWelcome;
            settings.Timezone = dto.Timezone ?? "Africa/Johannesburg";

            var updatedSettings = await _proactiveMessageService.UpdateSettingsAsync(tenantId.Value, settings);

            return Ok(new { message = "Settings updated successfully" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Failed to update settings", error = ex.Message });
        }
    }

    /// <summary>
    /// Get available placeholders for message templates
    /// </summary>
    [HttpGet("placeholders")]
    public IActionResult GetPlaceholders()
    {
        var placeholders = new[]
        {
            new { name = "{GuestFirstName}", description = "Guest's first name" },
            new { name = "{GuestName}", description = "Guest's full name" },
            new { name = "{HotelName}", description = "Hotel/property name" },
            new { name = "{CheckInDate}", description = "Formatted check-in date" },
            new { name = "{CheckOutDate}", description = "Formatted check-out date" },
            new { name = "{RoomNumber}", description = "Assigned room number" },
            new { name = "{PrepareLink}", description = "Link to pre-arrival preparation page" },
            new { name = "{FeedbackLink}", description = "Link to feedback page" },
            new { name = "{Nights}", description = "Number of nights staying" }
        };

        return Ok(placeholders);
    }

    /// <summary>
    /// Preview a template with sample data
    /// </summary>
    [HttpPost("preview")]
    public async Task<IActionResult> PreviewTemplate([FromBody] PreviewTemplateRequest request)
    {
        try
        {
            var tenantId = HttpContext.Items["TenantId"] as int?;
            if (!tenantId.HasValue)
            {
                return BadRequest("Tenant context not found");
            }

            var tenant = await _context.Tenants.FindAsync(tenantId.Value);
            var hotelName = tenant?.Name ?? "Sample Hotel";
            var portalUrl = $"https://{tenant?.Slug ?? "sample"}.staybot.co.za";

            // Sample data for preview
            // Note: PrepareLink uses booking ID (for pre-arrival), FeedbackLink uses room number (for post-check-in)
            var preview = request.Template
                .Replace("{GuestFirstName}", "John")
                .Replace("{GuestName}", "John Smith")
                .Replace("{HotelName}", hotelName)
                .Replace("{CheckInDate}", DateTime.Today.AddDays(3).ToString("dddd, MMMM d"))
                .Replace("{CheckOutDate}", DateTime.Today.AddDays(5).ToString("dddd, MMMM d"))
                .Replace("{RoomNumber}", "205")
                .Replace("{PrepareLink}", $"{portalUrl}/prepare?booking=12345")
                .Replace("{FeedbackLink}", $"{portalUrl}/feedback?room=205")
                .Replace("{Nights}", "2");

            return Ok(new { preview });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Failed to generate preview", error = ex.Message });
        }
    }

    /// <summary>
    /// Get scheduled messages for the current tenant
    /// </summary>
    [HttpGet("scheduled-messages")]
    public async Task<IActionResult> GetScheduledMessages(
        [FromQuery] string? status = null,
        [FromQuery] string? type = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            var tenantId = HttpContext.Items["TenantId"] as int?;
            if (!tenantId.HasValue)
            {
                return BadRequest("Tenant context not found");
            }

            var query = _context.ScheduledMessages
                .Where(m => m.TenantId == tenantId.Value)
                .Include(m => m.Booking)
                .AsQueryable();

            // Filter by status
            if (!string.IsNullOrEmpty(status) && Enum.TryParse<ScheduledMessageStatus>(status, true, out var statusEnum))
            {
                query = query.Where(m => m.Status == statusEnum);
            }

            // Filter by type
            if (!string.IsNullOrEmpty(type) && Enum.TryParse<ScheduledMessageType>(type, true, out var typeEnum))
            {
                query = query.Where(m => m.MessageType == typeEnum);
            }

            var totalCount = await query.CountAsync();
            var messages = await query
                .OrderByDescending(m => m.ScheduledFor)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(m => new
                {
                    m.Id,
                    m.Phone,
                    MessageType = m.MessageType.ToString(),
                    m.ScheduledFor,
                    Status = m.Status.ToString(),
                    m.SentAt,
                    m.Content,
                    m.ErrorMessage,
                    m.RetryCount,
                    GuestName = m.Booking.GuestName,
                    RoomNumber = m.Booking.RoomNumber
                })
                .ToListAsync();

            return Ok(new
            {
                messages,
                totalCount,
                page,
                pageSize,
                totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Failed to load scheduled messages", error = ex.Message });
        }
    }

    /// <summary>
    /// Schedule proactive messages for a specific booking (for testing)
    /// </summary>
    [HttpPost("schedule-for-booking/{bookingId}")]
    [AllowAnonymous]
    public async Task<IActionResult> ScheduleMessagesForBooking(int bookingId, [FromQuery] int? tenantId = null)
    {
        try
        {
            var tid = tenantId ?? HttpContext.Items["TenantId"] as int?;
            if (!tid.HasValue)
            {
                return BadRequest(new { message = "Tenant context not found. Pass ?tenantId=X" });
            }

            var booking = await _context.Bookings
                .FirstOrDefaultAsync(b => b.Id == bookingId && b.TenantId == tid.Value);

            if (booking == null)
            {
                return NotFound(new { message = "Booking not found" });
            }

            await _proactiveMessageService.ScheduleMessagesForBookingAsync(tid.Value, booking);

            // Return the scheduled messages
            var scheduledMessages = await _context.ScheduledMessages
                .Where(m => m.BookingId == bookingId)
                .OrderBy(m => m.ScheduledFor)
                .Select(m => new
                {
                    m.Id,
                    MessageType = m.MessageType.ToString(),
                    m.ScheduledFor,
                    Status = m.Status.ToString(),
                    m.Content
                })
                .ToListAsync();

            return Ok(new
            {
                message = $"Scheduled {scheduledMessages.Count} messages for booking {bookingId}",
                scheduledMessages
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Failed to schedule messages", error = ex.Message });
        }
    }

    /// <summary>
    /// Manually trigger processing of due scheduled messages (for testing)
    /// </summary>
    [HttpPost("process-messages")]
    [AllowAnonymous]
    public async Task<IActionResult> ProcessMessages()
    {
        try
        {
            await _proactiveMessageService.ProcessDueMessagesAsync();

            // Get recently sent messages
            var recentMessages = await _context.ScheduledMessages
                .Where(m => m.SentAt.HasValue && m.SentAt.Value > DateTime.UtcNow.AddMinutes(-5))
                .OrderByDescending(m => m.SentAt)
                .Select(m => new
                {
                    m.Id,
                    MessageType = m.MessageType.ToString(),
                    m.Phone,
                    m.ScheduledFor,
                    m.SentAt,
                    Status = m.Status.ToString(),
                    m.Content
                })
                .ToListAsync();

            return Ok(new
            {
                message = "Processed due messages",
                sentCount = recentMessages.Count,
                sentMessages = recentMessages
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Failed to process messages", error = ex.Message });
        }
    }

    #region Default Templates

    private static string GetDefaultPreArrivalTemplate()
    {
        return @"Hi {GuestFirstName}!

Your stay at {HotelName} is coming up on {CheckInDate}. We're excited to welcome you!

Prepare for your arrival:
{PrepareLink}

Safe travels!";
    }

    private static string GetDefaultCheckinDayTemplate()
    {
        return @"Good morning {GuestFirstName}!

Today's the day! We're looking forward to welcoming you to {HotelName}.

Check-in is available from 2 PM. Need an early check-in? Just let us know!

See you soon!";
    }

    private static string GetDefaultWelcomeSettledTemplate()
    {
        return @"Hi {GuestFirstName}!

Hope you're settling in well! How's room {RoomNumber}?

Let us know how we're doing:
{FeedbackLink}

We're here for anything you need!";
    }

    private static string GetDefaultMidStayTemplate()
    {
        return @"Hi {GuestFirstName}!

How's your stay going so far? We hope you're enjoying {HotelName}!

Need anything? Just message us here or call the front desk.

Have a wonderful day!";
    }

    private static string GetDefaultPreCheckoutTemplate()
    {
        return @"Hi {GuestFirstName}!

Your checkout is tomorrow. We hope you've had a wonderful stay!

Need a late checkout? We'll do our best to accommodate you - just let us know!

Thanks for staying with us at {HotelName}!";
    }

    private static string GetDefaultPostStayTemplate()
    {
        return @"Hi {GuestFirstName}!

Thank you for staying with us at {HotelName}!

We'd love to hear about your experience:
{FeedbackLink}

We hope to see you again soon!";
    }

    #endregion
}

#region DTOs

public class GuestJourneySettingsDto
{
    // Pre-Arrival
    public bool PreArrivalEnabled { get; set; }
    public int PreArrivalDaysBefore { get; set; }
    public string PreArrivalTime { get; set; } = "10:00";
    public string? PreArrivalTemplate { get; set; }

    // Check-in Day
    public bool CheckinDayEnabled { get; set; }
    public string CheckinDayTime { get; set; } = "09:00";
    public string? CheckinDayTemplate { get; set; }

    // Welcome Settled
    public bool WelcomeSettledEnabled { get; set; }
    public int WelcomeSettledHoursAfter { get; set; }
    public string? WelcomeSettledTemplate { get; set; }

    // Mid-Stay
    public bool MidStayEnabled { get; set; }
    public string MidStayTime { get; set; } = "10:00";
    public string? MidStayTemplate { get; set; }

    // Pre-Checkout
    public bool PreCheckoutEnabled { get; set; }
    public string PreCheckoutTime { get; set; } = "18:00";
    public string? PreCheckoutTemplate { get; set; }

    // Post-Stay
    public bool PostStayEnabled { get; set; }
    public string PostStayTime { get; set; } = "10:00";
    public string? PostStayTemplate { get; set; }

    // Media & Other
    public string? WelcomeImageUrl { get; set; }
    public bool IncludePhotoInWelcome { get; set; }
    public string? Timezone { get; set; }
}

public class PreviewTemplateRequest
{
    public string Template { get; set; } = string.Empty;
}

#endregion
