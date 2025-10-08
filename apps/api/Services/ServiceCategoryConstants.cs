namespace Hostr.Api.Services;

/// <summary>
/// Standardized service category constants used across the entire system.
/// These categories must be synchronized between:
/// - LLM Business Rules Engine (intent classification)
/// - Message Routing Service (Integration Point A detection)
/// - Information Gathering Service (field requirements)
/// </summary>
public static class ServiceCategoryConstants
{
    // Bookable service categories - require structured information gathering
    public const string MASSAGE = "MASSAGE";
    public const string SPA = "SPA";
    public const string LOCAL_TOURS = "LOCAL_TOURS";
    public const string CONFERENCE_ROOM = "CONFERENCE_ROOM";
    public const string DINING = "DINING";
    public const string ACTIVITIES = "ACTIVITIES";

    // Non-bookable service categories
    public const string FOOD_BEVERAGE = "FOOD_BEVERAGE";
    public const string HOUSEKEEPING = "HOUSEKEEPING";
    public const string MAINTENANCE = "MAINTENANCE";
    public const string CONCIERGE = "CONCIERGE";

    // Legacy/alias categories (for backward compatibility)
    public const string SPA_WELLNESS = "SPA_WELLNESS";  // Maps to MASSAGE or SPA
    public const string ACTIVITIES_EXPERIENCES = "ACTIVITIES_EXPERIENCES";  // Maps to LOCAL_TOURS or ACTIVITIES
    public const string WELLNESS = "Wellness";  // Maps to MASSAGE
    public const string BUSINESS = "Business";  // Maps to CONFERENCE_ROOM

    /// <summary>
    /// All bookable categories that should trigger information gathering flow
    /// </summary>
    public static readonly string[] BookableCategories = new[]
    {
        MASSAGE,
        SPA,
        LOCAL_TOURS,
        CONFERENCE_ROOM,
        DINING,
        ACTIVITIES
    };

    /// <summary>
    /// Maps legacy/alias category names to canonical category names
    /// </summary>
    public static string NormalizeCategory(string category)
    {
        if (string.IsNullOrEmpty(category))
            return category;

        var normalized = category.ToUpperInvariant().Replace(" ", "_");

        // Map legacy names to canonical names
        return normalized switch
        {
            "SPA_WELLNESS" => MASSAGE,  // SPA_WELLNESS defaults to MASSAGE
            "WELLNESS" => MASSAGE,
            "ACTIVITIES_EXPERIENCES" => LOCAL_TOURS,  // ACTIVITIES_EXPERIENCES defaults to LOCAL_TOURS
            "BUSINESS" => CONFERENCE_ROOM,
            "LOCAL TOURS" => LOCAL_TOURS,
            _ => category  // Return original if no mapping found
        };
    }

    /// <summary>
    /// Checks if a category is bookable (requires information gathering)
    /// </summary>
    public static bool IsBookable(string category)
    {
        if (string.IsNullOrEmpty(category))
            return false;

        // Normalize first, then check
        var normalized = NormalizeCategory(category);
        return Array.Exists(BookableCategories, c =>
            string.Equals(c, normalized, StringComparison.OrdinalIgnoreCase));
    }
}
