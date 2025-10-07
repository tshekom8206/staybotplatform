using System.ComponentModel.DataAnnotations;

namespace Hostr.Api.Models;

public class BroadcastMessage
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    
    [Required, MaxLength(50)]
    public string MessageType { get; set; } = string.Empty; // emergency, power_outage, water_outage, internet_down, custom
    
    [Required]
    public string Content { get; set; } = string.Empty;
    
    [MaxLength(100)]
    public string? EstimatedRestorationTime { get; set; }
    
    public int TotalRecipients { get; set; } = 0;
    public int SuccessfulDeliveries { get; set; } = 0;
    public int FailedDeliveries { get; set; } = 0;
    
    [Required, MaxLength(50)]
    public string CreatedBy { get; set; } = "System"; // Could be user ID or system identifier
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    
    [MaxLength(20)]
    public string Status { get; set; } = "Pending"; // Pending, InProgress, Completed, Failed
    
    // Navigation properties
    public virtual Tenant Tenant { get; set; } = null!;
    public virtual ICollection<BroadcastRecipient> Recipients { get; set; } = new List<BroadcastRecipient>();
}

public class BroadcastRecipient
{
    public int Id { get; set; }
    public int BroadcastMessageId { get; set; }
    public int ConversationId { get; set; }
    
    [Required, MaxLength(20)]
    public string PhoneNumber { get; set; } = string.Empty;
    
    [MaxLength(20)]
    public string DeliveryStatus { get; set; } = "Pending"; // Pending, Sent, Delivered, Failed
    
    public string? ErrorMessage { get; set; }
    
    public DateTime? SentAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    
    public int RetryCount { get; set; } = 0;
    public int MaxRetries { get; set; } = 3;
    
    // Navigation properties
    public virtual BroadcastMessage BroadcastMessage { get; set; } = null!;
    public virtual Conversation Conversation { get; set; } = null!;
}

public enum BroadcastMessageType
{
    Emergency,
    PowerOutage,
    WaterOutage,
    InternetDown,
    Custom
}

public enum BroadcastStatus
{
    Pending,
    InProgress,
    Completed,
    Failed
}

public enum DeliveryStatus
{
    Pending,
    Sent,
    Delivered,
    Failed
}

public enum BroadcastScope
{
    ActiveOnly,     // Only active conversations 
    RecentGuests,   // Active + conversations from last 7 days
    AllGuests       // All conversations for tenant
}