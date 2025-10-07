using System.ComponentModel.DataAnnotations;

namespace Hostr.Api.Models;

public class ConversationTransfer
{
    public int Id { get; set; }

    [Required]
    public int ConversationId { get; set; }

    [Required]
    public int TenantId { get; set; }

    public bool FromSystem { get; set; }

    public int? FromAgentId { get; set; }

    public int? ToAgentId { get; set; }

    [Required]
    [MaxLength(100)]
    public string TransferReason { get; set; } = string.Empty;

    public DateTime TransferredAt { get; set; }

    public DateTime? ReleasedAt { get; set; }

    [MaxLength(200)]
    public string? ReleaseReason { get; set; }

    [Required]
    [MaxLength(50)]
    public string Status { get; set; } = "Pending"; // Pending, Completed, Released, Failed

    [MaxLength(500)]
    public string? Notes { get; set; }

    // Navigation properties
    public Conversation Conversation { get; set; } = null!;
    public User? FromAgent { get; set; }
    public User? ToAgent { get; set; }
}