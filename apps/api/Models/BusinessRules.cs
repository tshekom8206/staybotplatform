using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Hostr.Api.Models;

[Index(nameof(ServiceId), nameof(IsActive), Name = "IX_ServiceBusinessRules_Service_Active")]
[Index(nameof(TenantId), Name = "IX_ServiceBusinessRules_Tenant")]
[Index(nameof(RuleType), Name = "IX_ServiceBusinessRules_RuleType")]
public class ServiceBusinessRule
{
    [Key]
    public int Id { get; set; }

    public int TenantId { get; set; }

    public int ServiceId { get; set; }

    [Required, MaxLength(50)]
    public string RuleType { get; set; } = string.Empty; // max_group_size, min_advance_booking_hours, max_bookings_per_day, etc.

    [Required, MaxLength(100)]
    public string RuleKey { get; set; } = string.Empty; // Human-readable key like "Maximum Group Size"

    [Required, MaxLength(500)]
    public string RuleValue { get; set; } = string.Empty; // The actual value or constraint

    [MaxLength(1000)]
    public string? ValidationMessage { get; set; } // Custom message to show when rule is violated

    public int Priority { get; set; } = 0; // For ordering rule evaluation

    public bool IsActive { get; set; } = true;

    [MaxLength(2000)]
    public string? UpsellSuggestions { get; set; } // JSON array of related upsell item IDs and contexts

    [MaxLength(1000)]
    public string? RelevanceContext { get; set; } // When to trigger upsells (e.g., "group_size > 4")

    public decimal? MinConfidenceScore { get; set; } = 0.8m; // Minimum confidence for upsell relevance

    [MaxLength(500)]
    public string? Notes { get; set; } // Internal notes for staff

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual Tenant Tenant { get; set; } = null!;
    public virtual Service Service { get; set; } = null!;
}

[Index(nameof(RequestItemId), nameof(IsActive), Name = "IX_RequestItemRules_Item_Active")]
[Index(nameof(TenantId), Name = "IX_RequestItemRules_Tenant")]
[Index(nameof(RuleType), Name = "IX_RequestItemRules_RuleType")]
public class RequestItemRule
{
    [Key]
    public int Id { get; set; }

    public int TenantId { get; set; }

    public int RequestItemId { get; set; }

    [Required, MaxLength(50)]
    public string RuleType { get; set; } = string.Empty; // max_per_room, requires_booking, restricted_hours, max_per_guest, etc.

    [Required, MaxLength(100)]
    public string RuleKey { get; set; } = string.Empty; // Human-readable key like "Max Per Room"

    [Required, MaxLength(500)]
    public string RuleValue { get; set; } = string.Empty; // The actual value or constraint

    [MaxLength(1000)]
    public string? ValidationMessage { get; set; } // Custom message to show when rule is violated

    public int? MaxPerRoom { get; set; } // Convenience field for common rule

    public int? MaxPerGuest { get; set; } // Convenience field for common rule

    public bool RequiresActiveBooking { get; set; } = false; // Must have active booking to request

    [MaxLength(100)]
    public string? RestrictedHours { get; set; } // Time restrictions (e.g., "22:00-06:00")

    public int Priority { get; set; } = 0; // For ordering rule evaluation

    public bool IsActive { get; set; } = true;

    [MaxLength(2000)]
    public string? UpsellSuggestions { get; set; } // JSON array of related upsell item IDs and contexts

    [MaxLength(1000)]
    public string? RelevanceContext { get; set; } // When to trigger upsells (e.g., "item = 'towels' AND quantity > 2")

    public decimal? MinConfidenceScore { get; set; } = 0.8m; // Minimum confidence for upsell relevance

    [MaxLength(500)]
    public string? Notes { get; set; } // Internal notes for staff

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual Tenant Tenant { get; set; } = null!;
    public virtual RequestItem RequestItem { get; set; } = null!;
}
