using System.ComponentModel.DataAnnotations;

namespace Hostr.Api.Models;

public class TaskNotification
{
    public int TaskId { get; set; }
    public int TenantId { get; set; }
    public string TaskType { get; set; } = string.Empty; // deliver_item|collect_item|maintenance
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Priority { get; set; } = "Normal"; // Low|Normal|High|Urgent
    public string Status { get; set; } = "Open"; // Open|InProgress|Completed|Cancelled
    public string? RoomNumber { get; set; }
    public string? Location { get; set; }
    public int Quantity { get; set; } = 1;
    public string? Notes { get; set; }
    public string? RequestItemName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty; // Guest phone or staff name
}

public class EmergencyNotification
{
    public int IncidentId { get; set; }
    public int TenantId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string EmergencyType { get; set; } = string.Empty; // Medical|Fire|Security
    public string SeverityLevel { get; set; } = "Medium"; // Low|Medium|High|Critical
    public string Status { get; set; } = "Open"; // Open|InProgress|Resolved|Cancelled
    public string? Location { get; set; }
    public string? ReportedBy { get; set; }
    public bool RequiresImmediateAction { get; set; } = false;
    public bool RequiresEvacuation { get; set; } = false;
    public DateTime ReportedAt { get; set; } = DateTime.UtcNow;
}

public class MaintenanceNotification
{
    public int RequestId { get; set; }
    public int TenantId { get; set; }
    public int? MaintenanceItemId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty; // HVAC|Plumbing|Electrical|General
    public string Priority { get; set; } = "Normal"; // Low|Normal|High|Urgent
    public string Status { get; set; } = "Open"; // Open|In Progress|Completed|Cancelled
    public string? Location { get; set; }
    public string? ReportedBy { get; set; }
    public string? MaintenanceItemName { get; set; }
    public DateTime ReportedAt { get; set; } = DateTime.UtcNow;
}

public class NotificationPayload
{
    public string Type { get; set; } = string.Empty; // task|emergency|maintenance
    public string Priority { get; set; } = "Normal";
    public object Data { get; set; } = new();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Message { get; set; } = string.Empty;
}