using System.ComponentModel.DataAnnotations;

namespace Hostr.Api.Models;

/// <summary>
/// Defines keyword-based urgency classification rules for maintenance requests
/// Allows tenant-specific configuration of maintenance priorities and SLAs
/// </summary>
public class MaintenanceUrgencyRule
{
    [Key]
    public int Id { get; set; }

    public int TenantId { get; set; }

    // Keyword/phrase to detect in guest messages
    [Required, MaxLength(100)]
    public string Keyword { get; set; } = string.Empty; // e.g., "gas", "fire", "leak", "remote"

    [Required, MaxLength(20)]
    public string KeywordType { get; set; } = "contains"; // exact|contains|starts_with

    // Classification
    [Required, MaxLength(20)]
    public string UrgencyLevel { get; set; } = string.Empty; // EMERGENCY, URGENT, HIGH, NORMAL, LOW

    [MaxLength(50)]
    public string? Category { get; set; } // Gas Leak, Plumbing, Electrical, HVAC, Amenity

    // Priority & SLA
    [Required, MaxLength(20)]
    public string TaskPriority { get; set; } = string.Empty; // Urgent, High, Normal, Low

    public int TargetMinutesToResolve { get; set; } // 5, 15, 60, 240, 1440

    // Flags
    public bool IsSafetyRisk { get; set; } = false;
    public bool IsHabitabilityImpact { get; set; } = false;
    public bool RequiresManagerEscalation { get; set; } = false;

    // Response
    public int? ResponseTemplateId { get; set; } // FK to ResponseTemplates

    [MaxLength(1000)]
    public string? SafetyInstructions { get; set; } // e.g., "Evacuate room immediately"

    // Metadata
    public int Priority { get; set; } = 0; // For matching priority (higher number = check first)

    public bool IsActive { get; set; } = true;

    [MaxLength(500)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual Tenant Tenant { get; set; } = null!;
    public virtual ResponseTemplate? ResponseTemplate { get; set; }
}
