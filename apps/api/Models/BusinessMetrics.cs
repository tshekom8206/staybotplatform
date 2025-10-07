using System.ComponentModel.DataAnnotations;

namespace Hostr.Api.Models;

public class GuestBusinessMetrics
{
    [Key]
    public int Id { get; set; }

    public int TenantId { get; set; }

    [Required, MaxLength(20)]
    public string PhoneNumber { get; set; } = string.Empty;

    public DateTime? FirstStayDate { get; set; }
    public DateTime? LastStayDate { get; set; }
    public int TotalStays { get; set; } = 0;
    public decimal LifetimeValue { get; set; } = 0;
    public decimal? AverageSatisfaction { get; set; }
    public int DaysSinceLastStay { get; set; } = 0;
    public bool HasReferred { get; set; } = false;
    public bool? WillReturn { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual Tenant Tenant { get; set; } = null!;
}

public class SatisfactionCorrelationReport
{
    public string SatisfactionLevel { get; set; } = string.Empty;
    public int GuestCount { get; set; }
    public decimal AvgLifetimeValue { get; set; }
    public decimal AvgStays { get; set; }
    public double ReturnRate { get; set; }
}

public class BusinessImpactMetrics
{
    public decimal RevenueFromSatisfiedGuests { get; set; }
    public decimal RevenueFromUnsatisfiedGuests { get; set; }
    public double SatisfiedGuestReturnRate { get; set; }
    public double UnsatisfiedGuestReturnRate { get; set; }
    public decimal CostOfServiceRecovery { get; set; }
    public int ExternalReviewsPosted { get; set; }
    public decimal RevenuePerSatisfactionPoint { get; set; }
    public decimal EstimatedRevenueLoss { get; set; }
    public int VipAlertsCount { get; set; }
}