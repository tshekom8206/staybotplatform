using System.ComponentModel.DataAnnotations;

namespace Hostr.Api.Models;

public class Agent
{
    public int Id { get; set; }
    public int TenantId { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Email { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string Department { get; set; } = string.Empty;

    public string[] Skills { get; set; } = Array.Empty<string>();

    [Required, MaxLength(20)]
    public string State { get; set; } = "Offline"; // Available, Busy, Away, DoNotDisturb, Offline

    public int MaxConcurrentChats { get; set; } = 5;

    public string? StatusMessage { get; set; }

    public DateTime LastActivity { get; set; } = DateTime.UtcNow;

    public DateTime? SessionStarted { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public virtual Tenant Tenant { get; set; } = null!;
    public virtual ICollection<AgentSession> Sessions { get; set; } = new List<AgentSession>();
    // REMOVED: AssignedConversations - Using User.AssignedConversations instead
}

public class AgentSession
{
    public int Id { get; set; }
    public int AgentId { get; set; }
    public int TenantId { get; set; }

    public DateTime SessionStarted { get; set; } = DateTime.UtcNow;
    public DateTime? SessionEnded { get; set; }

    public DateTime LastHeartbeat { get; set; } = DateTime.UtcNow;

    [MaxLength(20)]
    public string State { get; set; } = "Available";

    public string? StatusMessage { get; set; }

    public int ActiveConversations { get; set; } = 0;

    // Navigation properties
    public virtual Agent Agent { get; set; } = null!;
    public virtual Tenant Tenant { get; set; } = null!;
}

