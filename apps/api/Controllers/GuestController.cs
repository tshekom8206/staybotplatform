using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Hostr.Api.Data;
using Hostr.Api.Models;
using Hostr.Api.Services;

namespace Hostr.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class GuestController : ControllerBase
{
    private readonly HostrDbContext _context;
    private readonly IWhatsAppService _whatsAppService;

    public GuestController(HostrDbContext context, IWhatsAppService whatsAppService)
    {
        _context = context;
        _whatsAppService = whatsAppService;
    }

    /// <summary>
    /// Get active guest conversations
    /// </summary>
    [HttpGet("conversations/active")]
    public async Task<IActionResult> GetActiveConversations()
    {
        try
        {
            var tenantId = HttpContext.Items["TenantId"] as int?;
            if (!tenantId.HasValue)
            {
                return BadRequest("Tenant context not found");
            }

            var activeConversations = await _context.Conversations
                .Where(c => c.TenantId == tenantId.Value &&
                           (c.Status == "Active" || c.Status == "Pending"))
                .Include(c => c.Messages
                    .OrderByDescending(m => m.CreatedAt)
                    .Take(1))
                .OrderByDescending(c => c.Messages.Max(m => m.CreatedAt))
                .Take(50) // Limit to recent conversations
                .ToListAsync();

            var guestConversations = new List<GuestConversationSummary>();

            foreach (var conversation in activeConversations)
            {
                var lastMessage = conversation.Messages.FirstOrDefault();
                if (lastMessage == null) continue;

                // Try to find most recent booking for room number and guest name
                var booking = await _context.Bookings
                    .Where(b => b.TenantId == tenantId.Value &&
                               b.Phone == conversation.WaUserPhone)
                    .OrderByDescending(b => b.CreatedAt)
                    .FirstOrDefaultAsync();

                var priority = DeterminePriority(lastMessage.Body, conversation.Status);

                guestConversations.Add(new GuestConversationSummary
                {
                    Id = conversation.Id,
                    GuestId = conversation.Id, // Using conversation ID as guest ID for now
                    GuestName = booking?.GuestName ?? "Guest",
                    PhoneNumber = conversation.WaUserPhone,
                    RoomNumber = booking?.RoomNumber,
                    LastMessage = lastMessage.Body ?? "",
                    LastMessageAt = lastMessage.CreatedAt,
                    Status = conversation.Status ?? "Active",
                    Priority = priority,
                    UnreadCount = await _context.Messages
                        .CountAsync(m => m.ConversationId == conversation.Id &&
                                        m.Direction == "Inbound"),
                    ConversationId = conversation.Id
                });
            }

            return Ok(guestConversations);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Failed to load conversations", error = ex.Message });
        }
    }

