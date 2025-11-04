using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using System.Text.Json;

namespace Hostr.Api.Models;

public class UpsellItem
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    
    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;
    
    [Required]
    public string Description { get; set; } = string.Empty;
    
    public int PriceCents { get; set; }
    
    [MaxLength(20)]
    public string Unit { get; set; } = "item";
    
    public bool IsActive { get; set; } = true;
    
    public string[] Categories { get; set; } = Array.Empty<string>();
    
    public int LeadTimeMinutes { get; set; } = 60;
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual Tenant Tenant { get; set; } = null!;
}

public class GuideItem
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    
    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;
    
    [Required]
    public string Description { get; set; } = string.Empty;
    
    [MaxLength(50)]
    public string Category { get; set; } = string.Empty;
    
    public JsonDocument? LocationJson { get; set; }
    public JsonDocument? OpenHoursJson { get; set; }
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual Tenant Tenant { get; set; } = null!;
}

public class Booking
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    
    [Required, MaxLength(100)]
    public string GuestName { get; set; } = string.Empty;
    
    [Required, MaxLength(20)]
    public string Phone { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Email { get; set; }

    [JsonPropertyName("checkinDate")]
    public DateOnly CheckinDate { get; set; }
    [JsonPropertyName("checkoutDate")]
    public DateOnly CheckoutDate { get; set; }
    
    [Required, MaxLength(20)]
    public string Status { get; set; } = "Confirmed"; // Confirmed|CheckedIn|CheckedOut|Cancelled
    
    [MaxLength(50)]
    public string Source { get; set; } = "Direct";
    
    [MaxLength(10)]
    public string? RoomNumber { get; set; }

    public int NumberOfGuests { get; set; } = 1;

    [MaxLength(500)]
    public string? SpecialRequests { get; set; }

    // Business tracking fields
    public bool IsRepeatGuest { get; set; } = false;
    public int? PreviousBookingId { get; set; }
    public decimal? TotalRevenue { get; set; }
    public bool SurveyOptOut { get; set; } = false;
    public bool IsStaff { get; set; } = false;
    public int? ExtendedFromBookingId { get; set; }
    [JsonPropertyName("actualCheckInDate")]
    public DateTime? CheckInDate { get; set; }
    [JsonPropertyName("actualCheckOutDate")]
    public DateTime? CheckOutDate { get; set; }
    public int? TotalNights { get; set; }
    public decimal? RoomRate { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual Tenant Tenant { get; set; } = null!;
    public virtual ICollection<Rating> Ratings { get; set; } = new List<Rating>();
    public virtual Booking? PreviousBooking { get; set; }
    public virtual Booking? ExtendedFromBooking { get; set; }
}

public class Rating
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int? BookingId { get; set; }
    public int? ConversationId { get; set; }
    
    [Required, MaxLength(20)]
    public string GuestPhone { get; set; } = string.Empty;
    
    [Required, MaxLength(20)]
    public string Source { get; set; } = "checkout"; // checkout|manual|whatsapp
    
    public int? Score { get; set; } // 1-5
    public string? Comment { get; set; }
    
    public DateTime? AskedAt { get; set; }
    public DateTime? ReceivedAt { get; set; }
    
    [Required, MaxLength(20)]
    public string Status { get; set; } = "pending"; // pending|asked|received|expired
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual Tenant Tenant { get; set; } = null!;
    public virtual Booking? Booking { get; set; }
    public virtual Conversation? Conversation { get; set; }
}

public class BookingModification
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int BookingId { get; set; }
    public int? ConversationId { get; set; } // if requested via chat
    
    [Required, MaxLength(50)]
    public string ModificationType { get; set; } = string.Empty; // date_change|guest_change|cancellation|extension
    
    [Required, MaxLength(20)]
    public string Status { get; set; } = "Pending"; // Pending|Approved|Rejected|Cancelled
    
    public string RequestDetails { get; set; } = string.Empty; // JSON with modification details
    
    // Original booking data
    public DateOnly? OriginalCheckinDate { get; set; }
    public DateOnly? OriginalCheckoutDate { get; set; }
    public string? OriginalGuestName { get; set; }
    public string? OriginalPhone { get; set; }
    
    // Requested changes
    public DateOnly? NewCheckinDate { get; set; }
    public DateOnly? NewCheckoutDate { get; set; }
    public string? NewGuestName { get; set; }
    public string? NewPhone { get; set; }
    
    [MaxLength(20)]
    public string? RequestedBy { get; set; } // Guest phone or staff name
    
    public decimal? FeeDifference { get; set; } // Additional cost or refund
    public bool RequiresApproval { get; set; } = true;
    
    public string? Reason { get; set; }
    public string? RejectionReason { get; set; }
    public string? StaffNotes { get; set; }
    
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }
    public int? ProcessedBy { get; set; } // Staff user ID

    // NEW: Late checkout specific fields
    public TimeSpan? RequestedCheckOutTime { get; set; } // Requested late checkout time

    [MaxLength(20)]
    public string? ApprovalStatus { get; set; } // Pending|Auto-Approved|Approved|Rejected

    [MaxLength(500)]
    public string? ApprovalReason { get; set; } // Reason for approval/rejection

    public decimal? PricingImpact { get; set; } // Additional charge for late checkout

    public int? ApprovedBy { get; set; } // Staff user ID who approved

    public DateTime? ApprovedAt { get; set; }

    // Navigation properties
    public virtual Tenant Tenant { get; set; } = null!;
    public virtual Booking Booking { get; set; } = null!;
    public virtual Conversation? Conversation { get; set; }
    public virtual User? ProcessedByUser { get; set; }
    public virtual User? ApprovedByUser { get; set; }
}

public class BookingChangeHistory
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int BookingId { get; set; }
    public int? BookingModificationId { get; set; } // Link to the modification request if applicable
    
    [Required, MaxLength(50)]
    public string ChangeType { get; set; } = string.Empty; // checkin_date|checkout_date|guest_name|phone|status|cancellation
    
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    
    [MaxLength(20)]
    public string? ChangedBy { get; set; } // Staff name or "Guest" or "System"
    
    public string? ChangeReason { get; set; }
    
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual Tenant Tenant { get; set; } = null!;
    public virtual Booking Booking { get; set; } = null!;
    public virtual BookingModification? BookingModification { get; set; }
}