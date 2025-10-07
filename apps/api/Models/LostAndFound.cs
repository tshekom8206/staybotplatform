using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace Hostr.Api.Models;

public class LostItem
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    
    [Required, MaxLength(100)]
    public string ItemName { get; set; } = string.Empty;
    
    [Required, MaxLength(50)]
    public string Category { get; set; } = string.Empty; // Electronics, Clothing, Jewelry, Documents, Keys, Other
    
    public string? Description { get; set; }
    
    [MaxLength(20)]
    public string? Color { get; set; }
    
    [MaxLength(50)]
    public string? Brand { get; set; }
    
    [MaxLength(100)]
    public string? LocationLost { get; set; } // Where the guest thinks they lost it
    
    [MaxLength(20)]
    public string ReporterPhone { get; set; } = string.Empty;
    
    [MaxLength(100)]
    public string? ReporterName { get; set; }
    
    public int? ConversationId { get; set; } // If reported via WhatsApp
    
    [Required, MaxLength(20)]
    public string Status { get; set; } = "Open"; // Open, Matched, Claimed, Closed
    
    public DateTime ReportedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ClaimedAt { get; set; }
    
    // Premium features
    public decimal? RewardAmount { get; set; }
    public string? SpecialInstructions { get; set; }
    public JsonDocument? AdditionalDetails { get; set; } // Flexible storage for extra info
    
    // Navigation properties
    public virtual Tenant Tenant { get; set; } = null!;
    public virtual Conversation? Conversation { get; set; }
    public virtual ICollection<LostAndFoundMatch> LostItemMatches { get; set; } = new List<LostAndFoundMatch>();
}

public class FoundItem
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    
    [Required, MaxLength(100)]
    public string ItemName { get; set; } = string.Empty;
    
    [Required, MaxLength(50)]
    public string Category { get; set; } = string.Empty;
    
    public string? Description { get; set; }
    
    [MaxLength(20)]
    public string? Color { get; set; }
    
    [MaxLength(50)]
    public string? Brand { get; set; }
    
    [Required, MaxLength(100)]
    public string LocationFound { get; set; } = string.Empty; // Where staff found it
    
    [MaxLength(100)]
    public string? FinderName { get; set; } // Staff member or guest who found it
    
    [Required, MaxLength(20)]
    public string Status { get; set; } = "AVAILABLE"; // AVAILABLE, MATCHED, CLAIMED, DISPOSED
    
    public DateTime FoundAt { get; set; } = DateTime.UtcNow;
    public DateTime? ClaimedAt { get; set; }
    
    // Storage information
    [MaxLength(50)]
    public string? StorageLocation { get; set; } // Where the item is stored
    
    [MaxLength(100)]
    public string? StorageNotes { get; set; }
    
    // Disposal tracking
    public DateTime? DisposalDate { get; set; }
    public int DisposalAfterDays { get; set; } = 90; // Default 90 days before disposal
    
    public JsonDocument? AdditionalDetails { get; set; }
    
    // Navigation properties
    public virtual Tenant Tenant { get; set; } = null!;
    public virtual ICollection<LostAndFoundMatch> FoundItemMatches { get; set; } = new List<LostAndFoundMatch>();
}

public class LostAndFoundMatch
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int LostItemId { get; set; }
    public int FoundItemId { get; set; }
    
    public decimal MatchScore { get; set; } // 0.0 to 1.0, confidence level of the match
    
    [Required, MaxLength(20)]
    public string Status { get; set; } = "PENDING"; // PENDING, CONFIRMED, REJECTED, CLAIMED
    
    public string? MatchingReason { get; set; } // Why this was considered a match
    
    // Staff verification
    public int? VerifiedBy { get; set; } // Staff user ID who verified the match
    public DateTime? VerifiedAt { get; set; }
    
    // Guest confirmation
    public bool GuestConfirmed { get; set; } = false;
    public DateTime? GuestConfirmedAt { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ClaimedAt { get; set; }
    
    public string? Notes { get; set; }
    
    // Navigation properties
    public virtual Tenant Tenant { get; set; } = null!;
    public virtual LostItem LostItem { get; set; } = null!;
    public virtual FoundItem FoundItem { get; set; } = null!;
    public virtual User? VerifiedByUser { get; set; }
}

public class LostAndFoundCategory
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    
    [Required, MaxLength(50)]
    public string Name { get; set; } = string.Empty;
    
    public string? Description { get; set; }
    
    // Keywords for automatic categorization
    public string[] Keywords { get; set; } = Array.Empty<string>();
    
    // Default disposal period for this category
    public int DefaultDisposalDays { get; set; } = 90;
    
    // Whether items in this category require special handling
    public bool RequiresSecureStorage { get; set; } = false;
    
    public bool IsActive { get; set; } = true;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual Tenant Tenant { get; set; } = null!;
}

public class LostAndFoundNotification
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    
    [Required, MaxLength(20)]
    public string NotificationType { get; set; } = string.Empty; // MATCH_FOUND, READY_FOR_PICKUP, DISPOSAL_WARNING
    
    public int? LostItemId { get; set; }
    public int? FoundItemId { get; set; }
    public int? MatchId { get; set; }
    
    [Required, MaxLength(20)]
    public string RecipientPhone { get; set; } = string.Empty;
    
    [Required]
    public string Message { get; set; } = string.Empty;
    
    [Required, MaxLength(20)]
    public string Status { get; set; } = "PENDING"; // PENDING, SENT, DELIVERED, FAILED
    
    public DateTime ScheduledAt { get; set; }
    public DateTime? SentAt { get; set; }
    
    public int RetryCount { get; set; } = 0;
    public int MaxRetries { get; set; } = 3;
    
    public string? ErrorMessage { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual Tenant Tenant { get; set; } = null!;
    public virtual LostItem? LostItem { get; set; }
    public virtual FoundItem? FoundItem { get; set; }
    public virtual LostAndFoundMatch? Match { get; set; }
}