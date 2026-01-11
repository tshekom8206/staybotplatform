using System.Text.Json.Serialization;

namespace Hostr.Tests.ChatbotQA.Models;

public class HotelData
{
    [JsonPropertyName("policies")]
    public List<string> Policies { get; set; } = new();

    [JsonPropertyName("kb_snippets")]
    public List<string> KbSnippets { get; set; } = new();

    [JsonPropertyName("retrieved_context")]
    public string? RetrievedContext { get; set; }
}
