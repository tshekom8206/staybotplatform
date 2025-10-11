using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Hostr.Api.Models;

public class Service
{
    [Key]
    public int Id { get; set; }

    public int TenantId { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required, MaxLength(50)]
    public string Category { get; set; } = string.Empty; // Room Service, Concierge, Transportation, etc.

    [MaxLength(50)]
    public string? Icon { get; set; }

    public bool IsAvailable { get; set; } = true;

    public bool IsChargeable { get; set; } = false;

    [Column(TypeName = "decimal(10,2)")]
    public decimal? Price { get; set; }

    [MaxLength(3)]
    public string? Currency { get; set; } = "ZAR";

    [MaxLength(50)]
    public string? PricingUnit { get; set; } // per hour, per service, per person, etc.

    [MaxLength(100)]
    public string? AvailableHours { get; set; } // "24/7", "8:00-20:00", etc.

    [MaxLength(20)]
    public string? ContactMethod { get; set; } // phone, app, front desk, etc.

    [MaxLength(100)]
    public string? ContactInfo { get; set; } // phone number or extension

    public int Priority { get; set; } = 0; // for sorting/display order

    [MaxLength(1000)]
    public string? SpecialInstructions { get; set; }

    [MaxLength(200)]
    public string? ImageUrl { get; set; }

    public bool RequiresAdvanceBooking { get; set; } = false;

    public int? AdvanceBookingHours { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual Tenant Tenant { get; set; } = null!;
    public virtual ICollection<ServiceBusinessRule> BusinessRules { get; set; } = new List<ServiceBusinessRule>();
}

public class ServiceIcon
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(50)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string Icon { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Label { get; set; }

    public bool IsActive { get; set; } = true;

    public int SortOrder { get; set; }
}