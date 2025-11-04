using System.ComponentModel.DataAnnotations;

namespace Hostr.Api.Models;

/// <summary>
/// Stores tenant-specific configuration settings
/// All business rules that vary per hotel should be stored here
/// </summary>
public class TenantSetting
{
    [Key]
    public int Id { get; set; }

    public int TenantId { get; set; }

    // Check-in/out times
    public TimeSpan StandardCheckInTime { get; set; } = new TimeSpan(15, 0, 0); // 3:00 PM
    public TimeSpan StandardCheckOutTime { get; set; } = new TimeSpan(12, 0, 0); // 12:00 PM

    // Late checkout pricing (null = free)
    public decimal? LateCheckoutFeePerHour { get; set; }

    // Early check-in pricing (null = free)
    public decimal? EarlyCheckInFeePerHour { get; set; }

    // Business hours
    public TimeSpan? BusinessHoursStart { get; set; } = new TimeSpan(8, 0, 0); // 8:00 AM
    public TimeSpan? BusinessHoursEnd { get; set; } = new TimeSpan(22, 0, 0); // 10:00 PM

    // Currency and localization
    [MaxLength(3)]
    public string DefaultCurrency { get; set; } = "ZAR"; // South African Rand

    [MaxLength(50)]
    public string Timezone { get; set; } = "Africa/Johannesburg";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual Tenant Tenant { get; set; } = null!;
}
