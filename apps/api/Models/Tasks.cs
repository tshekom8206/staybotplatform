using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace Hostr.Api.Models;

public class RequestItem
{
    public int Id { get; set; }
    public int TenantId { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(50)]
    public string Category { get; set; } = string.Empty;

    [MaxLength(50)]
    public string Department { get; set; } = "General"; // Housekeeping|Maintenance|FrontDesk|Concierge|FoodService|General

    [MaxLength(100)]
    public string? Purpose { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    public bool IsAvailable { get; set; } = true;
    public bool RequiresQuantity { get; set; } = true;
    public int DefaultQuantityLimit { get; set; } = 10;
    public bool RequiresRoomDelivery { get; set; } = true;

    // Premium fields
    public int StockCount { get; set; } = 0;
    public int InServiceCount { get; set; } = 0;
    public bool AutoDecrementOnTask { get; set; } = false;
    public int LowStockThreshold { get; set; } = 5;

    [MaxLength(100)]
    public string LlmVisibleName { get; set; } = string.Empty;

    public string? NotesForStaff { get; set; }

    public JsonDocument? ServiceHours { get; set; }

    public int SlaMinutes { get; set; } = 30;

    // Frontend-expected fields
    public int? EstimatedTime { get; set; } // Estimated completion time in minutes
    public bool IsUrgent { get; set; } = false;
    public int DisplayOrder { get; set; } = 0;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual Tenant Tenant { get; set; } = null!;
    public virtual ICollection<StaffTask> StaffTasks { get; set; } = new List<StaffTask>();
    public virtual ICollection<StockEvent> StockEvents { get; set; } = new List<StockEvent>();
    public virtual ICollection<RequestItemRule> BusinessRules { get; set; } = new List<RequestItemRule>();
}

public class StaffTask
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int? ConversationId { get; set; }
    public int? RequestItemId { get; set; }
    public int? BookingId { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required, MaxLength(50)]
    public string TaskType { get; set; } = "deliver_item"; // deliver_item|collect_item|maintenance|frontdesk|concierge|general

    [Required, MaxLength(50)]
    public string Department { get; set; } = "General"; // Housekeeping|Maintenance|FrontDesk|Concierge|FoodService|General

    public int Quantity { get; set; } = 1;

    [MaxLength(10)]
    public string? RoomNumber { get; set; }

    [MaxLength(100)]
    public string? GuestName { get; set; }

    [MaxLength(20)]
    public string? GuestPhone { get; set; }

    [Required, MaxLength(20)]
    public string Status { get; set; } = "Open"; // Open|InProgress|Completed|Cancelled|Pending

    [Required, MaxLength(10)]
    public string Priority { get; set; } = "Normal"; // Low|Normal|High|Urgent

    public string? Notes { get; set; }

    public int? CreatedBy { get; set; }
    public int? AssignedToId { get; set; }
    public int? CompletedBy { get; set; }
    public int? MaintenanceRequestId { get; set; } // For maintenance-related tasks

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EstimatedCompletionTime { get; set; }
    public DateTime? CompletedAt { get; set; }

    // Navigation properties
    public virtual Tenant Tenant { get; set; } = null!;
    public virtual Conversation? Conversation { get; set; }
    public virtual RequestItem? RequestItem { get; set; }
    public virtual User? CreatedByUser { get; set; }
    public virtual User? AssignedToUser { get; set; }
    public virtual User? CompletedByUser { get; set; }
    public virtual Booking? Booking { get; set; }
    public virtual MaintenanceRequest? MaintenanceRequest { get; set; }
}

public class StockEvent
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int RequestItemId { get; set; }
    
    public int Delta { get; set; } // Positive = added, Negative = removed
    
    [Required, MaxLength(50)]
    public string Reason { get; set; } = string.Empty; // restock|task_delivery|task_collection|manual_adjustment|lost_damaged
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual Tenant Tenant { get; set; } = null!;
    public virtual RequestItem RequestItem { get; set; } = null!;
}

public class MaintenanceItem
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty; // AC Unit #1, Elevator A, Pool Pump
    
    [Required, MaxLength(50)]
    public string Category { get; set; } = string.Empty; // HVAC, Elevator, Plumbing, Electrical, Pool, General
    
    [MaxLength(100)]
    public string? Location { get; set; } // Building 1, Floor 2, Room 205
    
    [MaxLength(100)]
    public string? Manufacturer { get; set; }
    
    [MaxLength(100)]
    public string? ModelNumber { get; set; }
    
    public DateTime? LastServiceDate { get; set; }
    public DateTime? NextScheduledService { get; set; }
    
    public int ServiceIntervalDays { get; set; } = 0; // 0 = no scheduled service
    
    [Required, MaxLength(20)]
    public string Status { get; set; } = "Operational"; // Operational, Out of Service, Under Maintenance
    
    public string? Notes { get; set; }
    
    public bool IsActive { get; set; } = true;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual Tenant Tenant { get; set; } = null!;
    public virtual ICollection<MaintenanceRequest> MaintenanceRequests { get; set; } = new List<MaintenanceRequest>();
    public virtual ICollection<MaintenanceHistory> MaintenanceHistory { get; set; } = new List<MaintenanceHistory>();
}

public class MaintenanceRequest
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int? MaintenanceItemId { get; set; } // null for general maintenance
    public int? ConversationId { get; set; } // if reported by guest
    
    [Required, MaxLength(100)]
    public string Title { get; set; } = string.Empty;
    
    [Required]
    public string Description { get; set; } = string.Empty;
    
    [Required, MaxLength(20)]
    public string Category { get; set; } = string.Empty; // HVAC, Elevator, Plumbing, Electrical, Pool, General
    
    [Required, MaxLength(20)]
    public string Priority { get; set; } = "Normal"; // Low, Normal, High, Urgent
    
    [Required, MaxLength(20)]
    public string Status { get; set; } = "Open"; // Open, In Progress, Completed, Cancelled
    
    [MaxLength(100)]
    public string? Location { get; set; }
    
    [MaxLength(20)]
    public string? ReportedBy { get; set; } // Guest phone or staff name
    
    public int? AssignedTo { get; set; } // Staff member ID
    
    public DateTime? DueDate { get; set; }
    public DateTime ReportedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    
    public string? ResolutionNotes { get; set; }
    public decimal? Cost { get; set; }
    
    // Navigation properties
    public virtual Tenant Tenant { get; set; } = null!;
    public virtual MaintenanceItem? MaintenanceItem { get; set; }
    public virtual Conversation? Conversation { get; set; }
    public virtual User? AssignedToUser { get; set; }
    public virtual ICollection<StaffTask> StaffTasks { get; set; } = new List<StaffTask>();
}

public class MaintenanceHistory
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int MaintenanceItemId { get; set; }
    public int? MaintenanceRequestId { get; set; }
    
    [Required, MaxLength(50)]
    public string ServiceType { get; set; } = string.Empty; // Preventive, Reactive, Emergency, Inspection
    
    [Required]
    public string Description { get; set; } = string.Empty;
    
    public DateTime ServiceDate { get; set; }
    
    [MaxLength(100)]
    public string? PerformedBy { get; set; } // Staff name or external contractor
    
    public decimal? Cost { get; set; }
    
    public string? PartsReplaced { get; set; }
    public string? Notes { get; set; }
    
    public DateTime? NextServiceDue { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual Tenant Tenant { get; set; } = null!;
    public virtual MaintenanceItem MaintenanceItem { get; set; } = null!;
    public virtual MaintenanceRequest? MaintenanceRequest { get; set; }
}