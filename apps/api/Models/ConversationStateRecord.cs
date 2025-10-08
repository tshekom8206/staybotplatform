using System.ComponentModel.DataAnnotations;

namespace Hostr.Api.Models;

/// <summary>
/// Tracks pending clarifications and multi-turn conversation flows
/// </summary>
public class ConversationStateRecord
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int ConversationId { get; set; }

    /// <summary>
    /// Type of state/clarification needed
    /// </summary>
    [Required, MaxLength(50)]
    public string StateType { get; set; } = string.Empty;  // Maps to ConversationStateType enum

    /// <summary>
    /// What entity type are we waiting on? (LostItem, Booking, Upsell, etc.)
    /// </summary>
    [Required, MaxLength(50)]
    public string EntityType { get; set; } = string.Empty;

    /// <summary>
    /// The ID of the entity we're tracking
    /// </summary>
    public int EntityId { get; set; }

    /// <summary>
    /// What specific field/information are we waiting for?
    /// </summary>
    [MaxLength(50)]
    public string? PendingField { get; set; }

    /// <summary>
    /// Additional context data (JSON)
    /// </summary>
    public string? ContextData { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }

    // Navigation properties
    public virtual Tenant Tenant { get; set; } = null!;
    public virtual Conversation Conversation { get; set; } = null!;
}

/// <summary>
/// Enum for conversation state types
/// </summary>
public static class ConversationStateType
{
    public const string AwaitingClarification = "AwaitingClarification";
    public const string AwaitingConfirmation = "AwaitingConfirmation";
    public const string ProcessingPayment = "ProcessingPayment";
    public const string GatheringInformation = "GatheringInformation";
}

/// <summary>
/// Enum for entity types that can have pending states
/// </summary>
public static class EntityType
{
    public const string LostItem = "LostItem";
    public const string Booking = "Booking";
    public const string Upsell = "Upsell";
    public const string StaffTask = "StaffTask";
    public const string MaintenanceRequest = "MaintenanceRequest";
}

/// <summary>
/// Enum for pending field types
/// </summary>
public static class PendingField
{
    // Lost & Found
    public const string Location = "Location";
    public const string Description = "Description";
    public const string Color = "Color";

    // Booking
    public const string CheckInDate = "CheckInDate";
    public const string CheckOutDate = "CheckOutDate";
    public const string GuestCount = "GuestCount";

    // Upsell
    public const string Confirmation = "Confirmation";
    public const string Quantity = "Quantity";
}
