using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Hostr.Api.Models;

/// <summary>
/// Tracks which notifications have been read by which users
/// </summary>
public class UserNotificationRead
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// ID of the user who read the notification
    /// </summary>
    [Required]
    public int UserId { get; set; }

    /// <summary>
    /// Navigation property to User
    /// </summary>
    [ForeignKey(nameof(UserId))]
    public virtual User? User { get; set; }

    /// <summary>
    /// Type of notification (task, emergency, conversation, etc.)
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string NotificationType { get; set; } = string.Empty;

    /// <summary>
    /// ID of the entity (task ID, emergency ID, etc.)
    /// </summary>
    [Required]
    public int EntityId { get; set; }

    /// <summary>
    /// Full notification ID (e.g., "task-123")
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string NotificationId { get; set; } = string.Empty;

    /// <summary>
    /// When the notification was marked as read
    /// </summary>
    [Required]
    public DateTime ReadAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Tenant ID for multi-tenancy
    /// </summary>
    [Required]
    public int TenantId { get; set; }

    /// <summary>
    /// Navigation property to Tenant
    /// </summary>
    [ForeignKey(nameof(TenantId))]
    public virtual Tenant? Tenant { get; set; }
}
