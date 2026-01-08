using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Hostr.Api.Models;

public enum ScheduledMessageType
{
    CheckinDay,      // Morning of check-in day (9 AM default)
    MidStay,         // Day 2 satisfaction check (skip for 1-night stays)
    PreCheckout,     // Day before checkout or same day for 1-night stays
    PostStay         // Post-checkout feedback request
}

public enum ScheduledMessageStatus
{
    Pending,         // Waiting to be sent
    Sent,            // Successfully sent
    Failed,          // Failed to send (will retry)
    Cancelled        // Booking cancelled or dates changed
}

public class ScheduledMessage
{
    [Key]
    public int Id { get; set; }

    public int TenantId { get; set; }

    public int BookingId { get; set; }

    [Required, MaxLength(20)]
    public string Phone { get; set; } = string.Empty;

    [Required]
    public ScheduledMessageType MessageType { get; set; }

    public DateTime ScheduledFor { get; set; }

    [Required]
    public string Content { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? MediaUrl { get; set; }

    [Required]
    public ScheduledMessageStatus Status { get; set; } = ScheduledMessageStatus.Pending;

    public DateTime? SentAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(500)]
    public string? ErrorMessage { get; set; }

    public int RetryCount { get; set; } = 0;

    // Navigation properties
    [ForeignKey("TenantId")]
    public virtual Tenant Tenant { get; set; } = null!;

    [ForeignKey("BookingId")]
    public virtual Booking Booking { get; set; } = null!;
}

/// <summary>
/// Tenant-specific settings for proactive messaging
/// </summary>
public class ProactiveMessageSettings
{
    [Key]
    public int Id { get; set; }

    public int TenantId { get; set; }

    // Enable/disable each message type
    public bool CheckinDayEnabled { get; set; } = true;
    public bool MidStayEnabled { get; set; } = true;
    public bool PreCheckoutEnabled { get; set; } = true;
    public bool PostStayEnabled { get; set; } = true;

    // Timing configuration (stored as TimeSpan ticks for EF compatibility)
    public TimeSpan CheckinDayTime { get; set; } = new TimeSpan(9, 0, 0);   // 9:00 AM
    public TimeSpan MidStayTime { get; set; } = new TimeSpan(10, 0, 0);     // 10:00 AM
    public TimeSpan PreCheckoutTime { get; set; } = new TimeSpan(18, 0, 0); // 6:00 PM
    public TimeSpan PostStayTime { get; set; } = new TimeSpan(10, 0, 0);    // 10:00 AM

    // Media settings
    [MaxLength(500)]
    public string? WelcomeImageUrl { get; set; }

    public bool IncludePhotoInWelcome { get; set; } = true;

    // Timezone (IANA timezone name, e.g., "Africa/Johannesburg")
    [MaxLength(50)]
    public string Timezone { get; set; } = "Africa/Johannesburg";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    [ForeignKey("TenantId")]
    public virtual Tenant Tenant { get; set; } = null!;
}
