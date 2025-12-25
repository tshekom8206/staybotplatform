using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace Hostr.Api.Models;

public class Tenant
{
    public int Id { get; set; }
    
    [Required, MaxLength(100)]
    public string Slug { get; set; } = string.Empty;
    
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;
    
    [MaxLength(50)]
    public string Timezone { get; set; } = "Africa/Johannesburg";
    
    [Required, MaxLength(20)]
    public string Plan { get; set; } = "Basic"; // Basic|Standard|Premium
    
    [MaxLength(7)]
    public string ThemePrimary { get; set; } = "#007bff";
    
    [Required, MaxLength(20)]
    public string Status { get; set; } = "Active"; // Active|Suspended|Cancelled
    
    public int RetentionDays { get; set; } = 30;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual ICollection<UserTenant> UserTenants { get; set; } = new List<UserTenant>();
    public virtual ICollection<WhatsAppNumber> WhatsAppNumbers { get; set; } = new List<WhatsAppNumber>();
    public virtual ICollection<Conversation> Conversations { get; set; } = new List<Conversation>();
    public virtual ICollection<Message> Messages { get; set; } = new List<Message>();
    public virtual ICollection<KnowledgeBaseChunk> KnowledgeBaseChunks { get; set; } = new List<KnowledgeBaseChunk>();
    public virtual ICollection<UpsellItem> UpsellItems { get; set; } = new List<UpsellItem>();
    public virtual ICollection<GuideItem> GuideItems { get; set; } = new List<GuideItem>();
    public virtual ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    public virtual ICollection<Rating> Ratings { get; set; } = new List<Rating>();
    public virtual ICollection<RequestItem> RequestItems { get; set; } = new List<RequestItem>();
    public virtual ICollection<StaffTask> StaffTasks { get; set; } = new List<StaffTask>();
    public virtual ICollection<WhatsAppTemplate> WhatsAppTemplates { get; set; } = new List<WhatsAppTemplate>();
    public virtual ICollection<QuickReply> QuickReplies { get; set; } = new List<QuickReply>();
    public virtual ICollection<UsageDaily> UsageDaily { get; set; } = new List<UsageDaily>();
    public virtual ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
}

public class User : IdentityUser<int>
{
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual ICollection<UserTenant> UserTenants { get; set; } = new List<UserTenant>();
}

public class UserTenant
{
    public int UserId { get; set; }
    public int TenantId { get; set; }
    
    [Required, MaxLength(20)]
    public string Role { get; set; } = string.Empty; // Owner|Manager|Agent|SuperAdmin
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual User User { get; set; } = null!;
    public virtual Tenant Tenant { get; set; } = null!;
}

public class WhatsAppNumber
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    
    [Required, MaxLength(100)]
    public string WabaId { get; set; } = string.Empty;
    
    [Required, MaxLength(100)]
    public string PhoneNumberId { get; set; } = string.Empty;
    
    [Required, MaxLength(500)]
    public string PageAccessToken { get; set; } = string.Empty;
    
    [Required, MaxLength(20)]
    public string Status { get; set; } = "Active"; // Active|Inactive
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual Tenant Tenant { get; set; } = null!;
}