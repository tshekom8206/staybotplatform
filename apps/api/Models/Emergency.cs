using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace Hostr.Api.Models;

public class EmergencyType
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty; // Medical, Fire, Security, Natural disaster, Technical emergency
    
    [MaxLength(500)]
    public string? Description { get; set; }
    
    public string[] DetectionKeywords { get; set; } = Array.Empty<string>(); // Keywords for detection
    
    [Required, MaxLength(20)]
    public string SeverityLevel { get; set; } = "High"; // Low, Medium, High, Critical
    
    public bool AutoEscalate { get; set; } = true; // Automatically create incident
    public bool RequiresEvacuation { get; set; } = false;
    public bool ContactEmergencyServices { get; set; } = false;
    public bool IsActive { get; set; } = true;
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual Tenant Tenant { get; set; } = null!;
    public virtual ICollection<EmergencyIncident> EmergencyIncidents { get; set; } = new List<EmergencyIncident>();
    public virtual ICollection<EmergencyProtocol> EmergencyProtocols { get; set; } = new List<EmergencyProtocol>();
}

public class EmergencyIncident
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int EmergencyTypeId { get; set; }
    public int? ConversationId { get; set; } // Source conversation if reported by guest
    
    [Required, MaxLength(100)]
    public string Title { get; set; } = string.Empty;
    
    [Required]
    public string Description { get; set; } = string.Empty;
    
    [Required, MaxLength(20)]
    public string Status { get; set; } = "ACTIVE"; // ACTIVE, RESOLVED, FALSE_ALARM
    
    [Required, MaxLength(20)]
    public string SeverityLevel { get; set; } = "High"; // Low, Medium, High, Critical
    
    [MaxLength(100)]
    public string? ReportedBy { get; set; } // Guest phone or staff member
    
    [MaxLength(100)]
    public string? Location { get; set; } // Room, area, or general location
    
    public string[] AffectedAreas { get; set; } = Array.Empty<string>(); // Rooms/areas affected
    
    public JsonDocument? ResponseActions { get; set; } // Actions taken in response
    
    public DateTime ReportedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
    
    public string? ResolutionNotes { get; set; }
    
    // Navigation properties
    public virtual Tenant Tenant { get; set; } = null!;
    public virtual EmergencyType EmergencyType { get; set; } = null!;
    public virtual Conversation? Conversation { get; set; }
    public virtual ICollection<StaffTask> StaffTasks { get; set; } = new List<StaffTask>();
}

public class EmergencyProtocol
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int EmergencyTypeId { get; set; }
    
    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;
    
    [Required]
    public string ProcedureSteps { get; set; } = string.Empty; // Step-by-step procedure
    
    [MaxLength(20)]
    public string TriggerCondition { get; set; } = "IMMEDIATE"; // IMMEDIATE, SEVERITY_HIGH, MANUAL
    
    public bool NotifyGuests { get; set; } = false;
    public bool NotifyStaff { get; set; } = true;
    public bool NotifyEmergencyServices { get; set; } = false;
    
    [MaxLength(500)]
    public string? GuestMessage { get; set; } // Message to broadcast to guests
    
    [MaxLength(500)]
    public string? StaffMessage { get; set; } // Message to send to staff
    
    public string[] EmergencyContacts { get; set; } = Array.Empty<string>(); // Contact IDs to notify
    
    public bool IsActive { get; set; } = true;
    public int ExecutionOrder { get; set; } = 0; // Order of execution
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual Tenant Tenant { get; set; } = null!;
    public virtual EmergencyType EmergencyType { get; set; } = null!;
}

public class EmergencyContact
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    
    [Required, MaxLength(100)]
    public string ContactType { get; set; } = string.Empty; // Fire Department, Police, Medical, Security, Manager
    
    [Required, MaxLength(20)]
    public string PhoneNumber { get; set; } = string.Empty;
    
    [MaxLength(100)]
    public string? Email { get; set; }
    
    [MaxLength(200)]
    public string? Address { get; set; }
    
    [MaxLength(500)]
    public string? Notes { get; set; }
    
    public bool IsPrimary { get; set; } = false; // Primary contact for this type
    public bool IsActive { get; set; } = true;
    public bool IsPublic { get; set; } = false; // Whether guests can see this contact
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual Tenant Tenant { get; set; } = null!;
}

/// <summary>
/// Audit trail for emergency contact attempts - tracks all attempts to contact emergency services
/// </summary>
public class EmergencyContactAttempt
{
    public int Id { get; set; }
    public int IncidentId { get; set; }
    public int TenantId { get; set; }

    [Required, MaxLength(50)]
    public string ContactMethod { get; set; } = string.Empty; // SMS, Voice, Email

    [MaxLength(100)]
    public string? ContactName { get; set; }

    [MaxLength(20)]
    public string? ContactNumber { get; set; }

    public bool Success { get; set; }

    [MaxLength(500)]
    public string? Details { get; set; } // Success message or error details

    [MaxLength(100)]
    public string? MessageId { get; set; } // Twilio SID or email message ID

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(100)]
    public string? AttemptedBy { get; set; } // System or User ID who initiated

    // Navigation properties
    public virtual EmergencyIncident Incident { get; set; } = null!;
    public virtual Tenant Tenant { get; set; } = null!;
}