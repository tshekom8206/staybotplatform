using System.ComponentModel.DataAnnotations;

namespace Hostr.Api.Models;

public class ResponseTemplate
{
    public int Id { get; set; }

    [Required]
    public int TenantId { get; set; }

    [Required, MaxLength(100)]
    public string TemplateKey { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string Category { get; set; } = string.Empty;

    [MaxLength(10)]
    public string Language { get; set; } = "en";

    [Required]
    public string Template { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public int Priority { get; set; } = 1;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual Tenant Tenant { get; set; } = null!;
}

public class ResponseVariable
{
    public int Id { get; set; }

    [Required]
    public int TenantId { get; set; }

    [Required, MaxLength(50)]
    public string VariableName { get; set; } = string.Empty;

    [Required]
    public string VariableValue { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? Category { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual Tenant Tenant { get; set; } = null!;
}

// DTO for template processing
public class ProcessedTemplate
{
    public string Content { get; set; } = string.Empty;
    public Dictionary<string, string> UsedVariables { get; set; } = new();
    public List<string> MissingVariables { get; set; } = new();
}

// Constants for template keys
public static class ResponseTemplateKeys
{
    // Service requests
    public const string ServiceRequestLaundryAvailable = "service_request_laundry_available";
    public const string ServiceRequestLaundryUnavailable = "service_request_laundry_unavailable";
    public const string ServiceRequestHousekeepingAvailable = "service_request_housekeeping_available";
    public const string ServiceRequestHousekeepingUnavailable = "service_request_housekeeping_unavailable";
    public const string ServiceRequestRoomServiceAvailable = "service_request_room_service_available";
    public const string ServiceRequestRoomServiceUnavailable = "service_request_room_service_unavailable";

    // Contextual responses
    public const string AcknowledgmentPositive = "acknowledgment_positive";
    public const string AcknowledgmentNegative = "acknowledgment_negative";
    public const string AnythingElsePositive = "anything_else_positive";
    public const string AnythingElseNegative = "anything_else_negative";
    public const string FrontDeskConnection = "front_desk_connection";
    public const string TemperatureComplaint = "temperature_complaint";

    // WiFi support
    public const string WiFiTroubleshootingFollowUp = "wifi_troubleshooting_followup";
    public const string WiFiTechnicalSupport = "wifi_technical_support";
    public const string WiFiWorkingConfirmation = "wifi_working_confirmation";
    public const string WiFiStillNotWorking = "wifi_still_not_working";

    // Emergency and maintenance
    public const string EmergencyDetected = "emergency_detected";
    public const string MaintenanceUrgent = "maintenance_urgent";
    public const string MaintenanceStandard = "maintenance_standard";

    // Menu responses
    public const string MenuPriceInquiry = "menu_price_inquiry";
    public const string MenuFullRequest = "menu_full_request";
    public const string MenuMoreDetails = "menu_more_details";
    public const string MenuDefault = "menu_default";

    // Fallback responses
    public const string FallbackClarification = "fallback_clarification";
    public const string FallbackGeneralHelp = "fallback_general_help";

    // Time-based responses
    public const string LaundryScheduleConfirmation = "laundry_schedule_confirmation";
    public const string HousekeepingScheduleConfirmation = "housekeeping_schedule_confirmation";
    public const string FoodDeliveryConfirmation = "food_delivery_confirmation";

    // Item requests
    public const string TowelDeliveryConfirmation = "towel_delivery_confirmation";
    public const string ChargerRequest = "charger_request";
    public const string ChargerDeliveryConfirmation = "charger_delivery_confirmation";
    public const string IronDeliveryConfirmation = "iron_delivery_confirmation";
    public const string ToiletPaperDeliveryConfirmation = "toilet_paper_delivery_confirmation";
    public const string AmenityDeliveryConfirmation = "amenity_delivery_confirmation";
}

// Constants for variable names
public static class ResponseVariableNames
{
    // Hotel/Brand info
    public const string HotelName = "hotel_name";
    public const string BrandVoice = "brand_voice";
    public const string Phone = "phone";
    public const string Email = "email";
    public const string Website = "website";

    // Dynamic context
    public const string RoomNumber = "room_number";
    public const string GuestName = "guest_name";
    public const string TimeOfDay = "time_of_day";
    public const string CurrentTime = "current_time";

    // Service context
    public const string ServiceName = "service_name";
    public const string ServiceStatus = "service_status";
    public const string AvailabilityMessage = "availability_message";
    public const string Quantity = "quantity";
    public const string TimeMessage = "time_message";
    public const string ChargerType = "charger_type";
    public const string AmenityName = "amenity_name";
}