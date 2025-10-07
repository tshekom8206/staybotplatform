using Microsoft.EntityFrameworkCore;
using System.Text;
using Hostr.Api.Data;
using Hostr.Api.Models;

namespace Hostr.Api.Services;

public interface IConversationHandoffService
{
    Task<ConversationHandoffContext> PrepareHandoffContextAsync(int conversationId, TransferReason reason);
    Task<bool> NotifyAgentOfTransferAsync(int agentId, ConversationHandoffContext context);
    Task<string> GenerateHandoffSummaryAsync(ConversationHandoffContext context);
}

public class ConversationHandoffContext
{
    public int ConversationId { get; set; }
    public string GuestPhone { get; set; } = "";
    public string GuestName { get; set; } = "";
    public string? RoomNumber { get; set; }
    public TransferReason TransferReason { get; set; }
    public TransferPriority Priority { get; set; }
    public string TriggerMessage { get; set; } = "";
    public List<ConversationMessage> RecentMessages { get; set; } = new();
    public List<string> ActiveTasks { get; set; } = new();
    public List<string> OpenIssues { get; set; } = new();
    public GuestContextSummary GuestContext { get; set; } = new();
    public DateTime TransferRequestedAt { get; set; } = DateTime.UtcNow;
    public string HandoffSummary { get; set; } = "";
}

public class ConversationMessage
{
    public DateTime Timestamp { get; set; }
    public string Speaker { get; set; } = ""; // "Guest" or "Bot"
    public string Message { get; set; } = "";
    public bool IsTransferTrigger { get; set; }
}

public class GuestContextSummary
{
    public string CheckInStatus { get; set; } = "";
    public DateTime? CheckInDate { get; set; }
    public DateTime? CheckOutDate { get; set; }
    public List<string> PreviousRequests { get; set; } = new();
    public List<string> CompletedTasks { get; set; } = new();
    public string GuestType { get; set; } = ""; // "New", "Returning", "VIP", etc.
    public int ConversationDurationMinutes { get; set; }
    public bool HasUnresolvedIssues { get; set; }
}

public class ConversationHandoffService : IConversationHandoffService
{
    private readonly HostrDbContext _context;
    private readonly IOpenAIService _openAIService;
    private readonly INotificationService _notificationService;
    private readonly ILogger<ConversationHandoffService> _logger;

