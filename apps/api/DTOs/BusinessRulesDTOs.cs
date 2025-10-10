using System.ComponentModel.DataAnnotations;

namespace Hostr.Api.DTOs;

// Service Business Rules DTOs
public class CreateServiceRuleRequest
{
    public int ServiceId { get; set; }

    [Required, MaxLength(50)]
    public string RuleType { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string RuleKey { get; set; } = string.Empty;

    [Required, MaxLength(500)]
    public string RuleValue { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? ValidationMessage { get; set; }

    public int Priority { get; set; } = 3;

    public bool IsActive { get; set; } = true;

    public decimal? MinConfidenceScore { get; set; } = 0.8m;

    [MaxLength(500)]
    public string? Notes { get; set; }
}

public class UpdateServiceRuleRequest
{
    [Required, MaxLength(50)]
    public string RuleType { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string RuleKey { get; set; } = string.Empty;

    [Required, MaxLength(500)]
    public string RuleValue { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? ValidationMessage { get; set; }

    public int Priority { get; set; } = 3;

    public bool IsActive { get; set; } = true;

    public decimal? MinConfidenceScore { get; set; } = 0.8m;

    [MaxLength(500)]
    public string? Notes { get; set; }
}

// Request Item Rules DTOs
public class CreateRequestItemRuleRequest
{
    public int RequestItemId { get; set; }

    [Required, MaxLength(50)]
    public string RuleType { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string RuleKey { get; set; } = string.Empty;

    [Required, MaxLength(500)]
    public string RuleValue { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? ValidationMessage { get; set; }

    public int? MaxPerRoom { get; set; }

    public int? MaxPerGuest { get; set; }

    public bool RequiresActiveBooking { get; set; } = false;

    [MaxLength(100)]
    public string? RestrictedHours { get; set; }

    public int Priority { get; set; } = 3;

    public bool IsActive { get; set; } = true;

    public decimal? MinConfidenceScore { get; set; } = 0.8m;

    [MaxLength(500)]
    public string? Notes { get; set; }
}

public class UpdateRequestItemRuleRequest
{
    [Required, MaxLength(50)]
    public string RuleType { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string RuleKey { get; set; } = string.Empty;

    [Required, MaxLength(500)]
    public string RuleValue { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? ValidationMessage { get; set; }

    public int? MaxPerRoom { get; set; }

    public int? MaxPerGuest { get; set; }

    public bool RequiresActiveBooking { get; set; } = false;

    [MaxLength(100)]
    public string? RestrictedHours { get; set; }

    public int Priority { get; set; } = 3;

    public bool IsActive { get; set; } = true;

    public decimal? MinConfidenceScore { get; set; } = 0.8m;

    [MaxLength(500)]
    public string? Notes { get; set; }
}

// Upsell Items DTOs
public class CreateUpsellItemRequest
{
    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public int PriceCents { get; set; }

    [Required, MaxLength(50)]
    public string Unit { get; set; } = "item";

    public List<string> Categories { get; set; } = new();

    [MaxLength(500)]
    public string? ImageUrl { get; set; }

    public bool IsActive { get; set; } = true;

    public int Priority { get; set; } = 3;

    public int? LeadTimeMinutes { get; set; }

    [MaxLength(1000)]
    public string? RelevanceContext { get; set; }

    public decimal? MinConfidenceScore { get; set; } = 0.7m;
}

public class UpdateUpsellItemRequest
{
    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public int PriceCents { get; set; }

    [Required, MaxLength(50)]
    public string Unit { get; set; } = "item";

    public List<string> Categories { get; set; } = new();

    [MaxLength(500)]
    public string? ImageUrl { get; set; }

    public bool IsActive { get; set; } = true;

    public int Priority { get; set; } = 3;

    public int? LeadTimeMinutes { get; set; }

    [MaxLength(1000)]
    public string? RelevanceContext { get; set; }

    public decimal? MinConfidenceScore { get; set; } = 0.7m;
}

// Response DTOs
public class ServiceWithRulesDTO
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public int RuleCount { get; set; }
    public int ActiveRuleCount { get; set; }
    public DateTime? LastModified { get; set; }
    public string? LastModifiedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class RequestItemWithRulesDTO
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public int? StockQuantity { get; set; }
    public bool IsActive { get; set; }
    public int RuleCount { get; set; }
    public int ActiveRuleCount { get; set; }
    public DateTime? LastModified { get; set; }
    public string? LastModifiedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

// Stats DTOs
public class BusinessRulesStatsDTO
{
    public int TotalRules { get; set; }
    public int ActiveRules { get; set; }
    public int DraftRules { get; set; }
    public int InactiveRules { get; set; }
    public int TotalServices { get; set; }
    public int ServicesWithRules { get; set; }
    public int TotalRequestItems { get; set; }
    public int RequestItemsWithRules { get; set; }
    public int TotalUpsellItems { get; set; }
    public int ActiveUpsellItems { get; set; }
}

// Audit Log DTOs
public class AuditLogEntryDTO
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public int EntityId { get; set; }
    public string? EntityName { get; set; }
    public string? ChangesBefore { get; set; }
    public string? ChangesAfter { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime Timestamp { get; set; }
}

// Upsell Analytics DTOs
public class UpsellAnalyticsDTO
{
    public int TotalRevenue { get; set; }
    public decimal ConversionRate { get; set; }
    public int TotalSuggestions { get; set; }
    public int TotalAccepted { get; set; }
    public List<UpsellPerformerDTO> TopPerformers { get; set; } = new();
}

public class UpsellPerformerDTO
{
    public int UpsellItemId { get; set; }
    public string UpsellItemTitle { get; set; } = string.Empty;
    public int Suggestions { get; set; }
    public int Acceptances { get; set; }
    public decimal ConversionRate { get; set; }
    public int Revenue { get; set; }
}
