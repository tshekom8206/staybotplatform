using Microsoft.EntityFrameworkCore;
using Hostr.Api.Data;
using Hostr.Api.Models;
using System.Text.RegularExpressions;

namespace Hostr.Api.Services;

public interface IContextRelevanceService
{
    Task<List<RelevantContext>> ScoreConversationHistoryAsync(int conversationId, string currentMessage, int maxResults = 5);
    Task<List<RelevantContext>> GetRelevantBookingContextAsync(int conversationId, string phoneNumber, int tenantId);
    Task<List<RelevantContext>> GetRelevantServiceHistoryAsync(int conversationId, string phoneNumber, int tenantId);
    Task<double> CalculateMessageRelevanceAsync(string currentMessage, string historicalMessage, DateTime messageTime);
    Task<List<RelevantContext>> FilterByTemporalRelevanceAsync(List<RelevantContext> contexts, TimeSpan maxAge);
}

public enum ContextType
{
    ConversationHistory = 1,
    BookingInformation = 2,
    ServiceHistory = 3,
    PreferenceData = 4,
    TaskHistory = 5,
    ComplaintHistory = 6,
    MenuInteractions = 7,
    EmergencyContext = 8
}

public class RelevantContext
{
    public ContextType Type { get; set; }
    public string Content { get; set; } = string.Empty;
    public double RelevanceScore { get; set; }
    public DateTime Timestamp { get; set; }
    public string Source { get; set; } = string.Empty;
    public Dictionary<string, object> Metadata { get; set; } = new();
    public TimeSpan Age => DateTime.UtcNow - Timestamp;
    public bool IsRecent => Age.TotalDays <= 7;
    public bool IsCritical { get; set; }
}

public class ContextRelevanceService : IContextRelevanceService
{
    private readonly HostrDbContext _context;
    private readonly ILogger<ContextRelevanceService> _logger;
    private readonly ITemporalContextService _temporalContextService;

    // Keyword weights for different categories
    private static readonly Dictionary<string, double> ServiceKeywords = new()
    {
        { "towel", 2.0 }, { "housekeeping", 2.0 }, { "clean", 1.8 }, { "maintenance", 2.0 },
        { "room service", 2.5 }, { "food", 1.5 }, { "menu", 1.8 }, { "restaurant", 1.8 },
        { "spa", 2.0 }, { "booking", 2.5 }, { "reservation", 2.5 }, { "cancel", 2.0 },
        { "wifi", 1.5 }, { "internet", 1.5 }, { "tv", 1.2 }, { "air conditioning", 1.8 }
    };

    private static readonly Dictionary<string, double> TemporalKeywords = new()
    {
        { "today", 1.8 }, { "tonight", 2.0 }, { "tomorrow", 1.5 }, { "now", 2.5 },
        { "later", 1.2 }, { "morning", 1.3 }, { "evening", 1.3 }, { "lunch", 1.5 },
        { "dinner", 1.5 }, { "breakfast", 1.5 }
    };

    public ContextRelevanceService(
        HostrDbContext context,
        ILogger<ContextRelevanceService> logger,
        ITemporalContextService temporalContextService)
    {
        _context = context;
        _logger = logger;
        _temporalContextService = temporalContextService;
    }

    public async Task<List<RelevantContext>> ScoreConversationHistoryAsync(int conversationId, string currentMessage, int maxResults = 5)
    {
        try
        {
            var contexts = new List<RelevantContext>();

            // Get conversation history
            var messages = await _context.Messages
                .Where(m => m.ConversationId == conversationId)
                .OrderByDescending(m => m.CreatedAt)
                .Take(20) // Look at last 20 messages
                .ToListAsync();

            foreach (var message in messages)
            {
                if (string.IsNullOrEmpty(message.Body)) continue;

                var relevanceScore = await CalculateMessageRelevanceAsync(currentMessage, message.Body, message.CreatedAt);

                if (relevanceScore > 0.3) // Threshold for relevance
                {
                    contexts.Add(new RelevantContext
                    {
                        Type = ContextType.ConversationHistory,
                        Content = message.Body,
                        RelevanceScore = relevanceScore,
                        Timestamp = message.CreatedAt,
                        Source = $"Message {message.Id}",
                        Metadata = new Dictionary<string, object>
                        {
                            { "direction", message.Direction },
                            { "messageId", message.Id }
                        }
                    });
                }
            }

            // Get task history context
            var taskContexts = await GetTaskHistoryContextAsync(conversationId, currentMessage);
            contexts.AddRange(taskContexts);

            // Sort by relevance score and return top results
            return contexts
                .OrderByDescending(c => c.RelevanceScore)
                .ThenByDescending(c => c.Timestamp)
                .Take(maxResults)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scoring conversation history for conversation {ConversationId}", conversationId);
            return new List<RelevantContext>();
        }
    }

