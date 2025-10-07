using Hostr.Api.Models;

namespace Hostr.Api.Services;

public interface IBookingManagementService
{
    Task<(List<Booking> bookings, int totalCount)> GetBookingsAsync(
        int tenantId,
        int page = 1,
        int pageSize = 20,
        string? status = null,
        string? search = null,
        DateOnly? checkinFrom = null,
        DateOnly? checkinTo = null,
        DateOnly? checkoutFrom = null,
        DateOnly? checkoutTo = null,
        string? source = null);

    Task<Booking?> GetBookingByIdAsync(int tenantId, int bookingId);

    Task<Booking> CreateBookingAsync(int tenantId, CreateBookingRequest request);

    Task<Booking> UpdateBookingAsync(int tenantId, int bookingId, UpdateBookingRequest request);

    Task<bool> CancelBookingAsync(int tenantId, int bookingId);

    Task<BookingStatistics> GetBookingStatisticsAsync(int tenantId);

    Task<List<string>> GetAvailableRoomsAsync(int tenantId, DateOnly checkinDate, DateOnly checkoutDate, int? excludeBookingId = null);

    Task<bool> IsRoomAvailableAsync(int tenantId, string roomNumber, DateOnly checkinDate, DateOnly checkoutDate, int? excludeBookingId = null);
}

public class CreateBookingRequest
{
    public string GuestName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string RoomNumber { get; set; } = string.Empty;
    public DateOnly CheckinDate { get; set; }
    public DateOnly CheckoutDate { get; set; }
    public string Source { get; set; } = "Direct";
    public int NumberOfGuests { get; set; } = 1;
    public string? SpecialRequests { get; set; }
    public decimal? RoomRate { get; set; }
    public bool IsRepeatGuest { get; set; } = false;
}

public class UpdateBookingRequest
{
    public string? GuestName { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? RoomNumber { get; set; }
    public DateOnly? CheckinDate { get; set; }
    public DateOnly? CheckoutDate { get; set; }
    public string? Status { get; set; }
    public string? Source { get; set; }
    public int? NumberOfGuests { get; set; }
    public string? SpecialRequests { get; set; }
    public decimal? RoomRate { get; set; }
}

public class BookingStatistics
{
    public int TotalBookings { get; set; }
    public int ConfirmedBookings { get; set; }
    public int CheckedInBookings { get; set; }
    public int CheckedOutBookings { get; set; }
    public int CancelledBookings { get; set; }
    public int TodayCheckins { get; set; }
    public int TodayCheckouts { get; set; }
    public int UpcomingCheckins { get; set; }
    public decimal TotalRevenue { get; set; }
    public double AverageStayDuration { get; set; }
    public double OccupancyRate { get; set; }
}
