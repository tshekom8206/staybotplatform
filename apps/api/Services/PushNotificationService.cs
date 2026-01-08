using Hostr.Api.Data;
using Hostr.Api.Models;
using Microsoft.EntityFrameworkCore;
using WebPush;

namespace Hostr.Api.Services;

public interface IPushNotificationService
{
    // Staff notifications
    Task NotifyTaskAssigned(int agentId, int taskId, string taskTitle, string taskDescription);
    Task NotifyConversationAssigned(int agentId, int conversationId, string guestPhone);
    Task NotifyNewMessage(int agentId, int conversationId, string messageText, string guestPhone);
    Task NotifyEmergency(int tenantId, string emergencyType, string location, string description);

    // Guest notifications
    Task NotifyGuestServiceUpdate(int tenantId, string guestPhone, string serviceName, string status, string? details = null);
    Task NotifyGuestCheckoutReminder(int tenantId, string guestPhone, string guestName, DateTime checkoutTime);
    Task NotifyGuestEmergency(int tenantId, string message, string? location = null);
    Task NotifyGuestBroadcast(int tenantId, string guestPhone, string title, string message, string priority, bool isEmergency = false);
}

public class PushNotificationService : IPushNotificationService
{
    private readonly HostrDbContext _context;
    private readonly ILogger<PushNotificationService> _logger;
    private readonly IConfiguration _configuration;
    private readonly WebPushClient _webPushClient;

    public PushNotificationService(
        HostrDbContext context,
        ILogger<PushNotificationService> logger,
        IConfiguration configuration)
    {
        _context = context;
        _logger = logger;
        _configuration = configuration;
        _webPushClient = new WebPushClient();
    }

    public async Task NotifyTaskAssigned(int agentId, int taskId, string taskTitle, string taskDescription)
    {
        _logger.LogInformation($"Sending task assignment notification to agent {agentId} for task {taskId}");

        var payload = new
        {
            title = "🎯 New Task Assigned",
            body = taskTitle,
            icon = "/icons/icon-192x192.png",
            badge = "/icons/icon-72x72.png",
            tag = $"task-{taskId}",
            data = new
            {
                type = "task_assigned",
                taskId = taskId,
                url = $"/tasks/{taskId}",
                timestamp = DateTime.UtcNow
            },
            requireInteraction = true,
            vibrate = new[] { 200, 100, 200 }
        };

        await SendToAgent(agentId, payload);
    }

    public async Task NotifyConversationAssigned(int agentId, int conversationId, string guestPhone)
    {
        _logger.LogInformation($"Sending conversation assignment notification to agent {agentId} for conversation {conversationId}");

        var payload = new
        {
            title = "💬 New Conversation Assigned",
            body = $"Guest {guestPhone} needs assistance",
            icon = "/icons/icon-192x192.png",
            badge = "/icons/icon-72x72.png",
            tag = $"conversation-{conversationId}",
            data = new
            {
                type = "conversation_assigned",
                conversationId = conversationId,
                url = $"/conversations/{conversationId}",
                guestPhone = guestPhone,
                timestamp = DateTime.UtcNow
            },
            requireInteraction = true,
            vibrate = new[] { 200, 100, 200 }
        };

        await SendToAgent(agentId, payload);
    }

    public async Task NotifyNewMessage(int agentId, int conversationId, string messageText, string guestPhone)
    {
        _logger.LogInformation($"Sending new message notification to agent {agentId} for conversation {conversationId}");

        var preview = messageText.Length > 50
            ? messageText.Substring(0, 50) + "..."
            : messageText;

        var payload = new
        {
            title = $"💬 New Message from {guestPhone}",
            body = preview,
            icon = "/icons/icon-192x192.png",
            badge = "/icons/icon-72x72.png",
            tag = $"message-{conversationId}",
            data = new
            {
                type = "new_message",
                conversationId = conversationId,
                url = $"/conversations/{conversationId}",
                guestPhone = guestPhone,
                timestamp = DateTime.UtcNow
            },
            vibrate = new[] { 200, 100, 200 }
        };

        await SendToAgent(agentId, payload);
    }