    public async Task<List<RelevantContext>> GetRelevantBookingContextAsync(int conversationId, string phoneNumber, int tenantId)
    {
        try
        {
            var contexts = new List<RelevantContext>();

            // Get guest's bookings
            var bookings = await _context.Bookings
                .Where(b => b.Phone == phoneNumber && b.TenantId == tenantId)
                .OrderByDescending(b => b.CheckInDate)
                .Take(5)
                .ToListAsync();

            foreach (var booking in bookings)
            {
                var relevanceScore = CalculateBookingRelevance(booking);

                contexts.Add(new RelevantContext
                {
                    Type = ContextType.BookingInformation,
                    Content = $"Booking for {booking.GuestName} in room {booking.RoomNumber}, {booking.CheckInDate:MMM dd} - {booking.CheckOutDate:MMM dd}",
                    RelevanceScore = relevanceScore,
                    Timestamp = booking.CreatedAt,
                    Source = $"Booking {booking.Id}",
                    IsCritical = booking.CheckInDate <= DateTime.Today && booking.CheckOutDate >= DateTime.Today,
                    Metadata = new Dictionary<string, object>
                    {
                        { "bookingId", booking.Id },
                        { "roomNumber", booking.RoomNumber ?? "N/A" },
                        { "checkInDate", booking.CheckInDate },
                        { "checkOutDate", booking.CheckOutDate },
                        { "isActive", booking.CheckInDate <= DateTime.Today && booking.CheckOutDate >= DateTime.Today }
                    }
                });
            }

            return contexts.OrderByDescending(c => c.RelevanceScore).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting booking context for phone {PhoneNumber}", phoneNumber);
            return new List<RelevantContext>();
        }
    }

    public async Task<List<RelevantContext>> GetRelevantServiceHistoryAsync(int conversationId, string phoneNumber, int tenantId)
    {
        try
        {
            var contexts = new List<RelevantContext>();
            var cutoffDate = DateTime.UtcNow.AddDays(-30); // Look back 30 days

            // Get staff tasks created for this guest
            var tasks = await _context.StaffTasks
                .Where(t => t.TenantId == tenantId &&
                           t.CreatedAt >= cutoffDate &&
                           (t.Description.Contains(phoneNumber) || t.GuestPhone == phoneNumber))
                .OrderByDescending(t => t.CreatedAt)
                .Take(10)
                .ToListAsync();

            foreach (var task in tasks)
            {
                var relevanceScore = CalculateTaskRelevance(task);

                contexts.Add(new RelevantContext
                {
                    Type = ContextType.ServiceHistory,
                    Content = $"Previous request: {task.Title} - {task.Description}",
                    RelevanceScore = relevanceScore,
                    Timestamp = task.CreatedAt,
                    Source = $"Task {task.Id}",
                    IsCritical = task.Status == "Pending" || task.Status == "InProgress",
                    Metadata = new Dictionary<string, object>
                    {
                        { "taskId", task.Id },
                        { "status", task.Status },
                        { "priority", task.Priority },
                        { "assignedTo", task.AssignedToId?.ToString() ?? "Unassigned" }
                    }
                });
            }

            return contexts.OrderByDescending(c => c.RelevanceScore).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting service history for phone {PhoneNumber}", phoneNumber);
            return new List<RelevantContext>();
        }
    }

    public async Task<double> CalculateMessageRelevanceAsync(string currentMessage, string historicalMessage, DateTime messageTime)
    {
        try
        {
            if (string.IsNullOrEmpty(currentMessage) || string.IsNullOrEmpty(historicalMessage))
                return 0.0;

            var currentWords = TokenizeMessage(currentMessage.ToLower());
            var historicalWords = TokenizeMessage(historicalMessage.ToLower());

            double score = 0.0;

            // 1. Exact keyword matches (highest weight)
            var commonKeywords = currentWords.Intersect(historicalWords).ToList();
            score += commonKeywords.Count * 0.3;

            // 2. Service-related keyword matches (high weight)
            foreach (var word in commonKeywords)
            {
                if (ServiceKeywords.TryGetValue(word, out var weight))
                {
                    score += weight * 0.2;
                }
            }

            // 3. Temporal keyword matches
            foreach (var word in commonKeywords)
            {
                if (TemporalKeywords.TryGetValue(word, out var weight))
                {
                    score += weight * 0.1;
                }
            }

            // 4. Semantic similarity (enhanced approach with synonyms and context)
            var semanticScore = CalculateEnhancedSemanticSimilarity(currentWords, historicalWords, currentMessage, historicalMessage);
            score += semanticScore * 0.25;

            // 5. Temporal decay (older messages less relevant)
            var timeDecay = CalculateTimeDecay(messageTime);
            score *= timeDecay;

            // 6. Message length penalty (very short or very long messages less relevant)
            var lengthPenalty = CalculateLengthPenalty(historicalMessage);
            score *= lengthPenalty;

            // 7. Intent similarity boost
            var intentBoost = CalculateIntentSimilarity(currentMessage, historicalMessage);
            score += intentBoost * 0.15;

            // 8. Entity recognition boost (room numbers, dates, names)
            var entityBoost = CalculateEntityOverlap(currentMessage, historicalMessage);
            score += entityBoost * 0.1;

            return Math.Min(score, 1.0); // Cap at 1.0
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating message relevance");
            return 0.0;
        }
    }

