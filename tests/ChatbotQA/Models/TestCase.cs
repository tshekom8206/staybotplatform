using System.Text.Json.Serialization;

namespace Hostr.Tests.ChatbotQA.Models;

public class TestCase
{
    [JsonPropertyName("case_id")]
    public string CaseId { get; set; } = string.Empty;

    [JsonPropertyName("guest_message")]
    public string GuestMessage { get; set; } = string.Empty;

    [JsonPropertyName("chatbot_response")]
    public string ChatbotResponse { get; set; } = string.Empty;

    [JsonPropertyName("hotel_data")]
    public HotelData HotelData { get; set; } = new();

    [JsonPropertyName("locale")]
    public string Locale { get; set; } = "en-ZA";

    [JsonPropertyName("expected_notes")]
    public string? ExpectedNotes { get; set; }

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();
}
