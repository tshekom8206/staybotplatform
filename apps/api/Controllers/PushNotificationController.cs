using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Hostr.Api.Data;
using Hostr.Api.Models;
using System.Security.Claims;

namespace Hostr.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/push-notifications")]
    public class PushNotificationController : ControllerBase
    {
        private readonly HostrDbContext _context;
        private readonly ILogger<PushNotificationController> _logger;

        public PushNotificationController(
            HostrDbContext context,
            ILogger<PushNotificationController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Subscribe to push notifications
        /// </summary>
        [HttpPost("subscribe")]
        public async Task<ActionResult> Subscribe([FromBody] PushSubscriptionRequest request)
        {
            var tenantId = HttpContext.Items["TenantId"] as int? ?? 0;
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (tenantId == 0)
            {
                return BadRequest(new { error = "Tenant ID not found" });
            }

            try
            {
                // Check if subscription already exists
                var existingSubscription = await _context.Set<PushSubscription>()
                    .FirstOrDefaultAsync(s =>
                        s.Endpoint == request.Endpoint &&
                        s.TenantId == tenantId);

                if (existingSubscription != null)
                {
                    // Update existing subscription
                    existingSubscription.P256dhKey = request.Keys.P256dh;
                    existingSubscription.AuthKey = request.Keys.Auth;
                    existingSubscription.DeviceInfo = request.DeviceInfo;
                    existingSubscription.LastUsedAt = DateTime.UtcNow;
                    existingSubscription.IsActive = true;

                    if (!string.IsNullOrEmpty(userIdClaim) && int.TryParse(userIdClaim, out var userId))
                    {
                        existingSubscription.UserId = userId;
                    }

                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Updated push subscription for tenant {TenantId}", tenantId);

                    return Ok(new { message = "Subscription updated successfully" });
                }

                // Create new subscription
                var subscription = new PushSubscription
                {
                    TenantId = tenantId,
                    Endpoint = request.Endpoint,
                    P256dhKey = request.Keys.P256dh,
                    AuthKey = request.Keys.Auth,
                    DeviceInfo = request.DeviceInfo,
                    CreatedAt = DateTime.UtcNow,
                    LastUsedAt = DateTime.UtcNow,
                    IsActive = true
                };

                if (!string.IsNullOrEmpty(userIdClaim) && int.TryParse(userIdClaim, out var newUserId))
                {
                    subscription.UserId = newUserId;
                }

                _context.Set<PushSubscription>().Add(subscription);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Created push subscription for tenant {TenantId}", tenantId);

                return Ok(new { message = "Subscribed to push notifications successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error subscribing to push notifications");
                return StatusCode(500, new { error = "Failed to subscribe to push notifications" });
            }
        }

        /// <summary>
        /// Unsubscribe from push notifications
        /// </summary>
        [HttpPost("unsubscribe")]
        public async Task<ActionResult> Unsubscribe([FromBody] UnsubscribeRequest request)
        {
            var tenantId = HttpContext.Items["TenantId"] as int? ?? 0;

            if (tenantId == 0)
            {
                return BadRequest(new { error = "Tenant ID not found" });
            }

            try
            {
                var subscription = await _context.Set<PushSubscription>()
                    .FirstOrDefaultAsync(s =>
                        s.Endpoint == request.Endpoint &&
                        s.TenantId == tenantId);

                if (subscription == null)
                {
                    return NotFound(new { error = "Subscription not found" });
                }

                subscription.IsActive = false;
                await _context.SaveChangesAsync();

                _logger.LogInformation("Unsubscribed from push notifications for tenant {TenantId}", tenantId);

                return Ok(new { message = "Unsubscribed successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unsubscribing from push notifications");
                return StatusCode(500, new { error = "Failed to unsubscribe" });
            }
        }

        /// <summary>
        /// Get all active subscriptions for the tenant
        /// </summary>
        [HttpGet("subscriptions")]
        public async Task<ActionResult<IEnumerable<object>>> GetSubscriptions()
        {
            var tenantId = HttpContext.Items["TenantId"] as int? ?? 0;

            if (tenantId == 0)
            {
                return BadRequest(new { error = "Tenant ID not found" });
            }

            try
            {
                var subscriptions = await _context.Set<PushSubscription>()
                    .Where(s => s.TenantId == tenantId && s.IsActive)
                    .Select(s => new
                    {
                        s.Id,
                        s.Endpoint,
                        s.UserId,
                        s.DeviceInfo,
                        s.CreatedAt,
                        s.LastUsedAt
                    })
                    .ToListAsync();

                return Ok(subscriptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving subscriptions");
                return StatusCode(500, new { error = "Failed to retrieve subscriptions" });
            }
        }

        /// <summary>
        /// Send push notification (for testing/admin use)
        /// </summary>
        [HttpPost("send")]
        public async Task<ActionResult> SendNotification([FromBody] SendPushNotificationRequest request)
        {
            var tenantId = HttpContext.Items["TenantId"] as int? ?? 0;

            if (tenantId == 0)
            {
                return BadRequest(new { error = "Tenant ID not found" });
            }

            try
            {
                // Get subscriptions to send to
                var query = _context.Set<PushSubscription>()
                    .Where(s => s.TenantId == tenantId && s.IsActive);

                if (request.UserId.HasValue)
                {
                    query = query.Where(s => s.UserId == request.UserId.Value);
                }

                var subscriptions = await query.ToListAsync();

                if (!subscriptions.Any())
                {
                    return NotFound(new { error = "No active subscriptions found" });
                }

                _logger.LogInformation(
                    "Would send push notification to {Count} subscriptions for tenant {TenantId}",
                    subscriptions.Count,
                    tenantId);

                // Note: Actual push notification sending would require WebPush library
                // For now, just return success
                // TODO: Implement actual push notification sending using WebPush library

                return Ok(new
                {
                    message = "Push notification queued for delivery",
                    recipientCount = subscriptions.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending push notification");
                return StatusCode(500, new { error = "Failed to send push notification" });
            }
        }

        /// <summary>
        /// Guest: Subscribe to push notifications (no auth required)
        /// </summary>
        [AllowAnonymous]
        [HttpPost("guest/subscribe")]
        public async Task<ActionResult> GuestSubscribe([FromBody] GuestPushSubscriptionRequest request, [FromHeader(Name = "X-Tenant-ID")] int? headerTenantId = null)
        {
            var tenantId = headerTenantId ?? HttpContext.Items["TenantId"] as int? ?? 1;

            try
            {
                // Check if subscription already exists
                var existingSubscription = await _context.Set<PushSubscription>()
                    .FirstOrDefaultAsync(s =>
                        s.Endpoint == request.Endpoint &&
                        s.TenantId == tenantId &&
                        s.IsGuest);

                if (existingSubscription != null)
                {
                    // Update existing subscription
                    existingSubscription.P256dhKey = request.Keys.P256dh;
                    existingSubscription.AuthKey = request.Keys.Auth;
                    existingSubscription.DeviceInfo = request.DeviceInfo;
                    existingSubscription.GuestPhone = request.Phone;
                    existingSubscription.RoomNumber = request.RoomNumber;
                    existingSubscription.LastUsedAt = DateTime.UtcNow;
                    existingSubscription.IsActive = true;

                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Updated guest push subscription for tenant {TenantId}, phone {Phone}", tenantId, request.Phone);

                    return Ok(new { message = "Guest subscription updated successfully" });
                }

                // Create new subscription
                var subscription = new PushSubscription
                {
                    TenantId = tenantId,
                    Endpoint = request.Endpoint,
                    P256dhKey = request.Keys.P256dh,
                    AuthKey = request.Keys.Auth,
                    DeviceInfo = request.DeviceInfo,
                    GuestPhone = request.Phone,
                    RoomNumber = request.RoomNumber,
                    IsGuest = true,
                    CreatedAt = DateTime.UtcNow,
                    LastUsedAt = DateTime.UtcNow,
                    IsActive = true
                };

                _context.Set<PushSubscription>().Add(subscription);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Created guest push subscription for tenant {TenantId}, phone {Phone}", tenantId, request.Phone);

                return Ok(new { message = "Guest subscribed to push notifications successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error subscribing guest to push notifications");
                return StatusCode(500, new { error = "Failed to subscribe to push notifications" });
            }
        }

        /// <summary>
        /// Guest: Unsubscribe from push notifications (no auth required)
        /// </summary>
        [AllowAnonymous]
        [HttpPost("guest/unsubscribe")]
        public async Task<ActionResult> GuestUnsubscribe([FromBody] UnsubscribeRequest request, [FromHeader(Name = "X-Tenant-ID")] int? headerTenantId = null)
        {
            var tenantId = headerTenantId ?? HttpContext.Items["TenantId"] as int? ?? 1;

            try
            {
                var subscription = await _context.Set<PushSubscription>()
                    .FirstOrDefaultAsync(s =>
                        s.Endpoint == request.Endpoint &&
                        s.TenantId == tenantId &&
                        s.IsGuest);

                if (subscription == null)
                {
                    return NotFound(new { error = "Guest subscription not found" });
                }

                subscription.IsActive = false;
                await _context.SaveChangesAsync();

                _logger.LogInformation("Guest unsubscribed from push notifications for tenant {TenantId}", tenantId);

                return Ok(new { message = "Guest unsubscribed successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unsubscribing guest from push notifications");
                return StatusCode(500, new { error = "Failed to unsubscribe" });
            }
        }

        /// <summary>
        /// Delete subscription by ID
        /// </summary>
        [HttpDelete("subscriptions/{id}")]
        public async Task<ActionResult> DeleteSubscription(int id)
        {
            var tenantId = HttpContext.Items["TenantId"] as int? ?? 0;

            if (tenantId == 0)
            {
                return BadRequest(new { error = "Tenant ID not found" });
            }

            try
            {
                var subscription = await _context.Set<PushSubscription>()
                    .FirstOrDefaultAsync(s => s.Id == id && s.TenantId == tenantId);

                if (subscription == null)
                {
                    return NotFound(new { error = "Subscription not found" });
                }

                _context.Set<PushSubscription>().Remove(subscription);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Deleted subscription {SubscriptionId} for tenant {TenantId}", id, tenantId);

                return Ok(new { message = "Subscription deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting subscription");
                return StatusCode(500, new { error = "Failed to delete subscription" });
            }
        }
    }
}