    public async Task NotifyEmergency(int tenantId, string emergencyType, string location, string description)
    {
        _logger.LogInformation($"Sending emergency notification to all agents in tenant {tenantId}");

        var payload = new
        {
            title = $"🚨 EMERGENCY: {emergencyType}",
            body = $"{location} - {description}",
            icon = "/icons/icon-192x192.png",
            badge = "/icons/emergency-badge.png",
            tag = $"emergency-{Guid.NewGuid()}",
            data = new
            {
                type = "emergency",
                emergencyType = emergencyType,
                location = location,
                url = "/emergencies",
                timestamp = DateTime.UtcNow
            },
            requireInteraction = true,
            vibrate = new[] { 300, 100, 300, 100, 300 }
        };

        await SendToTenantAgents(tenantId, payload);
    }

    // Private helper methods

    private async Task SendToAgent(int agentId, object payload)
    {
        var subscriptions = await _context.PushSubscriptions
            .Where(s => s.UserId == agentId && s.IsActive)
            .ToListAsync();

        if (!subscriptions.Any())
        {
            _logger.LogInformation($"No active push subscriptions for agent {agentId}");
            return;
        }

        _logger.LogInformation($"Sending notification to agent {agentId} ({subscriptions.Count} device(s))");

        var tasks = subscriptions.Select(subscription =>
            SendPushNotification(subscription, payload));

        await Task.WhenAll(tasks);
    }

    private async Task SendToTenantAgents(int tenantId, object payload)
    {
        // Get all user IDs with active subscriptions in this tenant
        var activeUserIds = await _context.PushSubscriptions
            .Where(s => s.TenantId == tenantId && s.IsActive && s.UserId.HasValue)
            .Select(s => s.UserId.Value)
            .Distinct()
            .ToListAsync();

        if (!activeUserIds.Any())
        {
            _logger.LogInformation($"No active push subscriptions in tenant {tenantId}");
            return;
        }

        _logger.LogInformation($"Sending emergency notification to {activeUserIds.Count} user(s) in tenant {tenantId}");

        var tasks = activeUserIds.Select(userId => SendToAgent(userId, payload));
        await Task.WhenAll(tasks);
    }

    private async Task SendPushNotification(Models.PushSubscription subscription, object payload)
    {
        try
        {
            var vapidSubject = _configuration["WebPush:Subject"];
            var vapidPublicKey = _configuration["WebPush:PublicKey"];
            var vapidPrivateKey = _configuration["WebPush:PrivateKey"];

            if (string.IsNullOrEmpty(vapidSubject) ||
                string.IsNullOrEmpty(vapidPublicKey) ||
                string.IsNullOrEmpty(vapidPrivateKey))
            {
                _logger.LogError("VAPID configuration is missing in appsettings.json");
                return;
            }

            var vapidDetails = new VapidDetails(
                subject: vapidSubject,
                publicKey: vapidPublicKey,
                privateKey: vapidPrivateKey
            );

            var webPushSubscription = new WebPush.PushSubscription(
                endpoint: subscription.Endpoint,
                p256dh: subscription.P256dhKey,
                auth: subscription.AuthKey
            );

            var payloadJson = System.Text.Json.JsonSerializer.Serialize(payload);

            await _webPushClient.SendNotificationAsync(
                webPushSubscription,
                payloadJson,
                vapidDetails
            );

            // Update last used timestamp
            subscription.LastUsedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Push notification sent successfully to subscription {subscription.Id}");
        }
        catch (WebPushException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Gone)
        {
            // 410 Gone - Subscription expired
            _logger.LogWarning($"Subscription {subscription.Id} expired (410 Gone) - deactivating");
            subscription.IsActive = false;
            await _context.SaveChangesAsync();
        }
        catch (WebPushException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // 404 Not Found - Subscription not found
            _logger.LogWarning($"Subscription {subscription.Id} not found (404) - deactivating");
            subscription.IsActive = false;
            await _context.SaveChangesAsync();
        }
        catch (WebPushException ex)
        {
            _logger.LogError(ex, $"WebPush error for subscription {subscription.Id}: {ex.StatusCode} - {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to send push notification to subscription {subscription.Id}");
        }
    }

    // Guest notification implementations

    public async Task NotifyGuestServiceUpdate(int tenantId, string guestPhone, string serviceName, string status, string? details = null)
    {
        _logger.LogInformation($"Sending service update notification to guest {guestPhone} for {serviceName}: {status}");

        var payload = new
        {
            title = $"Service Update: {serviceName}",
            body = details ?? $"Your {serviceName} request is now {status}",
            icon = "/icons/icon-192x192.png",
            badge = "/icons/icon-72x72.png",
            tag = $"service-{serviceName.ToLower().Replace(" ", "-")}-{DateTime.UtcNow.Ticks}",
            data = new
            {
                type = "service_update",
                serviceName = serviceName,
                status = status,
                url = "/services",
                timestamp = DateTime.UtcNow
            },
            requireInteraction = status.ToLower() == "completed"
        };

        await SendToGuest(tenantId, guestPhone, payload);
    }

