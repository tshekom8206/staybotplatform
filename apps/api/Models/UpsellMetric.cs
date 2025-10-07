using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Hostr.Api.Models;

public class UpsellMetric
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int TenantId { get; set; }

    [Required]
    public int ConversationId { get; set; }

    [Required]
    public int SuggestedServiceId { get; set; }

    [Required, MaxLength(200)]
    public string SuggestedServiceName { get; set; } = string.Empty;

    [Required]
    [Column(TypeName = "decimal(10,2)")]
    public decimal SuggestedServicePrice { get; set; }

    [MaxLength(50)]
    public string? SuggestedServiceCategory { get; set; }

    [MaxLength(100)]
    public string? TriggerContext { get; set; }

    public int? TriggerServiceId { get; set; }

    public bool WasSuggested { get; set; } = true;

    public bool WasAccepted { get; set; } = false;

    [MaxLength(50)]
    public string? AcceptedVia { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal Revenue { get; set; } = 0;

    public DateTime SuggestedAt { get; set; } = DateTime.UtcNow;

    public DateTime? AcceptedAt { get; set; }

    // Navigation properties
    public virtual Tenant Tenant { get; set; } = null!;
    public virtual Conversation Conversation { get; set; } = null!;
    public virtual Service SuggestedService { get; set; } = null!;
}