    public ConversationHandoffService(
        HostrDbContext context,
        IOpenAIService openAIService,
        INotificationService notificationService,
        ILogger<ConversationHandoffService> logger)
    {
        _context = context;
        _openAIService = openAIService;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<ConversationHandoffContext> PrepareHandoffContextAsync(int conversationId, TransferReason reason)
    {
        try
        {
            var conversation = await _context.Conversations
                .Include(c => c.Messages)
                .FirstOrDefaultAsync(c => c.Id == conversationId);

            if (conversation == null)
            {
                throw new ArgumentException($"Conversation {conversationId} not found");
            }

            var context = new ConversationHandoffContext
            {
                ConversationId = conversationId,
                GuestPhone = conversation.WaUserPhone,
                TransferReason = reason,
                Priority = GetPriorityForReason(reason),
                TransferRequestedAt = DateTime.UtcNow
            };

            // Get guest information from booking
            var booking = await _context.Bookings
                .Where(b => b.Phone == conversation.WaUserPhone && b.TenantId == conversation.TenantId)
                .OrderByDescending(b => b.CreatedAt)
                .FirstOrDefaultAsync();

            if (booking != null)
            {
                context.GuestName = booking.GuestName;
                context.RoomNumber = booking.RoomNumber;
                context.GuestContext.CheckInDate = booking.CheckInDate ?? booking.CheckinDate.ToDateTime(TimeOnly.MinValue);
                context.GuestContext.CheckOutDate = booking.CheckOutDate ?? booking.CheckoutDate.ToDateTime(TimeOnly.MinValue);
                context.GuestContext.CheckInStatus = booking.Status;
                context.GuestContext.GuestType = booking.IsRepeatGuest ? "Returning Guest" : "New Guest";
            }

            // Get recent conversation messages
            var messages = await _context.Messages
                .Where(m => m.ConversationId == conversationId)
                .OrderByDescending(m => m.CreatedAt)
                .Take(20)
                .OrderBy(m => m.CreatedAt)
                .ToListAsync();

            context.RecentMessages = messages.Select(m => new ConversationMessage
            {
                Timestamp = m.CreatedAt,
                Speaker = m.Direction == "Inbound" ? "Guest" : "Bot",
                Message = m.Body,
                IsTransferTrigger = m == messages.LastOrDefault() && reason == TransferReason.UserRequested
            }).ToList();

            // Set trigger message
            var lastGuestMessage = messages.LastOrDefault(m => m.Direction == "Inbound");
            if (lastGuestMessage != null)
            {
                context.TriggerMessage = lastGuestMessage.Body;
            }

            // Get conversation duration
            if (messages.Any())
            {
                var duration = messages.Last().CreatedAt - messages.First().CreatedAt;
                context.GuestContext.ConversationDurationMinutes = (int)duration.TotalMinutes;
            }

            // Get active tasks for this guest
            var activeTasks = await _context.StaffTasks
                .Where(t => t.ConversationId == conversationId &&
                           (t.Status == "Open" || t.Status == "InProgress"))
                .Include(t => t.RequestItem)
                .ToListAsync();

            context.ActiveTasks = activeTasks.Select(t =>
                $"{t.RequestItem?.Name ?? "Task"}: {t.Description} (Status: {t.Status})").ToList();

            // Get completed tasks for context
            var completedTasks = await _context.StaffTasks
                .Where(t => t.ConversationId == conversationId && t.Status == "Completed")
                .Include(t => t.RequestItem)
                .OrderByDescending(t => t.CompletedAt)
                .Take(5)
                .ToListAsync();

            context.GuestContext.CompletedTasks = completedTasks.Select(t =>
                $"{t.RequestItem?.Name ?? "Task"}: {t.Description}").ToList();

            // Identify open issues from conversation
            context.OpenIssues = await IdentifyOpenIssuesAsync(messages);
            context.GuestContext.HasUnresolvedIssues = context.OpenIssues.Any() || context.ActiveTasks.Any();

            // Generate AI-powered handoff summary
            context.HandoffSummary = await GenerateHandoffSummaryAsync(context);

            _logger.LogInformation("Prepared handoff context for conversation {ConversationId} - Guest: {GuestName}, Room: {RoomNumber}, Duration: {Duration}min",
                conversationId, context.GuestName, context.RoomNumber, context.GuestContext.ConversationDurationMinutes);

            return context;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error preparing handoff context for conversation {ConversationId}", conversationId);
            throw;
        }
    }

    public async Task<string> GenerateHandoffSummaryAsync(ConversationHandoffContext context)
    {
        try
        {
            var prompt = BuildHandoffSummaryPrompt(context);
            var summary = await _openAIService.GetStructuredResponseAsync<HandoffSummaryResponse>(prompt);

            return summary?.Summary ?? GenerateFallbackSummary(context);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate AI summary for conversation {ConversationId}, using fallback", context.ConversationId);
            return GenerateFallbackSummary(context);
        }
    }

    public async Task<bool> NotifyAgentOfTransferAsync(int agentId, ConversationHandoffContext context)
    {
        try
        {
            var handoffMessage = $@"🔔 **New Conversation Transfer**

**Guest Information:**
- Name: {context.GuestName ?? "Not available"}
- Phone: {context.GuestPhone}
- Room: {context.RoomNumber ?? "Not available"}
- Guest Type: {context.GuestContext.GuestType}

**Transfer Details:**
- Reason: {context.TransferReason}
- Priority: {context.Priority}
- Conversation Duration: {context.GuestContext.ConversationDurationMinutes} minutes

**Current Issues:**
{(context.OpenIssues.Any() ? string.Join("\n", context.OpenIssues.Select(i => $"• {i}")) : "No open issues identified")}

**Active Tasks:**
{(context.ActiveTasks.Any() ? string.Join("\n", context.ActiveTasks.Select(t => $"• {t}")) : "No active tasks")}

**Conversation Summary:**
{context.HandoffSummary}

**Recent Messages:**
{string.Join("\n", context.RecentMessages.TakeLast(5).Select(m =>
    $"{m.Speaker} ({m.Timestamp:HH:mm}): {m.Message}"))}

Click to view full conversation history and take over this chat.";

            await _notificationService.NotifyAgentAssignmentAsync(
                agentId,
                context.ConversationId,
                handoffMessage);

            _logger.LogInformation("Sent handoff notification to agent {AgentId} for conversation {ConversationId}",
                agentId, context.ConversationId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to notify agent {AgentId} of transfer for conversation {ConversationId}",
                agentId, context.ConversationId);
            return false;
        }
    }

    private string BuildHandoffSummaryPrompt(ConversationHandoffContext context)
    {
        var promptBuilder = new StringBuilder();

        promptBuilder.AppendLine("You are a hotel concierge assistant helping with a conversation handoff.");
        promptBuilder.AppendLine("Analyze the conversation below and create a concise summary for the human agent taking over.");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("**GUEST INFORMATION:**");
        promptBuilder.AppendLine($"Name: {context.GuestName ?? "Unknown"}");
        promptBuilder.AppendLine($"Room: {context.RoomNumber ?? "Unknown"}");
        promptBuilder.AppendLine($"Phone: {context.GuestPhone}");
        promptBuilder.AppendLine($"Stay: {context.GuestContext.CheckInDate?.ToString("MMM dd")} - {context.GuestContext.CheckOutDate?.ToString("MMM dd")}");
        promptBuilder.AppendLine();

        promptBuilder.AppendLine("**CONVERSATION HISTORY:**");
        foreach (var message in context.RecentMessages)
        {
            promptBuilder.AppendLine($"{message.Speaker} ({message.Timestamp:HH:mm}): {message.Message}");
        }
        promptBuilder.AppendLine();

        if (context.ActiveTasks.Any())
        {
            promptBuilder.AppendLine("**ACTIVE TASKS:**");
            foreach (var task in context.ActiveTasks)
            {
                promptBuilder.AppendLine($"• {task}");
            }
            promptBuilder.AppendLine();
        }

        promptBuilder.AppendLine($"**TRANSFER REASON:** {context.TransferReason}");
        promptBuilder.AppendLine($"**TRIGGER MESSAGE:** {context.TriggerMessage}");
        promptBuilder.AppendLine();

        promptBuilder.AppendLine("Create a handoff summary that includes:");
        promptBuilder.AppendLine("1. What the guest needs/wants");
        promptBuilder.AppendLine("2. Key conversation context");
        promptBuilder.AppendLine("3. Any urgent issues or concerns");
        promptBuilder.AppendLine("4. Recommended next steps");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("Keep it concise but comprehensive. The agent should understand the situation immediately.");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("Respond with JSON in this format:");
        promptBuilder.AppendLine("{");
        promptBuilder.AppendLine("  \"summary\": \"Your handoff summary here\"");
        promptBuilder.AppendLine("}");

        return promptBuilder.ToString();
    }

    private string GenerateFallbackSummary(ConversationHandoffContext context)
    {
        var summary = new StringBuilder();

        summary.AppendLine($"Guest {context.GuestName ?? context.GuestPhone} has requested human assistance.");

        if (!string.IsNullOrEmpty(context.TriggerMessage))
        {
            summary.AppendLine($"Latest message: \"{context.TriggerMessage}\"");
        }

        if (context.ActiveTasks.Any())
        {
            summary.AppendLine($"Active requests: {string.Join(", ", context.ActiveTasks.Take(3))}");
        }

        if (context.OpenIssues.Any())
        {
            summary.AppendLine($"Open issues: {string.Join(", ", context.OpenIssues.Take(2))}");
        }

        summary.AppendLine($"Conversation started {context.GuestContext.ConversationDurationMinutes} minutes ago.");
        summary.AppendLine("Please review the conversation history to understand the full context.");

        return summary.ToString();
    }

    private async Task<List<string>> IdentifyOpenIssuesAsync(List<Message> messages)
    {
        var issues = new List<string>();

        // Look for unresolved issues in recent messages
        var guestMessages = messages
            .Where(m => m.Direction == "Inbound")
            .TakeLast(10)
            .ToList();

        foreach (var message in guestMessages)
        {
            var text = message.Body.ToLower();

            // Common issue patterns
            if (text.Contains("not working") || text.Contains("broken") || text.Contains("problem"))
            {
                issues.Add($"Technical issue: {message.Body}");
            }
            else if (text.Contains("still waiting") || text.Contains("haven't received"))
            {
                issues.Add($"Waiting for service: {message.Body}");
            }
            else if (text.Contains("disappointed") || text.Contains("unhappy") || text.Contains("complaint"))
            {
                issues.Add($"Service concern: {message.Body}");
            }
            else if (text.Contains("urgent") || text.Contains("asap") || text.Contains("immediately"))
            {
                issues.Add($"Urgent request: {message.Body}");
            }
        }

        return issues.Distinct().Take(3).ToList();
    }

    private TransferPriority GetPriorityForReason(TransferReason reason)
    {
        return reason switch
        {
            TransferReason.EmergencyHandoff => TransferPriority.Emergency,
            TransferReason.QualityAssurance => TransferPriority.Urgent,
            TransferReason.ComplexityLimit => TransferPriority.High,
            TransferReason.SpecialistRequired => TransferPriority.High,
            TransferReason.SystemEscalation => TransferPriority.Normal,
            TransferReason.UserRequested => TransferPriority.Normal,
            _ => TransferPriority.Normal
        };
    }
}

public class HandoffSummaryResponse
{
    public string Summary { get; set; } = "";
}