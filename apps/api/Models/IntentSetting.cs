using System.ComponentModel.DataAnnotations;

namespace Hostr.Api.Models;

/// <summary>
/// Stores configuration settings for intent handlers
/// Allows admins to customize intent behavior, enable/disable intents, and configure upsells
/// </summary>
public class IntentSetting
{
    [Key]
    public int Id { get; set; }

    public int TenantId { get; set; }

    [Required, MaxLength(50)]
    public string IntentName { get; set; } = string.Empty; // CANCELLATION, CHECK_IN_OUT, DIRECTIONS, FEEDBACK, RECOMMENDATION

    public bool IsEnabled { get; set; } = true;

    public bool EnableUpselling { get; set; } = true;

    [MaxLength(1000)]
    public string? CustomResponse { get; set; } // Optional custom response template

    [MaxLength(500)]
    public string? UpsellStrategy { get; set; } // JSON or description of upsell strategy

    public int Priority { get; set; } = 0; // For intent matching priority

    public bool RequiresStaffApproval { get; set; } = false; // Create staff task for review

    public bool NotifyStaff { get; set; } = true; // Send notifications

    [MaxLength(100)]
    public string? AssignedDepartment { get; set; } // Which department handles this intent

    [MaxLength(50)]
    public string? TaskPriority { get; set; } = "Medium"; // High, Medium, Low

    public bool AutoResolve { get; set; } = false; // Automatically resolve without staff intervention

    public int? AutoResolveDelayMinutes { get; set; } // Delay before auto-resolving

    [MaxLength(1000)]
    public string? AdditionalConfig { get; set; } // JSON for extra configuration

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual Tenant Tenant { get; set; } = null!;
}