    public async Task NotifyGuestCheckoutReminder(int tenantId, string guestPhone, string guestName, DateTime checkoutTime)
    {
        _logger.LogInformation($"Sending checkout reminder to guest {guestPhone}");

        var timeString = checkoutTime.ToString("h:mm tt");
        var payload = new
        {
            title = "Checkout Reminder",
            body = $"Hi {guestName}, your checkout time is {timeString}. Need late checkout? Just ask!",
            icon = "/icons/icon-192x192.png",
            badge = "/icons/icon-72x72.png",
            tag = "checkout-reminder",
            data = new
            {
                type = "checkout_reminder",
                checkoutTime = checkoutTime,
                url = "/",
                timestamp = DateTime.UtcNow
            },
            requireInteraction = false
        };

        await SendToGuest(tenantId, guestPhone, payload);
    }

    public async Task NotifyGuestEmergency(int tenantId, string message, string? location = null)
    {
        _logger.LogWarning($"Sending emergency notification to all guests in tenant {tenantId}: {message}");

        var body = string.IsNullOrEmpty(location) ? message : $"{location}: {message}";
        var payload = new
        {
            title = "EMERGENCY ALERT",
            body = body,
            icon = "/icons/icon-192x192.png",
            badge = "/icons/emergency-badge.png",
            tag = $"emergency-{Guid.NewGuid()}",
            data = new
            {
                type = "emergency",
                isEmergency = true,
                location = location,
                url = "/",
                timestamp = DateTime.UtcNow
            },
            requireInteraction = true,
            vibrate = new[] { 300, 100, 300, 100, 300 }
        };

        await SendToAllGuests(tenantId, payload);
    }

    public async Task NotifyGuestBroadcast(int tenantId, string guestPhone, string title, string message, string priority, bool isEmergency = false)
    {
        _logger.LogInformation($"Sending broadcast notification to guest {guestPhone}: {title}");

        // Truncate message for notification body if too long
        var body = message.Length > 200 ? message.Substring(0, 200) + "..." : message;

        var payload = new
        {
            title = isEmergency ? $"🚨 {title}" : $"📢 {title}",
            body = body,
            icon = "/icons/icon-192x192.png",
            badge = isEmergency ? "/icons/emergency-badge.png" : "/icons/icon-72x72.png",
            tag = $"broadcast-{DateTime.UtcNow.Ticks}",
            data = new
            {
                type = "broadcast",
                priority = priority,
                isEmergency = isEmergency,
                url = "/",
                timestamp = DateTime.UtcNow
            },
            requireInteraction = isEmergency || priority == "high",
            vibrate = isEmergency ? new[] { 300, 100, 300, 100, 300 } : (priority == "high" ? new[] { 200, 100, 200 } : null)
        };

        await SendToGuest(tenantId, guestPhone, payload);
    }

    private async Task SendToGuest(int tenantId, string guestPhone, object payload)
    {
        // Look up by phone OR room number
        var subscriptions = await _context.PushSubscriptions
            .Where(s => s.TenantId == tenantId &&
                       (s.GuestPhone == guestPhone || s.RoomNumber == guestPhone) &&
                       s.IsGuest &&
                       s.IsActive)
            .ToListAsync();

        if (!subscriptions.Any())
        {
            _logger.LogInformation($"No active push subscriptions for guest {guestPhone}");
            return;
        }

        _logger.LogInformation($"Sending notification to guest {guestPhone} ({subscriptions.Count} device(s))");

        var tasks = subscriptions.Select(subscription =>
            SendPushNotification(subscription, payload));

        await Task.WhenAll(tasks);
    }

    private async Task SendToAllGuests(int tenantId, object payload)
    {
        var subscriptions = await _context.PushSubscriptions
            .Where(s => s.TenantId == tenantId && s.IsGuest && s.IsActive)
            .ToListAsync();

        if (!subscriptions.Any())
        {
            _logger.LogInformation($"No active guest push subscriptions in tenant {tenantId}");
            return;
        }

        _logger.LogInformation($"Sending emergency notification to {subscriptions.Count} guest device(s) in tenant {tenantId}");

        var tasks = subscriptions.Select(subscription =>
            SendPushNotification(subscription, payload));

        await Task.WhenAll(tasks);
    }
}
