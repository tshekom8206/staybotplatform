using System.ComponentModel.DataAnnotations;

namespace Hostr.Api.Models;

public class MenuCategory
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    
    [MaxLength(500)]
    public string? Description { get; set; }
    
    [Required, MaxLength(20)]
    public string MealType { get; set; } = "all"; // breakfast, lunch, dinner, all
    
    public int DisplayOrder { get; set; } = 0;
    public bool IsActive { get; set; } = true;
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual Tenant Tenant { get; set; } = null!;
    public virtual ICollection<MenuItem> MenuItems { get; set; } = new List<MenuItem>();
}

public class MenuItem
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int MenuCategoryId { get; set; }
    
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;
    
    [Required]
    public string Description { get; set; } = string.Empty;
    
    public int PriceCents { get; set; }
    
    [MaxLength(20)]
    public string Currency { get; set; } = "ZAR";
    
    [MaxLength(100)]
    public string? Allergens { get; set; }
    
    [Required, MaxLength(20)]
    public string MealType { get; set; } = "all"; // breakfast, lunch, dinner, all
    
    public bool IsVegetarian { get; set; } = false;
    public bool IsVegan { get; set; } = false;
    public bool IsGlutenFree { get; set; } = false;
    public bool IsSpicy { get; set; } = false;
    
    public bool IsAvailable { get; set; } = true;
    public bool IsSpecial { get; set; } = false;
    
    public string[] Tags { get; set; } = Array.Empty<string>();
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual Tenant Tenant { get; set; } = null!;
    public virtual MenuCategory MenuCategory { get; set; } = null!;
}

public class MenuSpecial
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int? MenuItemId { get; set; }
    
    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;
    
    [Required]
    public string Description { get; set; } = string.Empty;
    
    public int? SpecialPriceCents { get; set; }
    
    [Required, MaxLength(20)]
    public string SpecialType { get; set; } = "daily"; // daily, weekly, seasonal, limited
    
    // Day of week (0=Sunday, 1=Monday, etc.) - null means all days
    public int? DayOfWeek { get; set; }
    
    public DateOnly? ValidFrom { get; set; }
    public DateOnly? ValidTo { get; set; }
    
    [Required, MaxLength(20)]
    public string MealType { get; set; } = "all"; // breakfast, lunch, dinner, all
    
    public bool IsActive { get; set; } = true;
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual Tenant Tenant { get; set; } = null!;
    public virtual MenuItem? MenuItem { get; set; }
}

public class BusinessInfo
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    
    [Required, MaxLength(100)]
    public string Category { get; set; } = string.Empty; // hours, location, amenities, policies, contact
    
    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;
    
    [Required]
    public string Content { get; set; } = string.Empty;
    
    public string[] Tags { get; set; } = Array.Empty<string>();
    
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; } = 0;
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual Tenant Tenant { get; set; } = null!;
}

public class InformationItem
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    
    [Required, MaxLength(200)]
    public string Question { get; set; } = string.Empty;
    
    [Required]
    public string Answer { get; set; } = string.Empty;
    
    [Required, MaxLength(100)]
    public string Category { get; set; } = "general"; // general, menu, services, amenities, local
    
    public string[] Keywords { get; set; } = Array.Empty<string>();
    
    public bool IsTimeRelevant { get; set; } = false;
    public int? RelevantHourStart { get; set; } // 0-23
    public int? RelevantHourEnd { get; set; } // 0-23
    
    public bool IsActive { get; set; } = true;
    public int Priority { get; set; } = 0; // Higher number = higher priority

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual Tenant Tenant { get; set; } = null!;
}

public class WelcomeMessage
{
    public int Id { get; set; }
    public int TenantId { get; set; }

    [Required, MaxLength(50)]
    public string MessageType { get; set; } = "greeting"; // greeting, welcome, assistance

    [Required]
    public string Template { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; } = 0;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual Tenant Tenant { get; set; } = null!;
}