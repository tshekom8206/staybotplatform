using Hostr.Api.Models;

namespace Hostr.Api.Services;

public interface IUpsellService
{
    Task<UpsellRecommendation?> GetRelevantUpsellAsync(
        int tenantId,
        int conversationId,
        UpsellContext context);

    Task<List<UpsellRecommendation>> GetMultipleUpsellsAsync(
        int tenantId,
        int conversationId,
        UpsellContext context,
        int maxSuggestions = 2);

    Task TrackSuggestionAsync(
        int conversationId,
        int upsellItemId,
        bool wasAccepted);
}

public class UpsellContext
{
    public string PrimaryIntent { get; set; } = string.Empty;
    public string ServiceCategory { get; set; } = string.Empty;
    public string SpecificItem { get; set; } = string.Empty;
    public int? BookingId { get; set; }
    public string? GuestName { get; set; }
    public int? NumberOfPeople { get; set; }
    public DateOnly? RequestedDate { get; set; }
    public TimeOnly? RequestedTime { get; set; }
    public string CurrentStage { get; set; } = "request"; // request|confirmation|fulfillment
    public List<string> ConversationHistory { get; set; } = new();
    public Dictionary<string, object> AdditionalContext { get; set; } = new();
}

public class UpsellRecommendation
{
    public int UpsellItemId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int PriceCents { get; set; }
    public string Unit { get; set; } = string.Empty;
    public double RelevanceScore { get; set; }
    public string RelevanceReason { get; set; } = string.Empty;
    public string SuggestionText { get; set; } = string.Empty; // Warm, concierge-style suggestion
    public bool IsHighConfidence { get; set; }
    public List<string> MatchedCategories { get; set; } = new();
}
