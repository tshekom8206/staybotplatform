using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Hostr.Api.Data;
using Hostr.Api.Models;
using Hostr.Api.Middleware;
using Hostr.Api.Hubs;
using Hostr.Api.Services;
using Microsoft.AspNetCore.SignalR;
using System.Text.Json;

namespace Hostr.Api.Controllers;

[ApiController]
[Route("api/room-preferences")]
public class RoomPreferencesController : ControllerBase
{
    private readonly HostrDbContext _context;
    private readonly ILogger<RoomPreferencesController> _logger;
    private readonly IHubContext<StaffTaskHub> _staffTaskHub;
    private readonly IWhatsAppService _whatsAppService;

    public RoomPreferencesController(
        HostrDbContext context,
        ILogger<RoomPreferencesController> logger,
        IHubContext<StaffTaskHub> staffTaskHub,
        IWhatsAppService whatsAppService)
    {
        _context = context;
        _logger = logger;
        _staffTaskHub = staffTaskHub;
        _whatsAppService = whatsAppService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<RoomPreferenceResponse>>> GetPreferences(
        [FromQuery] string? roomNumber = null,
        [FromQuery] string? status = null)
    {
        var tenantId = HttpContext.Items["TenantId"] as int? ?? 0;
        var phoneNumber = HttpContext.Items["PhoneNumber"] as string;

        if (tenantId == 0)
        {
            return Unauthorized(new { message = "Tenant not identified" });
        }

        var query = _context.RoomPreferences
            .Include(rp => rp.AcknowledgedByUser)
            .Where(rp => rp.TenantId == tenantId);

        // If guest is accessing, filter by their booking
        if (!string.IsNullOrEmpty(phoneNumber))
        {
            var booking = await _context.Bookings
                .Where(b => b.TenantId == tenantId && b.Phone == phoneNumber)
                .OrderByDescending(b => b.CheckinDate)
                .FirstOrDefaultAsync();

            if (booking == null)
            {
                return Ok(new List<RoomPreferenceResponse>());
            }

            query = query.Where(rp => rp.BookingId == booking.Id || rp.RoomNumber == booking.RoomNumber);
        }

        if (!string.IsNullOrEmpty(roomNumber))
        {
            query = query.Where(rp => rp.RoomNumber == roomNumber);
        }

        if (!string.IsNullOrEmpty(status))
        {
            query = query.Where(rp => rp.Status == status);
        }

        var preferences = await query
            .OrderByDescending(rp => rp.CreatedAt)
            .ToListAsync();

        var response = preferences.Select(rp => new RoomPreferenceResponse
        {
            Id = rp.Id,
            PreferenceType = rp.PreferenceType,
            PreferenceValue = rp.PreferenceValue,
            Notes = rp.Notes,
            Status = rp.Status,
            AcknowledgedAt = rp.AcknowledgedAt,
            AcknowledgedByName = rp.AcknowledgedByUser?.UserName,
            CreatedAt = rp.CreatedAt,
            UpdatedAt = rp.UpdatedAt
        });

        return Ok(response);
    }

    [HttpPost]
    public async Task<ActionResult<RoomPreferenceResponse>> CreateOrUpdatePreference(
        [FromBody] CreateRoomPreferenceRequest request)
    {
        var tenantId = HttpContext.Items["TenantId"] as int? ?? 0;
        var phoneNumber = HttpContext.Items["PhoneNumber"] as string;

        if (tenantId == 0)
        {
            return Unauthorized(new { message = "Tenant not identified" });
        }

        // Get guest's booking
        var booking = await _context.Bookings
            .Where(b => b.TenantId == tenantId && b.Phone == phoneNumber)
            .OrderByDescending(b => b.CheckinDate)
            .FirstOrDefaultAsync();

        if (booking == null)
        {
            return BadRequest(new { message = "No active booking found" });
        }

        // Check if preference already exists
        var existingPreference = await _context.RoomPreferences
            .Where(rp => rp.TenantId == tenantId 
                && rp.BookingId == booking.Id 
                && rp.PreferenceType == request.PreferenceType)
            .FirstOrDefaultAsync();

        RoomPreference preference;
        string action;

        if (existingPreference != null)
        {
            // Update existing preference
            var oldValue = existingPreference.PreferenceValue;
            existingPreference.PreferenceValue = request.PreferenceValue;
            existingPreference.Notes = request.Notes;
            existingPreference.UpdatedAt = DateTime.UtcNow;
            existingPreference.Status = "Active"; // Reset to Active if it was acknowledged

            preference = existingPreference;
            action = "updated";

            // Create history record
            _context.RoomPreferenceHistory.Add(new RoomPreferenceHistory
            {
                TenantId = tenantId,
                RoomPreferenceId = preference.Id,
                Action = "updated",
                OldValue = oldValue,
                NewValue = request.PreferenceValue,
                CreatedAt = DateTime.UtcNow
            });
        }
        else
        {
            // Create new preference
            preference = new RoomPreference
            {
                TenantId = tenantId,
                BookingId = booking.Id,
                RoomNumber = booking.RoomNumber ?? "Unknown",
                PreferenceType = request.PreferenceType,
                PreferenceValue = request.PreferenceValue,
                Notes = request.Notes,
                Status = "Active",
                ExpiresAt = booking.CheckoutDate.ToDateTime(TimeOnly.MinValue).AddDays(1),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.RoomPreferences.Add(preference);
            action = "created";

            // Create history record
            _context.RoomPreferenceHistory.Add(new RoomPreferenceHistory
            {
                TenantId = tenantId,
                RoomPreferenceId = preference.Id,
                Action = "created",
                NewValue = request.PreferenceValue,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync();

        // Send SignalR notification to staff
        try
        {
            await _staffTaskHub.Clients.Group($"tenant_{tenantId}")
                .SendAsync("PreferenceCreated", new
                {
                    preferenceId = preference.Id,
                    roomNumber = preference.RoomNumber,
                    preferenceType = preference.PreferenceType,
                    action = action,
                    guestPhone = phoneNumber
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send SignalR notification for preference {PreferenceId}", preference.Id);
        }

        // Send WhatsApp confirmation to guest
        if (!string.IsNullOrEmpty(phoneNumber))
        {
            try
            {
                var confirmationMessage = FormatPreferenceConfirmationMessage(preference, booking.GuestName, action);
                await _whatsAppService.SendTextMessageAsync(tenantId, phoneNumber, confirmationMessage);
                _logger.LogInformation("Sent WhatsApp confirmation for preference {PreferenceId} to {Phone}", preference.Id, phoneNumber);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send WhatsApp confirmation for preference {PreferenceId}", preference.Id);
            }
        }

        var response = new RoomPreferenceResponse
        {
            Id = preference.Id,
            PreferenceType = preference.PreferenceType,
            PreferenceValue = preference.PreferenceValue,
            Notes = preference.Notes,
            Status = preference.Status,
            AcknowledgedAt = preference.AcknowledgedAt,
            CreatedAt = preference.CreatedAt,
            UpdatedAt = preference.UpdatedAt
        };

        return Ok(response);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> CancelPreference(int id)
    {
        var tenantId = HttpContext.Items["TenantId"] as int? ?? 0;
        var phoneNumber = HttpContext.Items["PhoneNumber"] as string;

        if (tenantId == 0)
        {
            return Unauthorized(new { message = "Tenant not identified" });
        }

        var preference = await _context.RoomPreferences
            .Where(rp => rp.TenantId == tenantId && rp.Id == id)
            .FirstOrDefaultAsync();

        if (preference == null)
        {
            return NotFound(new { message = "Preference not found" });
        }

        // Verify guest owns this preference
        if (!string.IsNullOrEmpty(phoneNumber))
        {
            var booking = await _context.Bookings
                .Where(b => b.Id == preference.BookingId && b.Phone == phoneNumber)
                .FirstOrDefaultAsync();

            if (booking == null)
            {
                return Forbid();
            }
        }

        preference.Status = "Cancelled";
        preference.UpdatedAt = DateTime.UtcNow;

        // Create history record
        _context.RoomPreferenceHistory.Add(new RoomPreferenceHistory
        {
            TenantId = tenantId,
            RoomPreferenceId = preference.Id,
            Action = "cancelled",
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();

        // Send SignalR notification
        try
        {
            await _staffTaskHub.Clients.Group($"tenant_{tenantId}")
                .SendAsync("PreferenceCancelled", new
                {
                    preferenceId = preference.Id,
                    roomNumber = preference.RoomNumber
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send SignalR notification for cancelled preference {PreferenceId}", id);
        }

        return NoContent();
    }

    [HttpPost("{id}/acknowledge")]
    public async Task<IActionResult> AcknowledgePreference(int id)
    {
        var tenantId = HttpContext.Items["TenantId"] as int? ?? 0;
        var userId = HttpContext.Items["UserId"] as int?;

        if (tenantId == 0)
        {
            return Unauthorized(new { message = "Tenant not identified" });
        }

        var preference = await _context.RoomPreferences
            .Include(rp => rp.Booking)
            .Include(rp => rp.AcknowledgedByUser)
            .Where(rp => rp.TenantId == tenantId && rp.Id == id)
            .FirstOrDefaultAsync();

        if (preference == null)
        {
            return NotFound(new { message = "Preference not found" });
        }

        var staffUser = await _context.Users.FindAsync(userId);
        var staffName = staffUser?.UserName ?? "Our team";

        preference.Status = "Acknowledged";
        preference.AcknowledgedBy = userId;
        preference.AcknowledgedAt = DateTime.UtcNow;
        preference.UpdatedAt = DateTime.UtcNow;

        // Create history record
        _context.RoomPreferenceHistory.Add(new RoomPreferenceHistory
        {
            TenantId = tenantId,
            RoomPreferenceId = preference.Id,
            Action = "acknowledged",
            ChangedBy = userId,
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();

        // Send WhatsApp acknowledgement to guest
        if (preference.Booking != null && !string.IsNullOrEmpty(preference.Booking.Phone))
        {
            try
            {
                var acknowledgementMessage = FormatAcknowledgementMessage(preference, preference.Booking.GuestName, staffName);
                await _whatsAppService.SendTextMessageAsync(tenantId, preference.Booking.Phone, acknowledgementMessage);
                _logger.LogInformation("Sent WhatsApp acknowledgement for preference {PreferenceId} to {Phone}", preference.Id, preference.Booking.Phone);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send WhatsApp acknowledgement for preference {PreferenceId}", preference.Id);
            }
        }

        return Ok(new { message = "Preference acknowledged successfully" });
    }

    [HttpGet("staff/all")]
    public async Task<ActionResult<IEnumerable<StaffRoomPreferenceView>>> GetAllPreferencesForStaff(
        [FromQuery] string? status = null,
        [FromQuery] string? roomNumber = null)
    {
        var tenantId = HttpContext.Items["TenantId"] as int? ?? 0;

        if (tenantId == 0)
        {
            return Unauthorized(new { message = "Tenant not identified" });
        }

        var query = _context.RoomPreferences
            .Include(rp => rp.Booking)
            .Include(rp => rp.AcknowledgedByUser)
            .Where(rp => rp.TenantId == tenantId);

        if (!string.IsNullOrEmpty(status))
        {
            query = query.Where(rp => rp.Status == status);
        }

        if (!string.IsNullOrEmpty(roomNumber))
        {
            query = query.Where(rp => rp.RoomNumber == roomNumber);
        }

        var preferences = await query
            .OrderBy(rp => rp.RoomNumber)
            .ThenByDescending(rp => rp.CreatedAt)
            .ToListAsync();

        var response = preferences.Select(rp => new StaffRoomPreferenceView
        {
            Id = rp.Id,
            RoomNumber = rp.RoomNumber,
            GuestName = rp.Booking?.GuestName ?? "Unknown",
            PreferenceType = rp.PreferenceType,
            PreferenceLabel = GetPreferenceLabel(rp.PreferenceType),
            PreferenceDetails = GetPreferenceDetails(rp.PreferenceType, rp.PreferenceValue),
            Notes = rp.Notes,
            Status = rp.Status,
            AcknowledgedByName = rp.AcknowledgedByUser?.UserName,
            AcknowledgedAt = rp.AcknowledgedAt,
            CreatedAt = rp.CreatedAt,
            ExpiresAt = rp.ExpiresAt
        });

        return Ok(response);
    }

    private string FormatPreferenceConfirmationMessage(RoomPreference preference, string guestName, string action)
    {
        var preferenceLabel = GetPreferenceLabel(preference.PreferenceType);
        var preferenceDetails = GetPreferenceDetails(preference.PreferenceType, preference.PreferenceValue);
        
        var actionText = action == "created" ? "received" : "updated";
        
        var message = $"Hi {guestName},\n\n";
        message += $"✅ We've {actionText} your housekeeping preference:\n\n";
        message += $"🏠 Room: {preference.RoomNumber}\n";
        message += $"📋 Preference: {preferenceLabel}\n";
        message += $"ℹ️ Details: {preferenceDetails}\n";
        
        if (!string.IsNullOrEmpty(preference.Notes))
        {
            message += $"📝 Note: {preference.Notes}\n";
        }
        
        message += $"\nOur housekeeping team has been notified and will ensure your preferences are followed during your stay.\n\n";
        message += "If you need to make any changes, simply visit the Housekeeping section in your guest portal.";
        
        return message;
    }

    private string FormatAcknowledgementMessage(RoomPreference preference, string guestName, string staffName)
    {
        var preferenceLabel = GetPreferenceLabel(preference.PreferenceType);
        var preferenceDetails = GetPreferenceDetails(preference.PreferenceType, preference.PreferenceValue);
        
        var message = $"Hi {guestName},\n\n";
        message += $"👍 {staffName} has acknowledged your housekeeping preference:\n\n";
        message += $"🏠 Room: {preference.RoomNumber}\n";
        message += $"📋 {preferenceLabel}\n";
        message += $"ℹ️ {preferenceDetails}\n\n";
        message += "Your preference is now confirmed and will be followed by our housekeeping team.\n\n";
        message += "Thank you for helping us personalize your stay!";
        
        return message;
    }

    private string GetPreferenceLabel(string preferenceType)
    {
        return preferenceType switch
        {
            "aircon_after_cleaning" => "Air Conditioning After Cleaning",
            "linen_change_frequency" => "Linen Change Frequency",
            "towel_change_frequency" => "Towel Change Frequency",
            "dnd_schedule" => "Do Not Disturb Schedule",
            "temperature_preference" => "Temperature Preference",
            _ => preferenceType
        };
    }

    private string GetPreferenceDetails(string preferenceType, JsonDocument value)
    {
        try
        {
            var root = value.RootElement;
            
            return preferenceType switch
            {
                "aircon_after_cleaning" => root.TryGetProperty("enabled", out var enabled) && enabled.GetBoolean() 
                    ? "Keep aircon ON after cleaning" 
                    : "Keep aircon OFF after cleaning",
                "linen_change_frequency" => root.TryGetProperty("daily", out var dailyLinen) && dailyLinen.GetBoolean() 
                    ? "Change daily" 
                    : "Change every 2-3 days",
                "towel_change_frequency" => root.TryGetProperty("daily", out var dailyTowel) && dailyTowel.GetBoolean() 
                    ? "Change daily" 
                    : "Change every 2-3 days",
                "dnd_schedule" => root.TryGetProperty("until", out var until) 
                    ? $"Do not disturb until {until.GetString()}" 
                    : "Do not disturb",
                "temperature_preference" => root.TryGetProperty("celsius", out var temp) 
                    ? $"Set to {temp.GetInt32()}°C" 
                    : "Custom temperature",
                _ => "Custom preference"
            };
        }
        catch
        {
            return "Custom preference";
        }
    }
}
