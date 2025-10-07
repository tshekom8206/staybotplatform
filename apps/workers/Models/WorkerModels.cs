using System.ComponentModel.DataAnnotations;
using Pgvector;

namespace Hostr.Workers.Models;

public class Tenant
{
    public int Id { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Plan { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int RetentionDays { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class FAQ
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
    public bool NeedsEmbedding { get; set; } = true;
    public virtual Tenant Tenant { get; set; } = null!;
}

public class KnowledgeBaseChunk
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public string Source { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public Vector Embedding { get; set; } = new(Array.Empty<float>());
    public DateTime UpdatedAt { get; set; }
    public bool NeedsEmbedding { get; set; } = true;
    public virtual Tenant Tenant { get; set; } = null!;
}

public class Rating
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int? BookingId { get; set; }
    public int? ConversationId { get; set; }
    public string GuestPhone { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public int? Score { get; set; }
    public string? Comment { get; set; }
    public DateTime? AskedAt { get; set; }
    public DateTime? ReceivedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public virtual Tenant Tenant { get; set; } = null!;
    public virtual Booking? Booking { get; set; }
}

public class Booking
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public string GuestName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public DateOnly CheckinDate { get; set; }
    public DateOnly CheckoutDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public virtual Tenant Tenant { get; set; } = null!;
    public virtual ICollection<Rating> Ratings { get; set; } = new List<Rating>();
}

public class Message
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int ConversationId { get; set; }
    public string Direction { get; set; } = string.Empty;
    public string MessageType { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public int? TokensPrompt { get; set; }
    public int? TokensCompletion { get; set; }
    public DateTime CreatedAt { get; set; }
    public virtual Tenant Tenant { get; set; } = null!;
}

public class Conversation
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public string WaUserPhone { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public virtual Tenant Tenant { get; set; } = null!;
}

public class UsageDaily
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public DateOnly Date { get; set; }
    public int MessagesIn { get; set; }
    public int MessagesOut { get; set; }
    public int TokensIn { get; set; }
    public int TokensOut { get; set; }
    public int UpsellRevenueCents { get; set; }
    public DateTime CreatedAt { get; set; }
    public virtual Tenant Tenant { get; set; } = null!;
}