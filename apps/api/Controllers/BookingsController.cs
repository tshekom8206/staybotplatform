using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Hostr.Api.Services;
using Hostr.Api.Models;

namespace Hostr.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BookingsController : ControllerBase
{
    private readonly IBookingManagementService _bookingService;
    private readonly ILogger<BookingsController> _logger;

    public BookingsController(
        IBookingManagementService bookingService,
        ILogger<BookingsController> logger)
    {
        _bookingService = bookingService;
        _logger = logger;
    }

    /// <summary>
    /// Get all bookings with filtering and pagination
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetBookings(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] string? search = null,
        [FromQuery] string? checkinFrom = null,
        [FromQuery] string? checkinTo = null,
        [FromQuery] string? checkoutFrom = null,
        [FromQuery] string? checkoutTo = null,
        [FromQuery] string? source = null)
    {
        try
        {
            var tenantId = HttpContext.Items["TenantId"] as int?;
            if (!tenantId.HasValue)
            {
                return BadRequest(new { message = "Tenant context not found" });
            }

            // Parse date strings if provided
            DateOnly? checkinFromDate = null;
            DateOnly? checkinToDate = null;
            DateOnly? checkoutFromDate = null;
            DateOnly? checkoutToDate = null;

            if (!string.IsNullOrWhiteSpace(checkinFrom) && DateOnly.TryParse(checkinFrom, out var cf))
            {
                checkinFromDate = cf;
            }

            if (!string.IsNullOrWhiteSpace(checkinTo) && DateOnly.TryParse(checkinTo, out var ct))
            {
                checkinToDate = ct;
            }

            if (!string.IsNullOrWhiteSpace(checkoutFrom) && DateOnly.TryParse(checkoutFrom, out var cof))
            {
                checkoutFromDate = cof;
            }

            if (!string.IsNullOrWhiteSpace(checkoutTo) && DateOnly.TryParse(checkoutTo, out var cot))
            {
                checkoutToDate = cot;
            }

            var (bookings, totalCount) = await _bookingService.GetBookingsAsync(
                tenantId.Value,
                page,
                pageSize,
                status,
                search,
                checkinFromDate,
                checkinToDate,
                checkoutFromDate,
                checkoutToDate,
                source);

            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            return Ok(new
            {
                data = bookings,
                pagination = new
                {
                    currentPage = page,
                    pageSize,
                    totalCount,
                    totalPages
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving bookings");
            return StatusCode(500, new { message = "Failed to retrieve bookings", error = ex.Message });
        }
    }

    /// <summary>
    /// Get a single booking by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetBooking(int id)
    {
        try
        {
            var tenantId = HttpContext.Items["TenantId"] as int?;
            if (!tenantId.HasValue)
            {
                return BadRequest(new { message = "Tenant context not found" });
            }

            var booking = await _bookingService.GetBookingByIdAsync(tenantId.Value, id);
            if (booking == null)
            {
                return NotFound(new { message = $"Booking {id} not found" });
            }

            return Ok(booking);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving booking {BookingId}", id);
            return StatusCode(500, new { message = "Failed to retrieve booking", error = ex.Message });
        }
    }

    /// <summary>
    /// Create a new booking
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateBooking([FromBody] CreateBookingRequest request)
    {
        try
        {
            var tenantId = HttpContext.Items["TenantId"] as int?;
            if (!tenantId.HasValue)
            {
                return BadRequest(new { message = "Tenant context not found" });
            }

            var booking = await _bookingService.CreateBookingAsync(tenantId.Value, request);

            return CreatedAtAction(
                nameof(GetBooking),
                new { id = booking.Id },
                booking);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating booking");
            return StatusCode(500, new { message = "Failed to create booking", error = ex.Message });
        }
    }

    /// <summary>
    /// Update an existing booking
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateBooking(int id, [FromBody] UpdateBookingRequest request)
    {
        try
        {
            var tenantId = HttpContext.Items["TenantId"] as int?;
            if (!tenantId.HasValue)
            {
                return BadRequest(new { message = "Tenant context not found" });
            }

            var booking = await _bookingService.UpdateBookingAsync(tenantId.Value, id, request);

            return Ok(booking);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating booking {BookingId}", id);
            return StatusCode(500, new { message = "Failed to update booking", error = ex.Message });
        }
    }

    /// <summary>
    /// Cancel a booking
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> CancelBooking(int id)
    {
        try
        {
            var tenantId = HttpContext.Items["TenantId"] as int?;
            if (!tenantId.HasValue)
            {
                return BadRequest(new { message = "Tenant context not found" });
            }

            var success = await _bookingService.CancelBookingAsync(tenantId.Value, id);
            if (!success)
            {
                return NotFound(new { message = $"Booking {id} not found" });
            }

            return Ok(new { message = "Booking cancelled successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling booking {BookingId}", id);
            return StatusCode(500, new { message = "Failed to cancel booking", error = ex.Message });
        }
    }

    /// <summary>
    /// Get booking statistics
    /// </summary>
    [HttpGet("statistics")]
    public async Task<IActionResult> GetStatistics()
    {
        try
        {
            var tenantId = HttpContext.Items["TenantId"] as int?;
            if (!tenantId.HasValue)
            {
                return BadRequest(new { message = "Tenant context not found" });
            }

            var statistics = await _bookingService.GetBookingStatisticsAsync(tenantId.Value);

            return Ok(statistics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving booking statistics");
            return StatusCode(500, new { message = "Failed to retrieve statistics", error = ex.Message });
        }
    }

    /// <summary>
    /// Get available rooms for a date range
    /// </summary>
    [HttpGet("available-rooms")]
    public async Task<IActionResult> GetAvailableRooms(
        [FromQuery] string checkinDate,
        [FromQuery] string checkoutDate,
        [FromQuery] int? excludeBookingId = null)
    {
        try
        {
            var tenantId = HttpContext.Items["TenantId"] as int?;
            if (!tenantId.HasValue)
            {
                return BadRequest(new { message = "Tenant context not found" });
            }

            if (!DateOnly.TryParse(checkinDate, out var checkin))
            {
                return BadRequest(new { message = "Invalid checkin date format" });
            }

            if (!DateOnly.TryParse(checkoutDate, out var checkout))
            {
                return BadRequest(new { message = "Invalid checkout date format" });
            }

            if (checkout <= checkin)
            {
                return BadRequest(new { message = "Checkout date must be after checkin date" });
            }

            var availableRooms = await _bookingService.GetAvailableRoomsAsync(
                tenantId.Value,
                checkin,
                checkout,
                excludeBookingId);

            return Ok(new { rooms = availableRooms, count = availableRooms.Count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving available rooms");
            return StatusCode(500, new { message = "Failed to retrieve available rooms", error = ex.Message });
        }
    }

    /// <summary>
    /// Check if a specific room is available for a date range
    /// </summary>
    [HttpGet("check-availability")]
    public async Task<IActionResult> CheckRoomAvailability(
        [FromQuery] string roomNumber,
        [FromQuery] string checkinDate,
        [FromQuery] string checkoutDate,
        [FromQuery] int? excludeBookingId = null)
    {
        try
        {
            var tenantId = HttpContext.Items["TenantId"] as int?;
            if (!tenantId.HasValue)
            {
                return BadRequest(new { message = "Tenant context not found" });
            }

            if (string.IsNullOrWhiteSpace(roomNumber))
            {
                return BadRequest(new { message = "Room number is required" });
            }

            if (!DateOnly.TryParse(checkinDate, out var checkin))
            {
                return BadRequest(new { message = "Invalid checkin date format" });
            }

            if (!DateOnly.TryParse(checkoutDate, out var checkout))
            {
                return BadRequest(new { message = "Invalid checkout date format" });
            }

            var isAvailable = await _bookingService.IsRoomAvailableAsync(
                tenantId.Value,
                roomNumber,
                checkin,
                checkout,
                excludeBookingId);

            return Ok(new { roomNumber, isAvailable });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking room availability");
            return StatusCode(500, new { message = "Failed to check availability", error = ex.Message });
        }
    }
}
