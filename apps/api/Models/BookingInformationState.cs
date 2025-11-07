using System;
using System.Collections.Generic;

namespace Hostr.Api.Models;

/// <summary>
/// Tracks the state of information gathering for a booking request
/// </summary>
public class BookingInformationState
{
    /// <summary>
    /// Specific service name (e.g., "Kruger National Park Day Trip")
    /// </summary>
    public string? ServiceName { get; set; }

    /// <summary>
    /// Service category (LOCAL_TOURS, MASSAGE, CONFERENCE_ROOM, HOUSEKEEPING_ITEMS, DINING)
    /// </summary>
    public string? ServiceCategory { get; set; }

    /// <summary>
    /// Meal type for DINING bookings (breakfast, lunch, dinner, or null for non-dining)
    /// </summary>
    public string? MealType { get; set; }

    /// <summary>
    /// Location/Restaurant name for DINING bookings (e.g., "Main Restaurant", "Pool Bar", etc.)
    /// </summary>
    public string? Location { get; set; }

    /// <summary>
    /// Number of people for the booking (required for tours, massages, conferences)
    /// </summary>
    public int? NumberOfPeople { get; set; }

    /// <summary>
    /// Requested date for the service
    /// </summary>
    public DateOnly? RequestedDate { get; set; }

    /// <summary>
    /// Requested time for the service
    /// </summary>
    public TimeOnly? RequestedTime { get; set; }

    /// <summary>
    /// Any special requests from the guest
    /// </summary>
    public string? SpecialRequests { get; set; }

    /// <summary>
    /// Fields that have been extracted and provided by the guest
    /// </summary>
    public List<string> ProvidedFields { get; set; } = new();

    /// <summary>
    /// Required fields that are still missing
    /// </summary>
    public List<string> MissingRequiredFields { get; set; } = new();

    /// <summary>
    /// Number of clarifying questions asked so far
    /// </summary>
    public int QuestionAttempts { get; set; } = 0;

    /// <summary>
    /// Maximum number of clarifying questions before escalation
    /// </summary>
    public const int MAX_QUESTIONS = 3;

    /// <summary>
    /// Number of times user has asked for booking confirmation
    /// </summary>
    public int ConfirmationAttempts { get; set; } = 0;

    /// <summary>
    /// Maximum number of confirmation attempts before forcing database lookup
    /// </summary>
    public const int MAX_CONFIRMATION_ATTEMPTS = 2;

    /// <summary>
    /// Confidence level of the extraction (0.0 to 1.0)
    /// </summary>
    public double ExtractionConfidence { get; set; } = 0.0;

    /// <summary>
    /// Reasoning from the LLM about what was extracted
    /// </summary>
    public string? ExtractionReasoning { get; set; }

    /// <summary>
    /// When this state was last updated
    /// </summary>
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Check if all required fields are provided
    /// </summary>
    public bool IsComplete()
    {
        return MissingRequiredFields.Count == 0;
    }

    /// <summary>
    /// Check if we've exceeded the maximum question limit
    /// </summary>
    public bool ExceededQuestionLimit()
    {
        return QuestionAttempts >= MAX_QUESTIONS;
    }
}

/// <summary>
/// Service requirements configuration for different service categories
/// </summary>
public class ServiceRequirements
{
    public string[] RequiredFields { get; set; } = Array.Empty<string>();
    public string[] OptionalFields { get; set; } = Array.Empty<string>();
    public int MinPeople { get; set; } = 1;
    public int MaxPeople { get; set; } = 100;
    public bool RequiresAdvanceBooking { get; set; } = false;
    public int MinAdvanceHours { get; set; } = 0;
    public int DefaultQuantity { get; set; } = 1;
    public bool ImmediateDelivery { get; set; } = false;
}

/// <summary>
/// Result from LLM follow-up detection
/// </summary>
public class FollowUpDetectionResult
{
    public bool IsFollowUp { get; set; }
    public string? ReferenceType { get; set; } // "pronoun", "confirmation", "quantity_update"
    public string? ReferencedService { get; set; }
    public int? ExtractedQuantity { get; set; }
    public double Confidence { get; set; }
    public string? Reasoning { get; set; }
}

/// <summary>
/// Result from LLM intent detection (cancel, topic switch, etc.)
/// </summary>
public class IntentDetectionResult
{
    public string Intent { get; set; } = string.Empty; // "cancel", "topic_switch", "continue", "other"
    public double Confidence { get; set; }
    public string? NewTopic { get; set; }
    public string? Reasoning { get; set; }
}

/// <summary>
/// Result from booking validation
/// </summary>
public class BookingValidationResult
{
    public bool IsValid { get; set; }
    public string? ErrorType { get; set; } // "capacity", "advance_booking", "availability", "date_past"
    public string? ErrorMessage { get; set; }
    public string? SuggestedAlternative { get; set; }
    public string? Reasoning { get; set; }
}

/// <summary>
/// Result from confirmation request extraction
/// </summary>
public class ConfirmationRequest
{
    public bool IsConfirmationRequest { get; set; }
    public string? ServiceCategory { get; set; } // DINING, LOCAL_TOURS, etc.
    public int? NumberOfPeople { get; set; }
    public DateOnly? RequestedDate { get; set; }
    public TimeOnly? RequestedTime { get; set; }
    public string? Location { get; set; }
    public double Confidence { get; set; }
    public string? Reasoning { get; set; }
}
