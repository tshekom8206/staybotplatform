using Microsoft.EntityFrameworkCore;
using Hostr.Api.Data;
using Hostr.Api.Models;

namespace Hostr.Api.Services;

public class BookingManagementService : IBookingManagementService
{
    private readonly HostrDbContext _context;
    private readonly ILogger<BookingManagementService> _logger;
    private readonly IWhatsAppService _whatsAppService;

    public BookingManagementService(
        HostrDbContext context,
        ILogger<BookingManagementService> logger,
        IWhatsAppService whatsAppService)
    {
        _context = context;
        _logger = logger;
        _whatsAppService = whatsAppService;
    }

    public async Task<(List<Booking> bookings, int totalCount)> GetBookingsAsync(
        int tenantId,
        int page = 1,
        int pageSize = 20,
        string? status = null,
        string? search = null,
        DateOnly? checkinFrom = null,
        DateOnly? checkinTo = null,
        DateOnly? checkoutFrom = null,
        DateOnly? checkoutTo = null,
        string? source = null)
    {
        var query = _context.Bookings
            .Where(b => b.TenantId == tenantId);

        // Apply filters
        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(b => b.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.ToLower();
            query = query.Where(b =>
                b.GuestName.ToLower().Contains(searchLower) ||
                b.Phone.Contains(search) ||
                (b.Email != null && b.Email.ToLower().Contains(searchLower)) ||
                (b.RoomNumber != null && b.RoomNumber.Contains(search)));
        }

        if (checkinFrom.HasValue)
        {
            query = query.Where(b => b.CheckinDate >= checkinFrom.Value);
        }

        if (checkinTo.HasValue)
        {
            query = query.Where(b => b.CheckinDate <= checkinTo.Value);
        }

        if (checkoutFrom.HasValue)
        {
            query = query.Where(b => b.CheckoutDate >= checkoutFrom.Value);
        }

        if (checkoutTo.HasValue)
        {
            query = query.Where(b => b.CheckoutDate <= checkoutTo.Value);
        }

        if (!string.IsNullOrWhiteSpace(source))
        {
            query = query.Where(b => b.Source == source);
        }

        // Get total count before pagination
        var totalCount = await query.CountAsync();

        // Apply pagination and ordering
        var bookings = await query
            .OrderByDescending(b => b.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (bookings, totalCount);
    }

    public async Task<Booking?> GetBookingByIdAsync(int tenantId, int bookingId)
    {
        return await _context.Bookings
            .FirstOrDefaultAsync(b => b.TenantId == tenantId && b.Id == bookingId);
    }

    public async Task<Booking> CreateBookingAsync(int tenantId, CreateBookingRequest request)
    {
        // Validate dates
        if (request.CheckoutDate <= request.CheckinDate)
        {
            throw new ArgumentException("Checkout date must be after checkin date");
        }

        // Check room availability
        var isAvailable = await IsRoomAvailableAsync(tenantId, request.RoomNumber, request.CheckinDate, request.CheckoutDate);
        if (!isAvailable)
        {
            throw new InvalidOperationException($"Room {request.RoomNumber} is not available for the selected dates");
        }

        // Calculate total nights
        var totalNights = (request.CheckoutDate.ToDateTime(TimeOnly.MinValue) - request.CheckinDate.ToDateTime(TimeOnly.MinValue)).Days;

        var booking = new Booking
        {
            TenantId = tenantId,
            GuestName = request.GuestName,
            Phone = request.Phone,
            Email = request.Email,
            RoomNumber = request.RoomNumber,
            CheckinDate = request.CheckinDate,
            CheckoutDate = request.CheckoutDate,
            Status = "Confirmed",
            Source = request.Source,
            NumberOfGuests = request.NumberOfGuests,
            SpecialRequests = request.SpecialRequests,
            RoomRate = request.RoomRate,
            TotalNights = totalNights,
            TotalRevenue = request.RoomRate.HasValue ? request.RoomRate.Value * totalNights : null,
            IsRepeatGuest = request.IsRepeatGuest,
            CreatedAt = DateTime.UtcNow
        };

        _context.Bookings.Add(booking);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created booking {BookingId} for guest {GuestName} in room {RoomNumber}",
            booking.Id, booking.GuestName, booking.RoomNumber);

        // Send welcome WhatsApp message with amenities and upselling
        await SendWelcomeMessageAsync(tenantId, booking);

        return booking;
    }

