using Hostr.Api.Data;
using Hostr.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Hostr.Api.Services;

public class RoomValidationResult
{
    public bool IsValid { get; set; }
    public string? RoomNumber { get; set; }
    public bool IsVerified { get; set; }  // true if matched to booking
    public int? BookingId { get; set; }
    public string? GuestName { get; set; }
    public string? ErrorMessage { get; set; }
}

public interface IRoomValidationService
{
    /// <summary>
    /// Validates and resolves room number for a guest push subscription.
    /// First tries to find active booking by phone, then validates against valid rooms list.
    /// </summary>
    Task<RoomValidationResult> ValidateAndResolveRoom(int tenantId, string? phone, string? roomNumber);

    /// <summary>
    /// Checks if a room number is in the tenant's valid rooms list.
    /// </summary>
    Task<bool> IsValidRoom(int tenantId, string roomNumber);

    /// <summary>
    /// Gets the list of valid room numbers for a tenant.
    /// </summary>
    Task<List<string>> GetValidRooms(int tenantId);
}

public class RoomValidationService : IRoomValidationService
{
    private readonly HostrDbContext _context;
    private readonly ILogger<RoomValidationService> _logger;

    public RoomValidationService(HostrDbContext context, ILogger<RoomValidationService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<RoomValidationResult> ValidateAndResolveRoom(int tenantId, string? phone, string? roomNumber)
    {
        var normalizedPhone = !string.IsNullOrWhiteSpace(phone) ? NormalizePhoneNumber(phone) : null;
        var normalizedRoom = !string.IsNullOrWhiteSpace(roomNumber) ? roomNumber.Trim() : null;

        // Need at least phone or room number
        if (string.IsNullOrWhiteSpace(normalizedPhone) && string.IsNullOrWhiteSpace(normalizedRoom))
        {
            return new RoomValidationResult
            {
                IsValid = false,
                ErrorMessage = "Please enter your room number to enable notifications"
            };
        }

        // Step 1: If phone provided, try to find active booking
        if (!string.IsNullOrWhiteSpace(normalizedPhone))
        {
            var booking = await _context.Bookings
                .Where(b => b.TenantId == tenantId &&
                           b.Phone == normalizedPhone &&
                           (b.Status == "CheckedIn" || b.Status == "Confirmed"))
                .OrderByDescending(b => b.CheckinDate)
                .FirstOrDefaultAsync();

            if (booking != null && !string.IsNullOrWhiteSpace(booking.RoomNumber))
            {
                _logger.LogInformation(
                    "Found active booking for phone {Phone}: Room {Room}, Guest {Guest}",
                    normalizedPhone, booking.RoomNumber, booking.GuestName);

                return new RoomValidationResult
                {
                    IsValid = true,
                    RoomNumber = booking.RoomNumber,
                    IsVerified = true,
                    BookingId = booking.Id,
                    GuestName = booking.GuestName
                };
            }
        }

        // Step 2: Validate room number if provided
        if (!string.IsNullOrWhiteSpace(normalizedRoom))
        {
            // Check if room is valid
            var isValidRoom = await IsValidRoom(tenantId, normalizedRoom);

            if (!isValidRoom)
            {
                _logger.LogWarning(
                    "Invalid room number {Room} for tenant {TenantId}",
                    normalizedRoom, tenantId);

                return new RoomValidationResult
                {
                    IsValid = false,
                    ErrorMessage = $"Room {normalizedRoom} is not a valid room at this property"
                };
            }

            // Valid room - allow subscription (unverified walk-in)
            _logger.LogInformation(
                "Guest subscription by room: Phone {Phone}, Room {Room} (unverified)",
                normalizedPhone ?? "not provided", normalizedRoom);

            return new RoomValidationResult
            {
                IsValid = true,
                RoomNumber = normalizedRoom,
                IsVerified = false,
                BookingId = null
            };
        }

        // Phone provided but no booking found, and no room number given
        return new RoomValidationResult
        {
            IsValid = false,
            ErrorMessage = "No booking found for this phone number. Please enter your room number."
        };
    }

    public async Task<bool> IsValidRoom(int tenantId, string roomNumber)
    {
        var validRooms = await GetValidRooms(tenantId);

        if (!validRooms.Any())
        {
            // If no valid rooms configured, allow any room (legacy behavior)
            _logger.LogWarning("No valid rooms configured for tenant {TenantId}, allowing any room", tenantId);
            return true;
        }

        // Case-insensitive comparison
        var normalizedRoom = roomNumber.Trim().ToUpperInvariant();
        return validRooms.Any(r => r.ToUpperInvariant() == normalizedRoom);
    }

    public async Task<List<string>> GetValidRooms(int tenantId)
    {
        var tenant = await _context.Tenants
            .AsNoTracking()
            .Where(t => t.Id == tenantId)
            .Select(t => t.ValidRooms)
            .FirstOrDefaultAsync();

        if (string.IsNullOrWhiteSpace(tenant))
        {
            return new List<string>();
        }

        // Parse comma-separated list, trim each value
        return tenant
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(r => r.Trim())
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .ToList();
    }

    private string NormalizePhoneNumber(string phone)
    {
        // Remove spaces and common formatting
        var normalized = phone.Trim().Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "");

        // Ensure it starts with + if it doesn't
        if (!normalized.StartsWith("+") && normalized.Length > 9)
        {
            // Assume South African number if starts with 0
            if (normalized.StartsWith("0"))
            {
                normalized = "+27" + normalized.Substring(1);
            }
        }

        return normalized;
    }
}