    public async Task<List<RelevantContext>> FilterByTemporalRelevanceAsync(List<RelevantContext> contexts, TimeSpan maxAge)
    {
        try
        {
            var cutoffTime = DateTime.UtcNow - maxAge;

            return contexts
                .Where(c => c.Timestamp >= cutoffTime || c.IsCritical) // Keep critical contexts regardless of age
                .OrderByDescending(c => c.RelevanceScore)
                .ThenByDescending(c => c.Timestamp)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error filtering contexts by temporal relevance");
            return contexts;
        }
    }

    private async Task<List<RelevantContext>> GetTaskHistoryContextAsync(int conversationId, string currentMessage)
    {
        try
        {
            var conversation = await _context.Conversations
                .Include(c => c.Messages)
                .FirstOrDefaultAsync(c => c.Id == conversationId);

            if (conversation == null) return new List<RelevantContext>();

            var phoneNumber = conversation.WaUserPhone;
            var cutoffDate = DateTime.UtcNow.AddDays(-7);

            var tasks = await _context.StaffTasks
                .Where(t => t.GuestPhone == phoneNumber && t.CreatedAt >= cutoffDate)
                .OrderByDescending(t => t.CreatedAt)
                .Take(5)
                .ToListAsync();

            var contexts = new List<RelevantContext>();

            foreach (var task in tasks)
            {
                var relevanceScore = await CalculateMessageRelevanceAsync(currentMessage, $"{task.Title} {task.Description}", task.CreatedAt);

                if (relevanceScore > 0.2)
                {
                    contexts.Add(new RelevantContext
                    {
                        Type = ContextType.TaskHistory,
                        Content = $"Recent task: {task.Title}",
                        RelevanceScore = relevanceScore,
                        Timestamp = task.CreatedAt,
                        Source = $"Task {task.Id}",
                        IsCritical = task.Status == "Pending",
                        Metadata = new Dictionary<string, object>
                        {
                            { "taskId", task.Id },
                            { "status", task.Status }
                        }
                    });
                }
            }

            return contexts;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting task history context");
            return new List<RelevantContext>();
        }
    }

    private List<string> TokenizeMessage(string message)
    {
        // Simple tokenization - split by spaces and punctuation, remove stop words
        var stopWords = new HashSet<string> { "the", "a", "an", "and", "or", "but", "in", "on", "at", "to", "for", "of", "with", "by", "is", "are", "was", "were", "be", "been", "have", "has", "had", "do", "does", "did", "will", "would", "could", "should", "can", "i", "you", "he", "she", "it", "we", "they", "me", "him", "her", "us", "them" };

        var words = Regex.Split(message, @"\W+")
            .Where(w => w.Length > 2 && !stopWords.Contains(w))
            .ToList();

        return words;
    }

    private double CalculateSimpleSemanticSimilarity(List<string> words1, List<string> words2)
    {
        if (!words1.Any() || !words2.Any()) return 0.0;

        var intersection = words1.Intersect(words2).Count();
        var union = words1.Union(words2).Count();

        return union > 0 ? (double)intersection / union : 0.0;
    }

    private double CalculateTimeDecay(DateTime messageTime)
    {
        var age = DateTime.UtcNow - messageTime;

        // Recent messages (< 1 hour) get full weight
        if (age.TotalHours < 1) return 1.0;

        // Messages within 24 hours get 80% weight
        if (age.TotalDays < 1) return 0.8;

        // Messages within 7 days get 60% weight
        if (age.TotalDays < 7) return 0.6;

        // Messages within 30 days get 40% weight
        if (age.TotalDays < 30) return 0.4;

        // Older messages get minimal weight
        return 0.2;
    }

    private double CalculateLengthPenalty(string message)
    {
        var length = message.Length;

        // Optimal length is 20-200 characters
        if (length >= 20 && length <= 200) return 1.0;

        // Too short (< 20 chars) or too long (> 500 chars) get penalty
        if (length < 20 || length > 500) return 0.7;

        // Moderately long messages (200-500 chars) get slight penalty
        return 0.9;
    }

