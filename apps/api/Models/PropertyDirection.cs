using System.ComponentModel.DataAnnotations;

namespace Hostr.Api.Models;

/// <summary>
/// Stores detailed directions for property facilities
/// Used by the DIRECTIONS intent handler to provide accurate location information
/// </summary>
public class PropertyDirection
{
    [Key]
    public int Id { get; set; }

    public int TenantId { get; set; }

    [Required, MaxLength(100)]
    public string FacilityName { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string Category { get; set; } = string.Empty; // Pool, Dining, Spa, Fitness, etc.

    [Required, MaxLength(1000)]
    public string Directions { get; set; } = string.Empty; // Detailed walking directions

    [MaxLength(200)]
    public string? LocationDescription { get; set; } // e.g., "Ground floor, near lobby"

    [MaxLength(100)]
    public string? Floor { get; set; } // "Ground Floor", "2nd Floor", "Lower Level"

    [MaxLength(100)]
    public string? Wing { get; set; } // "East Wing", "Main Building", etc.

    [MaxLength(500)]
    public string? Landmarks { get; set; } // "Next to the main elevator", "Across from the spa"

    [MaxLength(100)]
    public string? Hours { get; set; } // Operating hours

    public bool IsActive { get; set; } = true;

    public int Priority { get; set; } = 0; // For sorting if multiple facilities of same type

    [MaxLength(200)]
    public string? ImageUrl { get; set; }

    [MaxLength(500)]
    public string? AdditionalInfo { get; set; } // Parking info, accessibility notes, etc.

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual Tenant Tenant { get; set; } = null!;
}
