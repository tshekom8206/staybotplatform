using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Hostr.Api.Data;
using Hostr.Api.Models;
using Hostr.Api.Services;

namespace Hostr.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class CheckinController : ControllerBase
{
    private readonly HostrDbContext _context;
    private readonly IProactiveMessageService _proactiveMessageService;

    public CheckinController(HostrDbContext context, IProactiveMessageService proactiveMessageService)
    {
        _context = context;
        _proactiveMessageService = proactiveMessageService;
    }

    /// <summary>
    /// Get today's check-ins with filtering options
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetCheckins(
        [FromQuery] DateTime? date = null,
        [FromQuery] string? status = null,
        [FromQuery] string? timeFilter = null,
        [FromQuery] string? search = null)
    {
        try
        {
            var tenantId = HttpContext.Items["TenantId"] as int?;
            if (!tenantId.HasValue)
            {
                return BadRequest("Tenant context not found");
            }

            var targetDate = date?.Date ?? DateTime.Today;

            var query = _context.Bookings
                .Where(b => b.TenantId == tenantId.Value &&
                           b.CheckinDate == DateOnly.FromDateTime(targetDate))
                .AsQueryable();

            // Apply status filter
            if (!string.IsNullOrEmpty(status) && status != "all")
            {
                var checkinStatus = MapBookingStatusToCheckinStatus(status);
                if (!string.IsNullOrEmpty(checkinStatus))
                {
                    query = query.Where(b => b.Status == checkinStatus);
                }
            }

            // Apply search filter
            if (!string.IsNullOrEmpty(search))
            {
                var searchLower = search.ToLower();
                query = query.Where(b =>
                    b.GuestName.ToLower().Contains(searchLower) ||
                    b.Phone.Contains(search) ||
                    (b.RoomNumber != null && b.RoomNumber.ToLower().Contains(searchLower)));
            }

            var bookings = await query
                .OrderBy(b => b.CheckinDate)
                .ThenBy(b => b.CreatedAt)
                .ToListAsync();

            var checkins = bookings.Select(booking => new CheckinSummary
            {
                Id = booking.Id,
                GuestName = booking.GuestName,
                PhoneNumber = booking.Phone,
                RoomNumber = booking.RoomNumber ?? "TBD",
                ExpectedArrival = DateTime.Today.AddHours(14), // Default check-in time 2 PM
                ActualArrival = booking.Status == "CheckedIn" ? DateTime.Now : null,
                Status = MapBookingStatusToCheckinStatus(booking.Status),
                SpecialRequests = null, // TODO: Add special requests field to Booking model
                NumberOfGuests = 2 // TODO: Add number of guests field to Booking model
            }).ToList();

            // Apply time filter
            if (!string.IsNullOrEmpty(timeFilter) && timeFilter != "all")
            {
                checkins = ApplyTimeFilter(checkins, timeFilter);
            }

            return Ok(new { data = checkins });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Failed to load check-ins", error = ex.Message });
        }
    }

    /// <summary>
    /// Update check-in status
    /// </summary>
    [HttpPatch("{id}/status")]
    public async Task<IActionResult> UpdateCheckinStatus(int id, [FromBody] UpdateStatusRequest request)
    {
        try
        {
            var tenantId = HttpContext.Items["TenantId"] as int?;
            if (!tenantId.HasValue)
            {
                return BadRequest("Tenant context not found");
            }

            var booking = await _context.Bookings
                .FirstOrDefaultAsync(b => b.Id == id && b.TenantId == tenantId.Value);

            if (booking == null)
            {
                return NotFound("Check-in not found");
            }

            // Map check-in status to booking status
            var bookingStatus = MapCheckinStatusToBookingStatus(request.Status);
            if (string.IsNullOrEmpty(bookingStatus))
            {
                return BadRequest("Invalid status");
            }

            var wasNotCheckedIn = booking.Status != "CheckedIn";
            booking.Status = bookingStatus;

            // If marking as completed (checked in), record the actual time and schedule welcome message
            if (request.Status == "completed" && wasNotCheckedIn)
            {
                // Record actual check-in time
                booking.CheckInDate = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                // Schedule Welcome Settled message (3 hours after check-in by default)
                await _proactiveMessageService.ScheduleWelcomeSettledAsync(booking);
            }
            else
            {
                await _context.SaveChangesAsync();
            }

            return Ok(new { message = "Check-in status updated successfully" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Failed to update check-in status", error = ex.Message });
        }
    }

    private static string MapBookingStatusToCheckinStatus(string bookingStatus)
    {
        return bookingStatus switch
        {
            "Confirmed" => "pending",
            "CheckedIn" => "completed",
            "CheckedOut" => "completed",
            "Cancelled" => "no-show",
            _ => "pending"
        };
    }

    private static string MapCheckinStatusToBookingStatus(string checkinStatus)
    {
        return checkinStatus switch
        {
            "pending" => "Confirmed",
            "in-progress" => "Confirmed",
            "completed" => "CheckedIn",
            "no-show" => "Cancelled",
            _ => "Confirmed"
        };
    }

    private static List<CheckinSummary> ApplyTimeFilter(List<CheckinSummary> checkins, string timeFilter)
    {
        var now = DateTime.Now;

        return timeFilter switch
        {
            "overdue" => checkins.Where(c => c.Status == "pending" && c.ExpectedArrival < now).ToList(),
            "upcoming" => checkins.Where(c => c.ExpectedArrival > now).ToList(),
            "today" => checkins.Where(c => c.ExpectedArrival.Date == DateTime.Today).ToList(),
            _ => checkins
        };
    }
}

public class UpdateStatusRequest
{
    public string Status { get; set; } = string.Empty;
}

public class CheckinSummary
{
    public int Id { get; set; }
    public string GuestName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string RoomNumber { get; set; } = string.Empty;
    public DateTime ExpectedArrival { get; set; }
    public DateTime? ActualArrival { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? SpecialRequests { get; set; }
    public int NumberOfGuests { get; set; }
}