    public async Task<Booking> UpdateBookingAsync(int tenantId, int bookingId, UpdateBookingRequest request)
    {
        var booking = await GetBookingByIdAsync(tenantId, bookingId);
        if (booking == null)
        {
            throw new KeyNotFoundException($"Booking {bookingId} not found");
        }

        // Update fields if provided
        if (!string.IsNullOrWhiteSpace(request.GuestName))
        {
            booking.GuestName = request.GuestName;
        }

        if (!string.IsNullOrWhiteSpace(request.Phone))
        {
            booking.Phone = request.Phone;
        }

        if (request.Email != null)
        {
            booking.Email = request.Email;
        }

        if (!string.IsNullOrWhiteSpace(request.RoomNumber) && request.RoomNumber != booking.RoomNumber)
        {
            // Check if new room is available for the booking dates
            var isAvailable = await IsRoomAvailableAsync(
                tenantId,
                request.RoomNumber,
                request.CheckinDate ?? booking.CheckinDate,
                request.CheckoutDate ?? booking.CheckoutDate,
                bookingId);

            if (!isAvailable)
            {
                throw new InvalidOperationException($"Room {request.RoomNumber} is not available for the selected dates");
            }

            booking.RoomNumber = request.RoomNumber;
        }

        if (request.CheckinDate.HasValue)
        {
            booking.CheckinDate = request.CheckinDate.Value;
        }

        if (request.CheckoutDate.HasValue)
        {
            booking.CheckoutDate = request.CheckoutDate.Value;
        }

        // Validate dates after updates
        if (booking.CheckoutDate <= booking.CheckinDate)
        {
            throw new ArgumentException("Checkout date must be after checkin date");
        }

        // Recalculate total nights and revenue
        var totalNights = (booking.CheckoutDate.ToDateTime(TimeOnly.MinValue) - booking.CheckinDate.ToDateTime(TimeOnly.MinValue)).Days;
        booking.TotalNights = totalNights;

        if (request.RoomRate.HasValue)
        {
            booking.RoomRate = request.RoomRate.Value;
        }

        if (booking.RoomRate.HasValue)
        {
            booking.TotalRevenue = booking.RoomRate.Value * totalNights;
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            booking.Status = request.Status;
        }

        if (!string.IsNullOrWhiteSpace(request.Source))
        {
            booking.Source = request.Source;
        }

        if (request.NumberOfGuests.HasValue)
        {
            booking.NumberOfGuests = request.NumberOfGuests.Value;
        }

        if (request.SpecialRequests != null)
        {
            booking.SpecialRequests = request.SpecialRequests;
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Updated booking {BookingId} for guest {GuestName}", bookingId, booking.GuestName);

        return booking;
    }

    public async Task<bool> CancelBookingAsync(int tenantId, int bookingId)
    {
        var booking = await GetBookingByIdAsync(tenantId, bookingId);
        if (booking == null)
        {
            return false;
        }

        booking.Status = "Cancelled";
        await _context.SaveChangesAsync();

        _logger.LogInformation("Cancelled booking {BookingId} for guest {GuestName}", bookingId, booking.GuestName);

        return true;
    }

    public async Task<BookingStatistics> GetBookingStatisticsAsync(int tenantId)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var tomorrow = today.AddDays(1);
        var next7Days = today.AddDays(7);

        var allBookings = await _context.Bookings
            .Where(b => b.TenantId == tenantId)
            .ToListAsync();

        var totalRevenue = allBookings
            .Where(b => b.Status != "Cancelled" && b.TotalRevenue.HasValue)
            .Sum(b => b.TotalRevenue ?? 0);

        var completedStays = allBookings
            .Where(b => b.Status == "CheckedOut" && b.TotalNights.HasValue)
            .ToList();

        var averageStayDuration = completedStays.Any()
            ? completedStays.Average(b => b.TotalNights ?? 0)
            : 0;

        // Calculate occupancy rate (current checked-in rooms / total unique rooms)
        var totalRooms = await _context.Bookings
            .Where(b => b.TenantId == tenantId && !string.IsNullOrEmpty(b.RoomNumber))
            .Select(b => b.RoomNumber)
            .Distinct()
            .CountAsync();

        var currentOccupied = allBookings
            .Count(b => b.Status == "CheckedIn" &&
                       b.CheckinDate <= today &&
                       b.CheckoutDate >= today);

        var occupancyRate = totalRooms > 0 ? (double)currentOccupied / totalRooms * 100 : 0;

        return new BookingStatistics
        {
            TotalBookings = allBookings.Count,
            ConfirmedBookings = allBookings.Count(b => b.Status == "Confirmed"),
            CheckedInBookings = allBookings.Count(b => b.Status == "CheckedIn"),
            CheckedOutBookings = allBookings.Count(b => b.Status == "CheckedOut"),
            CancelledBookings = allBookings.Count(b => b.Status == "Cancelled"),
            TodayCheckins = allBookings.Count(b => b.CheckinDate == today),
            TodayCheckouts = allBookings.Count(b => b.CheckoutDate == today),
            UpcomingCheckins = allBookings.Count(b => b.CheckinDate > today && b.CheckinDate <= next7Days),
            TotalRevenue = totalRevenue,
            AverageStayDuration = averageStayDuration,
            OccupancyRate = occupancyRate
        };
    }