    /// <summary>
    /// Get guest details by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetGuestDetails(int id)
    {
        try
        {
            var tenantId = HttpContext.Items["TenantId"] as int?;
            if (!tenantId.HasValue)
            {
                return BadRequest("Tenant context not found");
            }

            var conversation = await _context.Conversations
                .Include(c => c.Messages.OrderByDescending(m => m.CreatedAt))
                .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId.Value);

            if (conversation == null)
            {
                return NotFound("Guest not found");
            }

            var booking = await _context.Bookings
                .Where(b => b.TenantId == tenantId.Value &&
                           b.Phone == conversation.WaUserPhone)
                .OrderByDescending(b => b.CreatedAt)
                .FirstOrDefaultAsync();

            var guestDetails = new
            {
                Id = conversation.Id,
                Name = booking?.GuestName ?? "Unknown Guest",
                PhoneNumber = conversation.WaUserPhone,
                RoomNumber = booking?.RoomNumber,
                CheckinDate = booking?.CheckinDate,
                CheckoutDate = booking?.CheckoutDate,
                BookingStatus = booking?.Status,
                ConversationStatus = conversation.Status,
                Messages = conversation.Messages.Take(50).Select(m => new
                {
                    m.Id,
                    m.Body,
                    m.Direction,
                    m.CreatedAt
                }).ToList() // Last 50 messages without circular reference
            };

            return Ok(guestDetails);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Failed to load guest details", error = ex.Message });
        }
    }

    /// <summary>
    /// Send message to guest
    /// </summary>
    [HttpPost("messages/send")]
    public async Task<IActionResult> SendMessage([FromBody] SendMessageRequest request)
    {
        try
        {
            var tenantId = HttpContext.Items["TenantId"] as int?;
            if (!tenantId.HasValue)
            {
                return BadRequest("Tenant context not found");
            }

            // Find or create conversation
            var conversation = await _context.Conversations
                .FirstOrDefaultAsync(c => c.TenantId == tenantId.Value &&
                                         c.WaUserPhone == request.PhoneNumber);

            if (conversation == null)
            {
                // Create new conversation if it doesn't exist
                conversation = new Conversation
                {
                    TenantId = tenantId.Value,
                    WaUserPhone = request.PhoneNumber,
                    Status = "Active",
                    CreatedAt = DateTime.UtcNow
                };
                _context.Conversations.Add(conversation);
                await _context.SaveChangesAsync();
            }

            // Send message via WhatsApp
            await _whatsAppService.SendTextMessageAsync(tenantId.Value, request.PhoneNumber, request.Message);

            // Save message to database
            var message = new Message
            {
                TenantId = tenantId.Value,
                ConversationId = conversation.Id,
                Body = request.Message,
                Direction = "Outbound",
                CreatedAt = DateTime.UtcNow
            };

            _context.Messages.Add(message);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Message sent successfully" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Failed to send message", error = ex.Message });
        }
    }

    /// <summary>
    /// Get guest interaction history
    /// </summary>
    [HttpGet("history")]
    public async Task<IActionResult> GetGuestHistory(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            var tenantId = HttpContext.Items["TenantId"] as int?;
            if (!tenantId.HasValue)
            {
                return BadRequest("Tenant context not found");
            }

            var skip = (page - 1) * pageSize;

            // Get recent conversations with their messages and associated bookings
            var conversations = await _context.Conversations
                .Where(c => c.TenantId == tenantId.Value)
                .Include(c => c.Messages.OrderByDescending(m => m.CreatedAt))
                .OrderByDescending(c => c.CreatedAt)
                .Skip(skip)
                .Take(pageSize)
                .ToListAsync();

            var guestHistory = new List<GuestHistorySummary>();

            foreach (var conversation in conversations)
            {
                // Prioritize the most recent inbound (guest) message for display
                var lastInboundMessage = conversation.Messages
                    .Where(m => m.Direction == "Inbound")
                    .OrderByDescending(m => m.CreatedAt)
                    .FirstOrDefault();

                // If no inbound message, fall back to most recent message
                var displayMessage = lastInboundMessage ?? conversation.Messages.FirstOrDefault();
                if (displayMessage == null) continue;

                // Try to find booking for this conversation
                var booking = await _context.Bookings
                    .Where(b => b.TenantId == tenantId.Value && b.Phone == conversation.WaUserPhone)
                    .OrderByDescending(b => b.CreatedAt)
                    .FirstOrDefaultAsync();

                // Determine interaction type based on message content
                var interactionType = DetermineInteractionType(displayMessage.Body);

                guestHistory.Add(new GuestHistorySummary
                {
                    Id = conversation.Id,
                    Date = displayMessage.CreatedAt, // Use the message date, not conversation date
                    GuestName = booking?.GuestName ?? "Unknown Guest",
                    RoomNumber = booking?.RoomNumber ?? "N/A",
                    InteractionType = interactionType,
                    StaffMember = displayMessage.Direction == "Inbound" ? "Guest" : "AI Assistant",
                    Summary = TruncateText(displayMessage.Body, 100),
                    Status = conversation.Status == "Active" ? "pending" : "resolved",
                    Duration = null // Could calculate based on conversation length
                });
            }

            return Ok(guestHistory);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Failed to load guest history", error = ex.Message });
        }
    }

    /// <summary>
    /// Update conversation status
    /// </summary>
    [HttpPatch("conversations/{id}/status")]
    public async Task<IActionResult> UpdateConversationStatus(int id, [FromBody] UpdateConversationStatusRequest request)
    {
        try
        {
            var tenantId = HttpContext.Items["TenantId"] as int?;
            if (!tenantId.HasValue)
            {
                return BadRequest("Tenant context not found");
            }

            var conversation = await _context.Conversations
                .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId.Value);

            if (conversation == null)
            {
                return NotFound("Conversation not found");
            }

            conversation.Status = request.Status;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Conversation status updated successfully" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Failed to update conversation status", error = ex.Message });
        }
    }

    private static string DeterminePriority(string? messageContent, string? status)
    {
        if (string.IsNullOrEmpty(messageContent))
            return "low";

        var content = messageContent.ToLower();

        if (content.Contains("urgent") || content.Contains("emergency") || content.Contains("help") ||
            content.Contains("broken") || content.Contains("leak") || content.Contains("fire"))
            return "urgent";

        if (content.Contains("problem") || content.Contains("issue") || content.Contains("complaint") ||
            content.Contains("not working") || content.Contains("doesn't work"))
            return "high";

        if (content.Contains("request") || content.Contains("need") || content.Contains("want"))
            return "medium";

        return "low";
    }

    private static string DetermineInteractionType(string messageContent)
    {
        if (string.IsNullOrEmpty(messageContent))
            return "chat";

        var content = messageContent.ToLower();

        if (content.Contains("emergency") || content.Contains("urgent") || content.Contains("help"))
            return "emergency";

        if (content.Contains("complaint") || content.Contains("problem") || content.Contains("issue"))
            return "complaint";

        if (content.Contains("request") || content.Contains("need") || content.Contains("want") || content.Contains("book"))
            return "request";

        if (content.Contains("task") || content.Contains("maintenance") || content.Contains("clean"))
            return "task";

        return "chat";
    }

    private static string TruncateText(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text;

        return text.Substring(0, maxLength - 3) + "...";
    }
}

public class SendMessageRequest
{
    public string PhoneNumber { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? GuestName { get; set; }
    public int? ConversationId { get; set; }
}

public class UpdateConversationStatusRequest
{
    public string Status { get; set; } = string.Empty;
}

public class GuestConversationSummary
{
    public int Id { get; set; }
    public int GuestId { get; set; }
    public string GuestName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? RoomNumber { get; set; }
    public string LastMessage { get; set; } = string.Empty;
    public DateTime LastMessageAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public int UnreadCount { get; set; }
    public int ConversationId { get; set; }
}

public class GuestHistorySummary
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
    public string GuestName { get; set; } = string.Empty;
    public string RoomNumber { get; set; } = string.Empty;
    public string InteractionType { get; set; } = string.Empty; // 'chat' | 'task' | 'emergency' | 'complaint' | 'request'
    public string StaffMember { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty; // 'resolved' | 'pending' | 'escalated'
    public string? Duration { get; set; }
}