using System.ComponentModel.DataAnnotations;

namespace Hostr.Api.Models;

public enum GuestType
{
    // Active guests with full access
    Active,
    
    // Guests with bookings but limited access
    PreArrival,        // Confirmed but before check-in date
    PostCheckout,      // After checkout date or status = CheckedOut
    Cancelled,         // Booking cancelled
    
    // Users without bookings
    Unregistered,      // Phone not in bookings table
    
    // Special cases
    DayGuest,          // Same-day check-in/out
    VipMember,         // Loyalty program member with enhanced access
    Staff              // Staff override for testing
}

public class GuestStatus
{
    public GuestType Type { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public string DisplayName { get; set; } = "Guest";
    public bool IsActive => Type == GuestType.Active;
    
    // Permissions - More flexible and guest-friendly
    public bool CanRequestItems => Type == GuestType.Active || Type == GuestType.Staff;
    public bool CanOrderFood => Type == GuestType.Active || Type == GuestType.PreArrival || Type == GuestType.Staff;
    public bool CanViewMenu => true; // Everyone can view menu
    public bool CanMakeInquiries => true; // Everyone can ask questions
    public bool CanReportIssues => Type != GuestType.Unregistered || IsWithinGracePeriod;
    public bool CanAccessConcierge => Type != GuestType.Unregistered;
    public bool CanProvideFeedback => Type == GuestType.PostCheckout || Type == GuestType.Active || IsWithinGracePeriod;
    public bool CanFileComplaints => Type != GuestType.Unregistered; // Even cancelled guests can complain
    
    // Booking details (if applicable)
    public int? BookingId { get; set; }
    public DateOnly? CheckinDate { get; set; }
    public DateOnly? CheckoutDate { get; set; }
    public string? BookingStatus { get; set; }
    public string? RoomNumber { get; set; }
    
    // Context for responses
    public string StatusMessage { get; set; } = string.Empty;
    public List<string> AllowedActions { get; set; } = new();
    public List<string> RestrictedActions { get; set; } = new();
    
    // Additional scenarios covered
    public bool IsMultipleBookings { get; set; }
    public bool IsGroupBooking { get; set; }
    public bool IsWithinGracePeriod { get; set; } // 24 hours after checkout
    public DateTime StatusDeterminedAt { get; set; } = DateTime.UtcNow;
    
    public static GuestStatus CreateUnregistered(string phoneNumber)
    {
        return new GuestStatus
        {
            Type = GuestType.Unregistered,
            PhoneNumber = phoneNumber,
            StatusMessage = "Welcome! You can view our menu, check rates, and ask general questions",
            AllowedActions = new() { "view_menu", "check_rates", "general_inquiry", "ask_questions", "get_hotel_info" },
            RestrictedActions = new() { "request_items", "order_food", "create_room_tasks" }
        };
    }
    
    public static GuestStatus CreateActive(string phoneNumber, Booking booking)
    {
        return new GuestStatus
        {
            Type = GuestType.Active,
            PhoneNumber = phoneNumber,
            DisplayName = booking.GuestName,
            BookingId = booking.Id,
            CheckinDate = booking.CheckinDate,
            CheckoutDate = booking.CheckoutDate,
            BookingStatus = booking.Status,
            RoomNumber = booking.RoomNumber,
            StatusMessage = $"Active guest - full access (checked in from {booking.CheckinDate} to {booking.CheckoutDate})",
            AllowedActions = new() { "request_items", "order_food", "create_tasks", "view_menu", "concierge_services" },
            RestrictedActions = new()
        };
    }
    
    public static GuestStatus CreatePreArrival(string phoneNumber, Booking booking)
    {
        return new GuestStatus
        {
            Type = GuestType.PreArrival,
            PhoneNumber = phoneNumber,
            DisplayName = booking.GuestName,
            BookingId = booking.Id,
            CheckinDate = booking.CheckinDate,
            CheckoutDate = booking.CheckoutDate,
            BookingStatus = booking.Status,
            RoomNumber = booking.RoomNumber,
            StatusMessage = $"Pre-arrival guest - limited access (checking in on {booking.CheckinDate})",
            AllowedActions = new() { "view_menu", "pre_order_services", "check_in_info", "general_inquiry" },
            RestrictedActions = new() { "request_items", "create_maintenance_tasks" }
        };
    }
    
    public static GuestStatus CreatePostCheckout(string phoneNumber, Booking booking, bool withinGracePeriod)
    {
        return new GuestStatus
        {
            Type = GuestType.PostCheckout,
            PhoneNumber = phoneNumber,
            DisplayName = booking.GuestName,
            BookingId = booking.Id,
            CheckinDate = booking.CheckinDate,
            CheckoutDate = booking.CheckoutDate,
            BookingStatus = booking.Status,
            RoomNumber = booking.RoomNumber,
            IsWithinGracePeriod = withinGracePeriod,
            StatusMessage = withinGracePeriod
                ? $"Post-checkout guest within 48hr grace period (checked out {booking.CheckoutDate})"
                : $"Former guest - limited access (checked out {booking.CheckoutDate})",
            AllowedActions = withinGracePeriod
                ? new() { "view_menu", "report_lost_items", "request_receipts", "leave_feedback", "file_complaints", "general_inquiry" }
                : new() { "view_menu", "general_inquiry", "make_new_booking" },
            RestrictedActions = new() { "request_items", "order_food" }
        };
    }
    
    public static GuestStatus CreateCancelled(string phoneNumber, Booking booking)
    {
        return new GuestStatus
        {
            Type = GuestType.Cancelled,
            PhoneNumber = phoneNumber,
            DisplayName = booking.GuestName,
            BookingId = booking.Id,
            CheckinDate = booking.CheckinDate,
            CheckoutDate = booking.CheckoutDate,
            BookingStatus = booking.Status,
            StatusMessage = "Cancelled booking - inquiries and complaints allowed",
            AllowedActions = new() { "view_menu", "make_new_booking", "general_inquiry", "file_complaints", "ask_questions" },
            RestrictedActions = new() { "request_items", "order_food", "create_room_tasks" }
        };
    }
}