    public async Task<List<string>> GetAvailableRoomsAsync(int tenantId, DateOnly checkinDate, DateOnly checkoutDate, int? excludeBookingId = null)
    {
        // Get all unique room numbers
        var allRooms = await _context.Bookings
            .Where(b => b.TenantId == tenantId && !string.IsNullOrEmpty(b.RoomNumber))
            .Select(b => b.RoomNumber!)
            .Distinct()
            .ToListAsync();

        // Get occupied rooms for the date range
        var occupiedRooms = await _context.Bookings
            .Where(b => b.TenantId == tenantId &&
                       b.Status != "Cancelled" &&
                       b.Status != "CheckedOut" &&
                       (!excludeBookingId.HasValue || b.Id != excludeBookingId.Value) &&
                       !string.IsNullOrEmpty(b.RoomNumber) &&
                       ((b.CheckinDate <= checkinDate && b.CheckoutDate > checkinDate) ||
                        (b.CheckinDate < checkoutDate && b.CheckoutDate >= checkoutDate) ||
                        (b.CheckinDate >= checkinDate && b.CheckoutDate <= checkoutDate)))
            .Select(b => b.RoomNumber!)
            .Distinct()
            .ToListAsync();

        // Return rooms that are not occupied
        return allRooms.Except(occupiedRooms).ToList();
    }

    public async Task<bool> IsRoomAvailableAsync(int tenantId, string roomNumber, DateOnly checkinDate, DateOnly checkoutDate, int? excludeBookingId = null)
    {
        var conflictingBooking = await _context.Bookings
            .AnyAsync(b => b.TenantId == tenantId &&
                          b.RoomNumber == roomNumber &&
                          b.Status != "Cancelled" &&
                          b.Status != "CheckedOut" &&
                          (!excludeBookingId.HasValue || b.Id != excludeBookingId.Value) &&
                          ((b.CheckinDate <= checkinDate && b.CheckoutDate > checkinDate) ||
                           (b.CheckinDate < checkoutDate && b.CheckoutDate >= checkoutDate) ||
                           (b.CheckinDate >= checkinDate && b.CheckoutDate <= checkoutDate)));

        return !conflictingBooking;
    }

    /// <summary>
    /// Sends a warm welcome WhatsApp message to the guest when a booking is created
    /// Includes amenities, WiFi, and upselling opportunities for a premium experience
    /// </summary>
    private async Task SendWelcomeMessageAsync(int tenantId, Booking booking)
    {
        try
        {
            // Get tenant information
            var tenant = await _context.Tenants.FindAsync(tenantId);
            if (tenant == null)
            {
                _logger.LogWarning("Cannot send welcome message - tenant {TenantId} not found", tenantId);
                return;
            }

            // Get all relevant business info in parallel
            var businessInfoTask = _context.BusinessInfo
                .Where(b => b.TenantId == tenantId)
                .ToListAsync();

            // Get featured services for upselling (high priority services)
            var featuredServicesTask = _context.Services
                .Where(s => s.TenantId == tenantId && s.IsAvailable && s.Priority > 0)
                .OrderByDescending(s => s.Priority)
                .Take(3)
                .ToListAsync();

            await Task.WhenAll(businessInfoTask, featuredServicesTask);

            var businessInfo = await businessInfoTask;
            var featuredServices = await featuredServicesTask;

            // Build the welcome message
            var message = BuildWelcomeMessage(tenant.Name, booking, businessInfo, featuredServices);

            // Send via WhatsApp
            var sent = await _whatsAppService.SendTextMessageAsync(tenantId, booking.Phone, message);

            if (sent)
            {
                _logger.LogInformation("Sent welcome message to {Phone} for booking {BookingId}",
                    booking.Phone, booking.Id);
            }
            else
            {
                _logger.LogWarning("Failed to send welcome message to {Phone} for booking {BookingId}",
                    booking.Phone, booking.Id);
            }
        }
        catch (Exception ex)
        {
            // Don't fail the booking creation if welcome message fails
            _logger.LogError(ex, "Error sending welcome message for booking {BookingId}", booking.Id);
        }
    }

