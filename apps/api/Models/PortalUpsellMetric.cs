using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Hostr.Api.Models;

/// <summary>
/// Tracks upselling performance from the Guest Portal (not WhatsApp conversations)
/// </summary>
public class PortalUpsellMetric
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int TenantId { get; set; }

    [Required]
    public int ServiceId { get; set; }

    [Required, MaxLength(200)]
    public string ServiceName { get; set; } = string.Empty;

    [Column(TypeName = "decimal(10,2)")]
    public decimal ServicePrice { get; set; }

    [MaxLength(50)]
    public string? ServiceCategory { get; set; }

    /// <summary>
    /// Source of the upsell: weather_hot, weather_warm, weather_cold, weather_rainy, featured_carousel, service_menu
    /// </summary>
    [Required, MaxLength(100)]
    public string Source { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? RoomNumber { get; set; }

    /// <summary>
    /// Event type: impression (shown), click (opened modal), conversion (request submitted)
    /// </summary>
    [Required, MaxLength(20)]
    public string EventType { get; set; } = "impression";

    /// <summary>
    /// Revenue generated (for conversions only)
    /// </summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal Revenue { get; set; } = 0;

    /// <summary>
    /// Associated staff task ID (for conversions)
    /// </summary>
    public int? StaffTaskId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual Tenant Tenant { get; set; } = null!;
    public virtual Service Service { get; set; } = null!;
    public virtual StaffTask? StaffTask { get; set; }
}
