using System.ComponentModel.DataAnnotations;

namespace Hostr.Api.Models;

public class ServiceCategory
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    
    [MaxLength(500)]
    public string? Description { get; set; }
    
    [MaxLength(100)]
    public string LlmVisibleName { get; set; } = string.Empty;
    
    public bool IsActive { get; set; } = true;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual Tenant Tenant { get; set; } = null!;
    public virtual ICollection<ConciergeService> ConciergeServices { get; set; } = new List<ConciergeService>();
    public virtual ICollection<LocalProvider> LocalProviders { get; set; } = new List<LocalProvider>();
}

public class ConciergeService
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int ServiceCategoryId { get; set; }
    
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;
    
    [MaxLength(1000)]
    public string? Description { get; set; }
    
    [MaxLength(100)]
    public string LlmVisibleName { get; set; } = string.Empty;
    
    public bool IsActive { get; set; } = true;
    
    [MaxLength(500)]
    public string? ResponseTemplate { get; set; }
    
    public bool RequiresAdvanceNotice { get; set; } = false;
    
    [MaxLength(200)]
    public string? AdvanceNoticeText { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual Tenant Tenant { get; set; } = null!;
    public virtual ServiceCategory ServiceCategory { get; set; } = null!;
    public virtual ICollection<LocalProvider> LocalProviders { get; set; } = new List<LocalProvider>();
}

public class LocalProvider
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int ServiceCategoryId { get; set; }
    public int? ConciergeServiceId { get; set; }
    
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;
    
    [MaxLength(500)]
    public string? Description { get; set; }
    
    [MaxLength(20)]
    public string? PhoneNumber { get; set; }
    
    [MaxLength(200)]
    public string? Email { get; set; }
    
    [MaxLength(500)]
    public string? Address { get; set; }
    
    [MaxLength(200)]
    public string? Website { get; set; }
    
    public bool IsRecommended { get; set; } = false;
    
    public bool IsActive { get; set; } = true;
    
    public int DisplayOrder { get; set; } = 0;
    
    [MaxLength(1000)]
    public string? Notes { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual Tenant Tenant { get; set; } = null!;
    public virtual ServiceCategory ServiceCategory { get; set; } = null!;
    public virtual ConciergeService? ConciergeService { get; set; }
}