    /// <summary>
    /// Builds a warm, personalized welcome message with premium touches:
    /// - Booking confirmation
    /// - WiFi credentials
    /// - Amenities highlights
    /// - Featured services (upselling)
    /// - Restaurant/dining info
    /// - Concierge invitation
    /// </summary>
    private string BuildWelcomeMessage(
        string hotelName,
        Booking booking,
        List<BusinessInfo> businessInfo,
        List<Service> featuredServices)
    {
        var firstName = booking.GuestName.Split(' ')[0];
        var checkInDate = booking.CheckinDate.ToString("dddd, MMMM d, yyyy");
        var checkOutDate = booking.CheckoutDate.ToString("dddd, MMMM d, yyyy");
        var nights = booking.TotalNights ?? 1;

        // Extract relevant info from BusinessInfo
        string checkInTime = "3:00 PM";
        string? wifiNetwork = null;
        string? wifiPassword = null;
        string? amenitiesInfo = null;
        string? restaurantHours = null;

        foreach (var info in businessInfo)
        {
            try
            {
                if (info.Category == "hotel_info" || info.Category == "hours")
                {
                    var data = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(info.Content);
                    if (data?.TryGetValue("checkInTime", out var time) == true)
                        checkInTime = time;
                }
                else if (info.Category == "wifi_credentials")
                {
                    var data = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(info.Content);
                    if (data != null)
                    {
                        data.TryGetValue("network", out wifiNetwork);
                        data.TryGetValue("password", out wifiPassword);
                    }
                }
                else if (info.Category == "amenities" && string.IsNullOrEmpty(amenitiesInfo))
                {
                    amenitiesInfo = info.Content;
                }
                else if (info.Category == "facility_hours" && info.Title?.Contains("Restaurant", StringComparison.OrdinalIgnoreCase) == true)
                {
                    restaurantHours = info.Content;
                }
            }
            catch { /* Skip malformed entries */ }
        }

        var sb = new System.Text.StringBuilder();

        // Personal warm greeting
        sb.AppendLine($"Hello {firstName}!");
        sb.AppendLine();
        sb.AppendLine($"Welcome to {hotelName}! We're delighted to confirm your reservation.");
        sb.AppendLine();

        // Booking details
        sb.AppendLine("*Booking Details*");
        sb.AppendLine($"Check-in: {checkInDate} from {checkInTime}");
        sb.AppendLine($"Check-out: {checkOutDate}");
        if (!string.IsNullOrEmpty(booking.RoomNumber))
        {
            sb.AppendLine($"Room: {booking.RoomNumber}");
        }
        sb.AppendLine($"Guests: {booking.NumberOfGuests}");

        // WiFi - guests always want this!
        if (!string.IsNullOrEmpty(wifiNetwork) && !string.IsNullOrEmpty(wifiPassword))
        {
            sb.AppendLine();
            sb.AppendLine("*WiFi Access*");
            sb.AppendLine($"Network: {wifiNetwork}");
            sb.AppendLine($"Password: {wifiPassword}");
        }

        // Amenities highlights
        if (!string.IsNullOrEmpty(amenitiesInfo))
        {
            sb.AppendLine();
            sb.AppendLine("*Enjoy Our Amenities*");
            // Parse amenities if JSON, otherwise use as-is
            try
            {
                var amenitiesList = System.Text.Json.JsonSerializer.Deserialize<List<string>>(amenitiesInfo);
                if (amenitiesList != null)
                {
                    foreach (var amenity in amenitiesList.Take(4))
                    {
                        sb.AppendLine($"• {amenity}");
                    }
                }
            }
            catch
            {
                // If not JSON, show first 150 chars
                sb.AppendLine(amenitiesInfo.Length > 150 ? amenitiesInfo.Substring(0, 150) + "..." : amenitiesInfo);
            }
        }

        // Featured services (upselling)
        if (featuredServices.Any())
        {
            sb.AppendLine();
            sb.AppendLine("*Enhance Your Stay*");
            foreach (var service in featuredServices)
            {
                var price = service.Price.HasValue ? $" - R{service.Price:N0}" : "";
                sb.AppendLine($"• {service.Name}{price}");
            }
        }

        // Early check-in offer
        sb.AppendLine();
        sb.AppendLine("Need early check-in or have special requests? Just reply to this message!");

        // Special occasion recognition
        if (!string.IsNullOrEmpty(booking.SpecialRequests) &&
            (booking.SpecialRequests.Contains("birthday", StringComparison.OrdinalIgnoreCase) ||
             booking.SpecialRequests.Contains("anniversary", StringComparison.OrdinalIgnoreCase) ||
             booking.SpecialRequests.Contains("honeymoon", StringComparison.OrdinalIgnoreCase)))
        {
            sb.AppendLine();
            sb.AppendLine("We noticed you're celebrating something special - our team will make it memorable!");
        }

        // Closing with concierge availability
        sb.AppendLine();
        sb.AppendLine($"We look forward to hosting you at {hotelName}!");
        sb.AppendLine();
        sb.AppendLine("Your personal concierge is just a message away. Safe travels!");

        return sb.ToString();
    }
}
