using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Hostr.Api.Models;

public class TenantDepartment
{
    [Key]
    public int Id { get; set; }

    public int TenantId { get; set; }

    [Required, MaxLength(50)]
    public string DepartmentName { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public int Priority { get; set; } = 0; // For display order

    [MaxLength(100)]
    public string? ContactInfo { get; set; } // Phone extension, etc.

    [MaxLength(100)]
    public string? WorkingHours { get; set; } // "24/7", "8:00-17:00", etc.

    public int? MaxConcurrentTasks { get; set; } // Optional capacity limit

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual Tenant Tenant { get; set; } = null!;
    public virtual ICollection<ServiceDepartmentMapping> ServiceMappings { get; set; } = new List<ServiceDepartmentMapping>();
}

public class ServiceDepartmentMapping
{
    [Key]
    public int Id { get; set; }

    public int TenantId { get; set; }

    [Required, MaxLength(100)]
    public string ServiceCategory { get; set; } = string.Empty; // "Local Tours", "Dining", "Electronics", etc.

    [Required, MaxLength(50)]
    public string TargetDepartment { get; set; } = string.Empty; // "FrontDesk", "Housekeeping", etc.

    public bool RequiresRoomDelivery { get; set; } = false; // If true, guest must be checked in with valid room

    public bool RequiresAdvanceBooking { get; set; } = false; // If true, cannot be immediate request

    [MaxLength(50)]
    public string? ContactMethod { get; set; } // "phone", "app", "front-desk", "concierge"

    [MaxLength(20)]
    public string Priority { get; set; } = "Normal"; // "Low", "Normal", "High", "Urgent"

    [MaxLength(500)]
    public string? SpecialInstructions { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual Tenant Tenant { get; set; } = null!;
    public virtual TenantDepartment? Department { get; set; }
}

public enum HotelSize
{
    Small,      // 1-50 rooms
    Medium,     // 51-150 rooms
    Large,      // 151+ rooms
    Resort      // Full-service resort
}

public static class DepartmentDefaults
{
    public static readonly Dictionary<HotelSize, List<string>> DefaultDepartments = new()
    {
        [HotelSize.Small] = new() { "FrontDesk", "Housekeeping", "Maintenance" },
        [HotelSize.Medium] = new() { "FrontDesk", "Housekeeping", "Maintenance", "Concierge", "FoodService" },
        [HotelSize.Large] = new() { "FrontDesk", "Housekeeping", "Maintenance", "Concierge", "FoodService", "Security", "IT" },
        [HotelSize.Resort] = new() { "FrontDesk", "Housekeeping", "Maintenance", "Concierge", "FoodService", "Security", "IT", "Events", "Recreation", "Spa" }
    };

    public static readonly Dictionary<string, string> DefaultServiceMappings = new()
    {
        // Revenue-generating services → FrontDesk
        ["Local Tours"] = "FrontDesk",
        ["Transportation"] = "FrontDesk",
        ["Accommodation"] = "FrontDesk",
        ["Business"] = "FrontDesk",

        // Food & Beverage → FoodService (fallback to FrontDesk if no FoodService)
        ["Dining"] = "FoodService",
        ["Food & Beverage"] = "FoodService",

        // Physical delivery items → Housekeeping
        ["Electronics"] = "Housekeeping",
        ["Amenities"] = "Housekeeping",
        ["Laundry"] = "Housekeeping",

        // Information and coordination → Concierge (fallback to FrontDesk)
        ["Concierge"] = "Concierge",
        ["Information"] = "Concierge",

        // Wellness → Concierge or Spa department
        ["Wellness"] = "Concierge",
        ["Spa"] = "Spa"
    };
}