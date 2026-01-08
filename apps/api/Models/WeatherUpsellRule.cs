using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Hostr.Api.Models;

/// <summary>
/// Defines weather-based upselling rules for promoting services based on current weather conditions
/// </summary>
public class WeatherUpsellRule
{
    public int Id { get; set; }

    public int TenantId { get; set; }

    /// <summary>
    /// Weather condition type: hot, warm, mild, cold, rainy, stormy, cloudy
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string WeatherCondition { get; set; } = string.Empty;

    /// <summary>
    /// Minimum temperature in Celsius for this rule to apply (nullable)
    /// </summary>
    public int? MinTemperature { get; set; }

    /// <summary>
    /// Maximum temperature in Celsius for this rule to apply (nullable)
    /// </summary>
    public int? MaxTemperature { get; set; }

    /// <summary>
    /// JSON array of WMO weather codes that trigger this rule (e.g., [61,63,65] for rain)
    /// </summary>
    public string? WeatherCodes { get; set; }

    /// <summary>
    /// JSON array of Service IDs to promote when this rule matches
    /// </summary>
    [Required]
    public string ServiceIds { get; set; } = "[]";

    /// <summary>
    /// Banner text to display (e.g., "Perfect pool weather today!")
    /// </summary>
    [MaxLength(200)]
    public string? BannerText { get; set; }

    /// <summary>
    /// Bootstrap icon name for the banner (e.g., "sun", "cloud-rain")
    /// </summary>
    [MaxLength(50)]
    public string? BannerIcon { get; set; }

    /// <summary>
    /// Priority for ordering rules (higher = shown first)
    /// </summary>
    public int Priority { get; set; } = 0;

    /// <summary>
    /// Whether this rule is active
    /// </summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    [ForeignKey("TenantId")]
    public Tenant? Tenant { get; set; }
}

/// <summary>
/// Predefined weather condition types with their typical characteristics
/// </summary>
public static class WeatherConditionTypes
{
    public const string Hot = "hot";         // >28°C, clear/sunny
    public const string Warm = "warm";       // 20-28°C
    public const string Mild = "mild";       // 15-20°C
    public const string Cold = "cold";       // <15°C
    public const string Rainy = "rainy";     // WMO codes 51-67, 80-82
    public const string Stormy = "stormy";   // WMO codes 95-99
    public const string Cloudy = "cloudy";   // WMO codes 1-3, 45-48

    public static readonly string[] All = { Hot, Warm, Mild, Cold, Rainy, Stormy, Cloudy };

    /// <summary>
    /// WMO weather codes that indicate rain
    /// </summary>
    public static readonly int[] RainCodes = { 51, 53, 55, 56, 57, 61, 63, 65, 66, 67, 80, 81, 82 };

    /// <summary>
    /// WMO weather codes that indicate thunderstorms
    /// </summary>
    public static readonly int[] StormCodes = { 95, 96, 99 };

    /// <summary>
    /// WMO weather codes that indicate clear/sunny weather
    /// </summary>
    public static readonly int[] ClearCodes = { 0, 1, 2, 3 };

    /// <summary>
    /// WMO weather codes that indicate fog/cloudy
    /// </summary>
    public static readonly int[] CloudyCodes = { 45, 48 };
}