    private double CalculateEnhancedSemanticSimilarity(List<string> words1, List<string> words2, string message1, string message2)
    {
        if (!words1.Any() || !words2.Any()) return 0.0;

        // Basic Jaccard similarity
        var jaccard = CalculateSimpleSemanticSimilarity(words1, words2);

        // Check for semantic relationships
        var semanticBoost = 0.0;

        // Synonyms and related terms
        var synonymGroups = new List<HashSet<string>>
        {
            new() { "room", "accommodation", "suite", "chamber" },
            new() { "clean", "tidy", "housekeeping", "maintenance" },
            new() { "food", "meal", "dining", "breakfast", "lunch", "dinner" },
            new() { "book", "reserve", "appointment", "schedule" },
            new() { "help", "assist", "support", "service" },
            new() { "problem", "issue", "trouble", "complaint", "broken" }
        };

        foreach (var group in synonymGroups)
        {
            var group1Match = words1.Any(w => group.Contains(w));
            var group2Match = words2.Any(w => group.Contains(w));
            if (group1Match && group2Match)
                semanticBoost += 0.2;
        }

        return jaccard + Math.Min(semanticBoost, 0.4);
    }

    private double CalculateIntentSimilarity(string message1, string message2)
    {
        var intents = new Dictionary<string, string[]>
        {
            { "request", new[] { "need", "want", "require", "please", "could", "can" } },
            { "complaint", new[] { "problem", "issue", "broken", "not working", "complaint" } },
            { "booking", new[] { "book", "reserve", "appointment", "schedule", "cancel" } },
            { "information", new[] { "what", "when", "where", "how", "tell me", "info" } }
        };

        var intent1 = DetectIntent(message1.ToLower(), intents);
        var intent2 = DetectIntent(message2.ToLower(), intents);

        return intent1 == intent2 && !string.IsNullOrEmpty(intent1) ? 0.5 : 0.0;
    }

    private string DetectIntent(string message, Dictionary<string, string[]> intents)
    {
        foreach (var intent in intents)
        {
            if (intent.Value.Any(keyword => message.Contains(keyword)))
                return intent.Key;
        }
        return string.Empty;
    }

    private double CalculateEntityOverlap(string message1, string message2)
    {
        var score = 0.0;

        // Room number detection
        var roomPattern = @"\b(room\s*)?(\d{3,4})\b";
        var rooms1 = Regex.Matches(message1, roomPattern, RegexOptions.IgnoreCase);
        var rooms2 = Regex.Matches(message2, roomPattern, RegexOptions.IgnoreCase);
        if (rooms1.Count > 0 && rooms2.Count > 0)
        {
            foreach (Match r1 in rooms1)
            foreach (Match r2 in rooms2)
                if (r1.Groups[2].Value == r2.Groups[2].Value)
                    score += 0.3;
        }

        // Time detection
        var timePattern = @"\b(\d{1,2}):?(\d{2})?\s*(am|pm)?\b|\b(morning|afternoon|evening|night)\b";
        var times1 = Regex.Matches(message1, timePattern, RegexOptions.IgnoreCase);
        var times2 = Regex.Matches(message2, timePattern, RegexOptions.IgnoreCase);
        if (times1.Count > 0 && times2.Count > 0)
            score += 0.2;

        // Phone number detection
        var phonePattern = @"\+?\d{10,15}";
        var phones1 = Regex.Matches(message1, phonePattern);
        var phones2 = Regex.Matches(message2, phonePattern);
        if (phones1.Count > 0 && phones2.Count > 0)
            score += 0.2;

        return Math.Min(score, 0.5);
    }

    private double CalculateBookingRelevance(Booking booking)
    {
        var today = DateTime.Today;
        var score = 0.5; // Base score

        // Active booking gets highest score
        if (booking.CheckInDate <= today && booking.CheckOutDate >= today)
        {
            score = 1.0;
        }
        // Future booking gets high score
        else if (booking.CheckInDate > today && booking.CheckInDate <= today.AddDays(7))
        {
            score = 0.8;
        }
        // Recent past booking gets medium score
        else if (booking.CheckOutDate >= today.AddDays(-7))
        {
            score = 0.6;
        }

        return score;
    }

    private double CalculateTaskRelevance(StaffTask task)
    {
        var score = 0.5; // Base score

        // Active tasks get higher score
        if (task.Status == "Pending") score = 0.9;
        else if (task.Status == "InProgress") score = 0.8;
        else if (task.Status == "Completed") score = 0.6;

        // High priority tasks get bonus
        if (task.Priority == "High") score += 0.2;
        else if (task.Priority == "Medium") score += 0.1;

        // Recent tasks get bonus
        var age = DateTime.UtcNow - task.CreatedAt;
        if (age.TotalDays < 1) score += 0.1;

        return Math.Min(score, 1.0);
    }
}