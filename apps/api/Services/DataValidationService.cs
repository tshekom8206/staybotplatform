using System.Text.RegularExpressions;

namespace Hostr.Api.Services;

public interface IDataValidationService
{
    string StandardizeDepartmentName(string? department);
    string FormatPhoneNumber(string? phoneNumber);
    bool IsValidRating(int rating);
    string SanitizeComment(string? comment);
    string StandardizeTaskStatus(string? status);
    string StandardizeTaskPriority(string? priority);
}

public class DataValidationService : IDataValidationService
{
    // Standard department names mapping
    private static readonly Dictionary<string, string> DepartmentMappings = new()
    {
        { "front desk", "FrontDesk" },
        { "frontdesk", "FrontDesk" },
        { "front_desk", "FrontDesk" },
        { "reception", "FrontDesk" },
        { "housekeeping", "Housekeeping" },
        { "house keeping", "Housekeeping" },
        { "cleaning", "Housekeeping" },
        { "maintenance", "Maintenance" },
        { "technical", "Maintenance" },
        { "food & beverage", "FoodService" },
        { "food and beverage", "FoodService" },
        { "foodservice", "FoodService" },
        { "food_service", "FoodService" },
        { "restaurant", "FoodService" },
        { "kitchen", "FoodService" },
        { "concierge", "Concierge" },
        { "guest services", "Concierge" },
        { "tours", "Concierge" },
        { "recreation", "Recreation" },
        { "activities", "Recreation" },
        { "general", "General" },
        { "other", "General" },
        { "", "General" }
    };

    public string StandardizeDepartmentName(string? department)
    {
        if (string.IsNullOrWhiteSpace(department))
            return "General";

        var normalized = department.Trim().ToLowerInvariant();

        // Check for exact match first
        if (DepartmentMappings.TryGetValue(normalized, out var standardName))
            return standardName;

        // Check for partial matches
        foreach (var mapping in DepartmentMappings)
        {
            if (normalized.Contains(mapping.Key) && !string.IsNullOrEmpty(mapping.Key))
                return mapping.Value;
        }

        // If no match found, capitalize first letter of each word
        return string.Join("", department.Split(' ')
            .Select(word => char.ToUpperInvariant(word[0]) + word[1..].ToLowerInvariant()));
    }

    public string FormatPhoneNumber(string? phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            return string.Empty;

        // Remove all non-digit characters
        var digitsOnly = Regex.Replace(phoneNumber, @"[^\d]", "");

        // If it doesn't start with country code, assume South African (+27)
        if (digitsOnly.StartsWith("0") && digitsOnly.Length == 10)
        {
            // Convert local format (0XX) to international (27XX)
            digitsOnly = "27" + digitsOnly[1..];
        }
        else if (digitsOnly.Length == 9 && !digitsOnly.StartsWith("27"))
        {
            // Add South African country code if missing
            digitsOnly = "27" + digitsOnly;
        }

        // Ensure it starts with +
        return digitsOnly.StartsWith("+") ? digitsOnly : "+" + digitsOnly;
    }

    public bool IsValidRating(int rating)
    {
        return rating >= 1 && rating <= 5;
    }

    public string SanitizeComment(string? comment)
    {
        if (string.IsNullOrWhiteSpace(comment))
            return string.Empty;

        // Trim whitespace and limit length
        var sanitized = comment.Trim();

        // Limit to 500 characters
        if (sanitized.Length > 500)
            sanitized = sanitized[..500] + "...";

        // Remove potentially harmful characters
        sanitized = Regex.Replace(sanitized, @"[<>""']", "");

        return sanitized;
    }

    public string StandardizeTaskStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return "Open";

        var normalized = status.Trim().ToLowerInvariant();

        return normalized switch
        {
            "completed" or "done" or "complete" => "Completed",
            "inprogress" or "in_progress" or "in progress" or "working" => "InProgress",
            "pending" or "waiting" => "Pending",
            "open" or "new" or "created" => "Open",
            "cancelled" or "canceled" => "Cancelled",
            _ => "Open"
        };
    }

    public string StandardizeTaskPriority(string? priority)
    {
        if (string.IsNullOrWhiteSpace(priority))
            return "Normal";

        var normalized = priority.Trim().ToLowerInvariant();

        return normalized switch
        {
            "urgent" or "high" or "critical" or "4" or "5" => "High",
            "medium" or "med" or "moderate" or "2" or "3" => "Medium",
            "low" or "minor" or "1" => "Low",
            "normal" or "regular" or "standard" => "Normal",
            _ => "Normal"
        };
    }
}