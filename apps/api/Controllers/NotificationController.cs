using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Hostr.Api.Data;
using Hostr.Api.Models;
using System.Security.Claims;

namespace Hostr.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class NotificationController : ControllerBase
{
    private readonly HostrDbContext _context;
    private readonly ILogger<NotificationController> _logger;

    public NotificationController(HostrDbContext context, ILogger<NotificationController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Get all read notifications for the current user
    /// </summary>
    [HttpGet("read")]
    public async Task<IActionResult> GetReadNotifications()
    {
        try
        {
            var userId = GetCurrentUserId();
            var tenantId = GetCurrentTenantId();

            var readNotifications = await _context.UserNotificationReads
                .Where(n => n.UserId == userId && n.TenantId == tenantId)
                .OrderByDescending(n => n.ReadAt)
                .ToListAsync();

            return Ok(readNotifications);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching read notifications");
            return StatusCode(500, new { error = "Failed to fetch read notifications" });
        }
    }

    /// <summary>
    /// Check if a notification has been read by the current user
    /// </summary>
    [HttpGet("read/{notificationId}")]
    public async Task<IActionResult> IsNotificationRead(string notificationId)
    {
        try
        {
            var userId = GetCurrentUserId();
            var tenantId = GetCurrentTenantId();

            var exists = await _context.UserNotificationReads
                .AnyAsync(n => n.NotificationId == notificationId
                    && n.UserId == userId
                    && n.TenantId == tenantId);

            return Ok(new { notificationId, isRead = exists });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking notification read state for {NotificationId}", notificationId);
            return StatusCode(500, new { error = "Failed to check notification read state" });
        }
    }

    /// <summary>
    /// Mark a notification as read
    /// </summary>
    [HttpPost("mark-read")]
    public async Task<IActionResult> MarkAsRead([FromBody] MarkAsReadRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var tenantId = GetCurrentTenantId();

            // Check if already marked as read
            var existing = await _context.UserNotificationReads
                .FirstOrDefaultAsync(n => n.NotificationId == request.NotificationId
                    && n.UserId == userId
                    && n.TenantId == tenantId);

            if (existing != null)
            {
                return Ok(new { message = "Notification already marked as read", notificationId = request.NotificationId });
            }

            // Parse notification ID to get type and entity ID
            var parts = request.NotificationId.Split('-');
            string notificationType = parts.Length > 0 ? parts[0] : "unknown";
            int entityId = parts.Length > 1 && int.TryParse(parts[1], out var id) ? id : 0;

            // Create new read record
            var readRecord = new UserNotificationRead
            {
                UserId = userId,
                TenantId = tenantId,
                NotificationId = request.NotificationId,
                NotificationType = notificationType,
                EntityId = entityId,
                ReadAt = DateTime.UtcNow
            };

            _context.UserNotificationReads.Add(readRecord);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Notification marked as read", notificationId = request.NotificationId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking notification as read: {NotificationId}", request.NotificationId);
            return StatusCode(500, new { error = "Failed to mark notification as read" });
        }
    }

    /// <summary>
    /// Mark all notifications as read for the current user
    /// </summary>
    [HttpPost("mark-all-read")]
    public async Task<IActionResult> MarkAllAsRead([FromBody] MarkAllAsReadRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var tenantId = GetCurrentTenantId();

            var newReadRecords = new List<UserNotificationRead>();

            // Get all existing read notification IDs to avoid duplicates
            var existingReadIds = await _context.UserNotificationReads
                .Where(n => n.UserId == userId && n.TenantId == tenantId)
                .Select(n => n.NotificationId)
                .ToListAsync();

            foreach (var notificationId in request.NotificationIds)
            {
                // Skip if already marked as read
                if (existingReadIds.Contains(notificationId))
                {
                    continue;
                }

                // Parse notification ID to get type and entity ID
                var parts = notificationId.Split('-');
                string notificationType = parts.Length > 0 ? parts[0] : "unknown";
                int entityId = parts.Length > 1 && int.TryParse(parts[1], out var id) ? id : 0;

                newReadRecords.Add(new UserNotificationRead
                {
                    UserId = userId,
                    TenantId = tenantId,
                    NotificationId = notificationId,
                    NotificationType = notificationType,
                    EntityId = entityId,
                    ReadAt = DateTime.UtcNow
                });
            }

            if (newReadRecords.Any())
            {
                _context.UserNotificationReads.AddRange(newReadRecords);
                await _context.SaveChangesAsync();
            }

            return Ok(new { message = $"{newReadRecords.Count} notifications marked as read" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking all notifications as read");
            return StatusCode(500, new { error = "Failed to mark all notifications as read" });
        }
    }

    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedAccessException("User ID not found in token");
        }
        return userId;
    }

    private int GetCurrentTenantId()
    {
        // Get tenant ID from HttpContext.Items (set by tenant middleware)
        var tenantId = HttpContext.Items["TenantId"] as int?;
        if (!tenantId.HasValue || tenantId.Value == 0)
        {
            throw new UnauthorizedAccessException("Tenant ID not found in token");
        }
        return tenantId.Value;
    }
}

public class MarkAsReadRequest
{
    public string NotificationId { get; set; } = string.Empty;
}

public class MarkAllAsReadRequest
{
    public List<string> NotificationIds { get; set; } = new();
}
