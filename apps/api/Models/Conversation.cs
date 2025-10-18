using System.ComponentModel.DataAnnotations;
using Pgvector;

namespace Hostr.Api.Models;

public class Conversation
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    
    [Required, MaxLength(20)]
    public string WaUserPhone { get; set; } = string.Empty;
    
    [Required, MaxLength(20)]
    public string Status { get; set; } = "Active"; // Active|Handover|Closed|TransferredToAgent

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastBotReplyAt { get; set; }

    // Agent transfer properties
    public int? AssignedAgentId { get; set; }
    public string? TransferReason { get; set; }
    public DateTime? TransferredAt { get; set; }
    public DateTime? TransferCompletedAt { get; set; }
    public string? TransferSummary { get; set; }

    // Booking information gathering state
    [MaxLength(50)]
    public string ConversationMode { get; set; } = "Normal"; // Normal|GatheringBookingInfo
    public string? BookingInfoState { get; set; } // JSON serialized BookingInformationState
    public string? LastBotAction { get; set; } // What the bot asked or did last
    public string? StateVariables { get; set; } // JSON serialized Dictionary<string, string> for conversation state

    // Navigation properties
    public virtual Tenant Tenant { get; set; } = null!;
    public virtual User? AssignedAgent { get; set; }
    public virtual ICollection<Message> Messages { get; set; } = new List<Message>();
    public virtual ICollection<StaffTask> StaffTasks { get; set; } = new List<StaffTask>();
    public virtual ICollection<Rating> Ratings { get; set; } = new List<Rating>();
}

public class Message
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int ConversationId { get; set; }
    
    [Required, MaxLength(10)]
    public string Direction { get; set; } = string.Empty; // Inbound|Outbound

    [Required, MaxLength(20)]
    public string MessageType { get; set; } = "text"; // text|image|document|template

    [Required]
    public string Body { get; set; } = string.Empty;

    // For transfer detection and conversation analysis
    public bool IsFromGuest => Direction == "Inbound";
    public string MessageText => Body;
    
    [MaxLength(50)]
    public string? Model { get; set; }
    
    public bool UsedRag { get; set; } = false;
    
    public int? TokensPrompt { get; set; }
    public int? TokensCompletion { get; set; }

    [MaxLength(50)]
    public string? IntentClassification { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual Tenant Tenant { get; set; } = null!;
    public virtual Conversation Conversation { get; set; } = null!;
}

public class FAQ
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    
    [Required]
    public string Question { get; set; } = string.Empty;
    
    [Required]
    public string Answer { get; set; } = string.Empty;
    
    [MaxLength(5)]
    public string Language { get; set; } = "en";
    
    public string[] Tags { get; set; } = Array.Empty<string>();
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual Tenant Tenant { get; set; } = null!;
}

public class KnowledgeBaseChunk
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    
    [Required, MaxLength(200)]
    public string Source { get; set; } = string.Empty;
    
    [MaxLength(5)]
    public string Language { get; set; } = "en";
    
    [Required]
    public string Content { get; set; } = string.Empty;
    
    public Vector Embedding { get; set; } = new(Array.Empty<float>());
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual Tenant Tenant { get; set; } = null!;
}