using System.Text.Json.Serialization;

namespace Hostr.Contracts.DTOs.WhatsApp;

public record WebhookPayload
{
    [JsonPropertyName("object")]
    public string Object { get; init; } = string.Empty;
    
    [JsonPropertyName("entry")]
    public List<WebhookEntry> Entry { get; init; } = new();
}

public record WebhookEntry
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;
    
    [JsonPropertyName("changes")]
    public List<WebhookChange> Changes { get; init; } = new();
}

public record WebhookChange
{
    [JsonPropertyName("value")]
    public WebhookValue Value { get; init; } = new();
    
    [JsonPropertyName("field")]
    public string Field { get; init; } = string.Empty;
}

public record WebhookValue
{
    [JsonPropertyName("messaging_product")]
    public string MessagingProduct { get; init; } = string.Empty;
    
    [JsonPropertyName("metadata")]
    public WebhookMetadata Metadata { get; init; } = new();
    
    [JsonPropertyName("contacts")]
    public List<WebhookContact> Contacts { get; init; } = new();
    
    [JsonPropertyName("messages")]
    public List<WebhookMessage> Messages { get; init; } = new();
}

public record WebhookMetadata
{
    [JsonPropertyName("display_phone_number")]
    public string DisplayPhoneNumber { get; init; } = string.Empty;
    
    [JsonPropertyName("phone_number_id")]
    public string PhoneNumberId { get; init; } = string.Empty;
}

public record WebhookContact
{
    [JsonPropertyName("profile")]
    public WebhookProfile Profile { get; init; } = new();
    
    [JsonPropertyName("wa_id")]
    public string WaId { get; init; } = string.Empty;
}

public record WebhookProfile
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;
}

public record WebhookMessage
{
    [JsonPropertyName("from")]
    public string From { get; init; } = string.Empty;
    
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;
    
    [JsonPropertyName("timestamp")]
    public string Timestamp { get; init; } = string.Empty;
    
    [JsonPropertyName("text")]
    public WebhookText? Text { get; init; }
    
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;
}

public record WebhookText
{
    [JsonPropertyName("body")]
    public string Body { get; init; } = string.Empty;
}

public record OutboundMessage
{
    [JsonPropertyName("messaging_product")]
    public string MessagingProduct { get; init; } = "whatsapp";
    
    [JsonPropertyName("to")]
    public string To { get; init; } = string.Empty;
    
    [JsonPropertyName("type")]
    public string Type { get; init; } = "text";
    
    [JsonPropertyName("text")]
    public OutboundText? Text { get; init; }
    
    [JsonPropertyName("template")]
    public OutboundTemplate? Template { get; init; }
}

public record OutboundText
{
    [JsonPropertyName("body")]
    public string Body { get; init; } = string.Empty;
}

public record OutboundTemplate
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;
    
    [JsonPropertyName("language")]
    public OutboundLanguage Language { get; init; } = new();
}

public record OutboundLanguage
{
    [JsonPropertyName("code")]
    public string Code { get; init; } = "en";
}