using System.ComponentModel.DataAnnotations;

namespace Hostr.Api.Models;

public class WhatsAppTemplate
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    
    [Required, MaxLength(50)]
    public string Category { get; set; } = string.Empty;
    
    [Required, MaxLength(10)]
    public string Language { get; set; } = "en";
    
    [Required]
    public string Body { get; set; } = string.Empty;
    
    [Required, MaxLength(20)]
    public string Status { get; set; } = "PENDING"; // PENDING|APPROVED|REJECTED
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual Tenant Tenant { get; set; } = null!;
}

public class QuickReply
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    
    [Required, MaxLength(100)]
    public string Title { get; set; } = string.Empty;
    
    [Required]
    public string Body { get; set; } = string.Empty;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual Tenant Tenant { get; set; } = null!;
}

public class UsageDaily
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    
    public DateOnly Date { get; set; }
    
    public int MessagesIn { get; set; } = 0;
    public int MessagesOut { get; set; } = 0;
    public int TokensIn { get; set; } = 0;
    public int TokensOut { get; set; } = 0;
    public int UpsellRevenueCents { get; set; } = 0;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual Tenant Tenant { get; set; } = null!;
}

public class AuditLog
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int? ActorUserId { get; set; }
    
    [Required, MaxLength(50)]
    public string Action { get; set; } = string.Empty;
    
    [Required, MaxLength(50)]
    public string Entity { get; set; } = string.Empty;
    
    public int? EntityId { get; set; }
    
    public string? DiffJson { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual Tenant Tenant { get; set; } = null!;
    public virtual User? ActorUser { get; set; }
}