using System;

namespace Hostr.Api.Models
{
    public class PushSubscription
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
        public int? UserId { get; set; }
        public string Endpoint { get; set; } = string.Empty;
        public string P256dhKey { get; set; } = string.Empty;
        public string AuthKey { get; set; } = string.Empty;
        public string? DeviceInfo { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastUsedAt { get; set; }
        public bool IsActive { get; set; } = true;

        // Navigation properties
        public Tenant? Tenant { get; set; }
    }

    public class PushSubscriptionRequest
    {
        public string Endpoint { get; set; } = string.Empty;
        public PushKeys Keys { get; set; } = new PushKeys();
        public string? DeviceInfo { get; set; }
    }

    public class PushKeys
    {
        public string P256dh { get; set; } = string.Empty;
        public string Auth { get; set; } = string.Empty;
    }

    public class UnsubscribeRequest
    {
        public string Endpoint { get; set; } = string.Empty;
    }

    public class SendPushNotificationRequest
    {
        public string Title { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public string? Icon { get; set; }
        public string? Badge { get; set; }
        public string? Tag { get; set; }
        public bool RequireInteraction { get; set; }
        public object? Data { get; set; }
        public int? UserId { get; set; } // If null, send to all users in tenant
    }
}
