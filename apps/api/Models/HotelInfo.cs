using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Hostr.Api.Models;

public class HotelInfo
{
    [Key]
    public int Id { get; set; }

    public int TenantId { get; set; }

    // Basic Information
    [MaxLength(1000)]
    public string? Description { get; set; }

    [MaxLength(50)]
    public string? Category { get; set; } // luxury, premium, comfort, boutique, business, resort

    [MaxLength(500)]
    public string? LogoUrl { get; set; }

    // Contact Information
    [MaxLength(20)]
    public string? Phone { get; set; }

    [MaxLength(100)]
    public string? Email { get; set; }

    [MaxLength(200)]
    public string? Website { get; set; }

    // Address
    [MaxLength(200)]
    public string? Street { get; set; }

    [MaxLength(100)]
    public string? City { get; set; }

    [MaxLength(100)]
    public string? State { get; set; }

    [MaxLength(20)]
    public string? PostalCode { get; set; }

    [MaxLength(100)]
    public string? Country { get; set; }

    // Geolocation for weather and maps
    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    // Business Details
    [MaxLength(5)]
    public string? CheckInTime { get; set; } // Format: HH:MM

    [MaxLength(5)]
    public string? CheckOutTime { get; set; } // Format: HH:MM

    public int? NumberOfRooms { get; set; }

    public int? NumberOfFloors { get; set; }

    public int? EstablishedYear { get; set; }

    // Languages (stored as JSON)
    [MaxLength(1000)]
    public string? SupportedLanguages { get; set; } // JSON array of language codes

    [MaxLength(5)]
    public string? DefaultLanguage { get; set; } = "en";

    // Features (stored as JSON)
    [MaxLength(2000)]
    public string? Features { get; set; } // JSON array of features

    // Social Media
    [MaxLength(200)]
    public string? FacebookUrl { get; set; }

    [MaxLength(200)]
    public string? TwitterUrl { get; set; }

    [MaxLength(200)]
    public string? InstagramUrl { get; set; }

    [MaxLength(200)]
    public string? LinkedInUrl { get; set; }

    // Policies
    [MaxLength(1000)]
    public string? CancellationPolicy { get; set; }

    [MaxLength(1000)]
    public string? PetPolicy { get; set; }

    [MaxLength(1000)]
    public string? SmokingPolicy { get; set; }

    [MaxLength(1000)]
    public string? ChildPolicy { get; set; }

    // Configuration Settings
    public bool AllowOnlineBooking { get; set; } = true;

    public bool RequirePhoneVerification { get; set; } = true;

    public bool EnableNotifications { get; set; } = true;

    public bool EnableChatbot { get; set; } = true;

    [MaxLength(50)]
    public string? Currency { get; set; } = "ZAR";

    // WiFi Credentials for Guest Portal
    [MaxLength(100)]
    public string? WifiNetwork { get; set; }

    [MaxLength(100)]
    public string? WifiPassword { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual Tenant Tenant { get; set; } = null!;
}

public class HotelCategory
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(50)]
    public string Value { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Label { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public int SortOrder { get; set; }
}

public class SupportedLanguage
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(5)]
    public string Code { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public int SortOrder { get; set; }
}

public class HotelFeature
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? Icon { get; set; }

    [MaxLength(50)]
    public string? Category { get; set; } // accommodation, dining, wellness, business, etc.

    public bool IsActive { get; set; } = true;

    public int SortOrder { get; set; }
}

public class SupportedCurrency
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(3)]
    public string Code { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(5)]
    public string? Symbol { get; set; }

    public bool IsActive { get; set; } = true;

    public int SortOrder { get; set; }
}