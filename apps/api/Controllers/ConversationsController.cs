using Hostr.Api.Data;
using Hostr.Api.Models;
using Hostr.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;

namespace Hostr.Api.Controllers;

[ApiController]
[Route("api/conversations")]
public class ConversationsController : ControllerBase
{
    private readonly HostrDbContext _context;
    private readonly ILogger<ConversationsController> _logger;
    private readonly IPushNotificationService _pushNotificationService;

    public ConversationsController(
        HostrDbContext context,
        ILogger<ConversationsController> logger,
        IPushNotificationService pushNotificationService)
    {
        _context = context;
        _logger = logger;
        _pushNotificationService = pushNotificationService;
    }

    /// <summary>
    /// Get all active conversations for the current tenant
    /// </summary>
    [HttpGet("active")]
    public async Task<IActionResult> GetActiveConversations()
    {
        try
        {
            var tenantId = HttpContext.Items["TenantId"] as int? ?? 0;
            if (tenantId == 0)
            {
                return BadRequest(new { error = "Tenant context not available" });
            }

            var activeConversations = await _context.Conversations
                .Where(c => c.TenantId == tenantId &&
                           (c.Status == "Active" || c.Status == "TransferredToAgent"))
                .Include(c => c.Messages.OrderByDescending(m => m.CreatedAt).Take(1))
                .Include(c => c.AssignedAgent)
                .GroupJoin(
                    _context.Bookings.Where(b => b.TenantId == tenantId && b.Status == "CheckedIn"),
                    c => c.WaUserPhone,
                    b => b.Phone,
                    (c, bookings) => new { Conversation = c, Booking = bookings.FirstOrDefault() }
                )
                .OrderByDescending(x => x.Conversation.CreatedAt)
                .Select(x => new
                {
                    x.Conversation.Id,
                    GuestPhone = x.Conversation.WaUserPhone,
                    x.Conversation.Status,
                    x.Conversation.CreatedAt,
                    LastMessageTime = x.Conversation.Messages.OrderByDescending(m => m.CreatedAt).Select(m => m.CreatedAt).FirstOrDefault(),
                    x.Conversation.AssignedAgentId,
                    AssignedAgent = x.Conversation.AssignedAgent != null ? x.Conversation.AssignedAgent.UserName : null,
                    x.Conversation.TransferReason,
                    x.Conversation.TransferredAt,
                    LastMessage = x.Conversation.Messages.OrderByDescending(m => m.CreatedAt).Select(m => m.Body).FirstOrDefault() ?? "",
                    MessageCount = x.Conversation.Messages.Count,
                    RoomNumber = x.Booking != null ? x.Booking.RoomNumber : null,
                    GuestName = x.Booking != null ? x.Booking.GuestName : null,
                    Priority = "Normal",
                    IsTransferRequested = x.Conversation.Status == "Handover" || x.Conversation.Status == "TransferredToAgent",
                    TenantId = x.Conversation.TenantId
                })
                .ToListAsync();

            return Ok(activeConversations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active conversations");
            return StatusCode(500, new { error = "Internal server error", message = ex.Message });
        }
    }

    /// <summary>
    /// Get conversation details (metadata only)
    /// </summary>
    [HttpGet("{id}/details")]
    public async Task<IActionResult> GetConversationDetails(int id)
    {
        try
        {
            var tenantId = HttpContext.Items["TenantId"] as int? ?? 0;
            if (tenantId == 0)
            {
                return BadRequest(new { error = "Tenant context not available" });
            }

            var conversation = await _context.Conversations
                .Where(c => c.Id == id && c.TenantId == tenantId)
                .Include(c => c.AssignedAgent)
                .GroupJoin(
                    _context.Bookings.Where(b => b.TenantId == tenantId && b.Status == "CheckedIn"),
                    c => c.WaUserPhone,
                    b => b.Phone,
                    (c, bookings) => new { Conversation = c, Booking = bookings.FirstOrDefault() }
                )
                .Select(x => new
                {
                    x.Conversation.Id,
                    GuestPhone = x.Conversation.WaUserPhone,
                    GuestName = x.Booking != null ? x.Booking.GuestName : null,
                    RoomNumber = x.Booking != null ? x.Booking.RoomNumber : null,
                    x.Conversation.Status,
                    x.Conversation.CreatedAt,
                    EndedAt = x.Conversation.Status == "Closed" ? x.Conversation.TransferCompletedAt : (DateTime?)null,
                    AssignedAgent = x.Conversation.AssignedAgent != null ? x.Conversation.AssignedAgent.UserName : null,
                    Priority = "Normal",
                    MessageCount = x.Conversation.Messages.Count,
                    LastActivity = x.Conversation.Messages.OrderByDescending(m => m.CreatedAt).Select(m => m.CreatedAt).FirstOrDefault(),
                    TransferHistory = new object[] { },
                    AssignmentHistory = new object[] { }
                })
                .FirstOrDefaultAsync();

            if (conversation == null)
            {
                return NotFound(new { error = "Conversation not found" });
            }

            return Ok(conversation);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving conversation details {ConversationId}", id);
            return StatusCode(500, new { error = "Internal server error", message = ex.Message });
        }
    }

    /// <summary>
    /// Get conversation messages
    /// </summary>
    [HttpGet("{id}/messages")]
    public async Task<IActionResult> GetConversationMessages(int id)
    {
        try
        {
            var tenantId = HttpContext.Items["TenantId"] as int? ?? 0;
            if (tenantId == 0)
            {
                return BadRequest(new { error = "Tenant context not available" });
            }

            var messages = await _context.Messages
                .Where(m => m.ConversationId == id && m.TenantId == tenantId)
                .OrderBy(m => m.CreatedAt)
                .Select(m => new
                {
                    m.Id,
                    ConversationId = m.ConversationId,
                    MessageText = m.Body,
                    SenderType = m.Direction == "Inbound" ? "Guest" : "Bot",
                    SenderName = m.Direction == "Inbound" ? "Guest" : "Bot",
                    Timestamp = m.CreatedAt,
                    IsSystemMessage = false
                })
                .ToListAsync();

            return Ok(messages);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving messages for conversation {ConversationId}", id);
            return StatusCode(500, new { error = "Internal server error", message = ex.Message });
        }
    }

    /// <summary>
    /// Assign agent to conversation
    /// </summary>
    [HttpPost("{id}/assign-agent")]
    public async Task<IActionResult> AssignAgentToConversation(int id, [FromBody] AssignAgentRequest request)
    {
        try
        {
            var tenantId = HttpContext.Items["TenantId"] as int? ?? 0;
            if (tenantId == 0)
            {
                return BadRequest(new { error = "Tenant context not available" });
            }

            var conversation = await _context.Conversations
                .Include(c => c.Messages.OrderByDescending(m => m.CreatedAt).Take(1))
                .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId);

            if (conversation == null)
            {
                return NotFound(new { error = "Conversation not found" });
            }

            // Get agent details from Users table (with Agent role)
            var agent = await _context.UserTenants
                .Where(ut => ut.UserId == request.AgentId && ut.TenantId == tenantId && ut.Role == "Agent")
                .Join(_context.Users,
                    ut => ut.UserId,
                    u => u.Id,
                    (ut, u) => new { u.Id, Name = u.UserName, u.Email })
                .FirstOrDefaultAsync();

            if (agent == null)
            {
                return NotFound(new { error = "Agent not found" });
            }

            conversation.AssignedAgentId = request.AgentId;
            conversation.Status = "TransferredToAgent";
            conversation.TransferredAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // Send push notification to the assigned agent
            try
            {
                await _pushNotificationService.NotifyConversationAssigned(
                    request.AgentId,
                    id,
                    conversation.WaUserPhone
                );

                _logger.LogInformation("Push notification sent to agent {AgentId} for conversation {ConversationId}", request.AgentId, id);
            }
            catch (Exception notifEx)
            {
                _logger.LogWarning(notifEx, "Failed to send push notification to agent {AgentId} for conversation {ConversationId}", request.AgentId, id);
                // Don't fail the assignment if notification fails
            }

            return Ok(new { message = "Agent assigned successfully", agentName = agent.Name });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning agent to conversation {ConversationId}", id);
            return StatusCode(500, new { error = "Internal server error", message = ex.Message });
        }
    }

    /// <summary>
    /// Release agent from conversation
    /// </summary>
    [HttpPost("{id}/release-agent")]
    public async Task<IActionResult> ReleaseAgentFromConversation(int id)
    {
        try
        {
            var tenantId = HttpContext.Items["TenantId"] as int? ?? 0;
            if (tenantId == 0)
            {
                return BadRequest(new { error = "Tenant context not available" });
            }

            var conversation = await _context.Conversations
                .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId);

            if (conversation == null)
            {
                return NotFound(new { error = "Conversation not found" });
            }

            conversation.AssignedAgentId = null;
            conversation.Status = "Active";
            conversation.TransferCompletedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Agent released successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error releasing agent from conversation {ConversationId}", id);
            return StatusCode(500, new { error = "Internal server error", message = ex.Message });
        }
    }

    /// <summary>
    /// Get a specific conversation by ID with all messages
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetConversation(int id)
    {
        try
        {
            var tenantId = HttpContext.Items["TenantId"] as int? ?? 0;
            if (tenantId == 0)
            {
                return BadRequest(new { error = "Tenant context not available" });
            }

            var conversation = await _context.Conversations
                .Where(c => c.Id == id && c.TenantId == tenantId)
                .Include(c => c.Messages.OrderBy(m => m.CreatedAt))
                .Include(c => c.AssignedAgent)
                .Include(c => c.StaffTasks)
                .FirstOrDefaultAsync();

            if (conversation == null)
            {
                return NotFound(new { error = "Conversation not found" });
            }

            return Ok(new
            {
                conversation.Id,
                conversation.WaUserPhone,
                conversation.Status,
                conversation.CreatedAt,
                conversation.LastBotReplyAt,
                conversation.AssignedAgentId,
                AssignedAgentName = conversation.AssignedAgent?.UserName,
                conversation.TransferReason,
                conversation.TransferredAt,
                conversation.TransferCompletedAt,
                conversation.TransferSummary,
                Messages = conversation.Messages.Select(m => new
                {
                    m.Id,
                    m.Direction,
                    m.MessageType,
                    m.Body,
                    m.Model,
                    m.UsedRag,
                    m.CreatedAt
                }),
                Tasks = conversation.StaffTasks.Select(t => new
                {
                    t.Id,
                    t.Department,
                    t.Status,
                    t.Priority,
                    t.Description,
                    t.CreatedAt
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving conversation {ConversationId}", id);
            return StatusCode(500, new { error = "Internal server error", message = ex.Message });
        }
    }

    /// <summary>
    /// Get conversations by phone number
    /// </summary>
    [HttpGet("phone/{phoneNumber}")]
    public async Task<IActionResult> GetConversationsByPhone(string phoneNumber)
    {
        try
        {
            var tenantId = HttpContext.Items["TenantId"] as int? ?? 0;
            if (tenantId == 0)
            {
                return BadRequest(new { error = "Tenant context not available" });
            }

            var conversations = await _context.Conversations
                .Where(c => c.TenantId == tenantId && c.WaUserPhone == phoneNumber)
                .Include(c => c.Messages.OrderByDescending(m => m.CreatedAt).Take(1))
                .Include(c => c.AssignedAgent)
                .GroupJoin(
                    _context.Bookings.Where(b => b.TenantId == tenantId && b.Status == "CheckedIn"),
                    c => c.WaUserPhone,
                    b => b.Phone,
                    (c, bookings) => new { Conversation = c, Booking = bookings.FirstOrDefault() }
                )
                .OrderByDescending(x => x.Conversation.CreatedAt)
                .Select(x => new
                {
                    x.Conversation.Id,
                    GuestPhone = x.Conversation.WaUserPhone,
                    x.Conversation.Status,
                    x.Conversation.CreatedAt,
                    LastMessageTime = x.Conversation.Messages.OrderByDescending(m => m.CreatedAt).Select(m => m.CreatedAt).FirstOrDefault(),
                    x.Conversation.AssignedAgentId,
                    AssignedAgent = x.Conversation.AssignedAgent != null ? x.Conversation.AssignedAgent.UserName : null,
                    LastMessage = x.Conversation.Messages.OrderByDescending(m => m.CreatedAt).Select(m => m.Body).FirstOrDefault() ?? "",
                    MessageCount = x.Conversation.Messages.Count,
                    RoomNumber = x.Booking != null ? x.Booking.RoomNumber : null,
                    GuestName = x.Booking != null ? x.Booking.GuestName : null
                })
                .ToListAsync();

            return Ok(conversations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving conversations for phone {PhoneNumber}", phoneNumber);
            return StatusCode(500, new { error = "Internal server error", message = ex.Message });
        }
    }

    /// <summary>
    /// Get all conversations with optional filtering
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAllConversations(
        [FromQuery] string? status = null,
        [FromQuery] int? agentId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var tenantId = HttpContext.Items["TenantId"] as int? ?? 0;
            if (tenantId == 0)
            {
                return BadRequest(new { error = "Tenant context not available" });
            }

            var query = _context.Conversations
                .Where(c => c.TenantId == tenantId);

            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(c => c.Status == status);
            }

            if (agentId.HasValue)
            {
                query = query.Where(c => c.AssignedAgentId == agentId.Value);
            }

            var totalCount = await query.CountAsync();

            var conversations = await query
                .Include(c => c.Messages.OrderByDescending(m => m.CreatedAt).Take(1))
                .Include(c => c.AssignedAgent)
                .GroupJoin(
                    _context.Bookings.Where(b => b.TenantId == tenantId && b.Status == "CheckedIn"),
                    c => c.WaUserPhone,
                    b => b.Phone,
                    (c, bookings) => new { Conversation = c, Booking = bookings.FirstOrDefault() }
                )
                .OrderByDescending(x => x.Conversation.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new
                {
                    x.Conversation.Id,
                    GuestPhone = x.Conversation.WaUserPhone,
                    x.Conversation.Status,
                    x.Conversation.CreatedAt,
                    LastMessageTime = x.Conversation.Messages.OrderByDescending(m => m.CreatedAt).Select(m => m.CreatedAt).FirstOrDefault(),
                    x.Conversation.AssignedAgentId,
                    AssignedAgent = x.Conversation.AssignedAgent != null ? x.Conversation.AssignedAgent.UserName : null,
                    x.Conversation.TransferReason,
                    x.Conversation.TransferredAt,
                    LastMessage = x.Conversation.Messages.OrderByDescending(m => m.CreatedAt).Select(m => m.Body).FirstOrDefault() ?? "",
                    MessageCount = x.Conversation.Messages.Count,
                    RoomNumber = x.Booking != null ? x.Booking.RoomNumber : null,
                    GuestName = x.Booking != null ? x.Booking.GuestName : null
                })
                .ToListAsync();

            return Ok(new
            {
                totalCount,
                page,
                pageSize,
                totalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                conversations
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving conversations");
            return StatusCode(500, new { error = "Internal server error", message = ex.Message });
        }
    }

    /// <summary>
    /// Update conversation status
    /// </summary>
    [HttpPatch("{id}/status")]
    public async Task<IActionResult> UpdateConversationStatus(int id, [FromBody] UpdateStatusRequest request)
    {
        try
        {
            var tenantId = HttpContext.Items["TenantId"] as int? ?? 0;
            if (tenantId == 0)
            {
                return BadRequest(new { error = "Tenant context not available" });
            }

            var conversation = await _context.Conversations
                .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId);

            if (conversation == null)
            {
                return NotFound(new { error = "Conversation not found" });
            }

            conversation.Status = request.Status;

            if (request.Status == "Closed" && conversation.TransferredAt.HasValue && !conversation.TransferCompletedAt.HasValue)
            {
                conversation.TransferCompletedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            return Ok(new { message = "Conversation status updated", conversation });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating conversation status for {ConversationId}", id);
            return StatusCode(500, new { error = "Internal server error", message = ex.Message });
        }
    }

    /// <summary>
    /// Get conversation statistics
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetConversationStats()
    {
        try
        {
            var tenantId = HttpContext.Items["TenantId"] as int? ?? 0;
            if (tenantId == 0)
            {
                return BadRequest(new { error = "Tenant context not available" });
            }

            var stats = await _context.Conversations
                .Where(c => c.TenantId == tenantId)
                .GroupBy(c => 1)
                .Select(g => new
                {
                    Total = g.Count(),
                    Active = g.Count(c => c.Status == "Active"),
                    TransferredToAgent = g.Count(c => c.Status == "TransferredToAgent"),
                    Closed = g.Count(c => c.Status == "Closed"),
                    Handover = g.Count(c => c.Status == "Handover"),
                    AvgMessagesPerConversation = g.Average(c => c.Messages.Count)
                })
                .FirstOrDefaultAsync();

            return Ok(stats ?? new
            {
                Total = 0,
                Active = 0,
                TransferredToAgent = 0,
                Closed = 0,
                Handover = 0,
                AvgMessagesPerConversation = 0.0
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving conversation statistics");
            return StatusCode(500, new { error = "Internal server error", message = ex.Message });
        }
    }

    /// <summary>
    /// Get conversation statistics (alias endpoint)
    /// </summary>
    [HttpGet("statistics")]
    public async Task<IActionResult> GetConversationStatistics()
    {
        try
        {
            var tenantId = HttpContext.Items["TenantId"] as int? ?? 0;
            if (tenantId == 0)
            {
                return BadRequest(new { error = "Tenant context not available" });
            }

            var conversations = await _context.Conversations
                .Where(c => c.TenantId == tenantId)
                .Include(c => c.Messages)
                .ToListAsync();

            var totalActive = conversations.Count(c => c.Status == "Active" || c.Status == "TransferredToAgent");
            var transferRequests = conversations.Count(c => c.Status == "TransferredToAgent" || c.Status == "Handover");
            var assigned = conversations.Count(c => c.AssignedAgentId != null);

            // Count emergency conversations (high priority or emergency status)
            var emergency = conversations.Count(c =>
                c.Messages.Any(m => m.Body.ToLower().Contains("emergency") ||
                                   m.Body.ToLower().Contains("urgent") ||
                                   m.Body.ToLower().Contains("help")));

            var stats = new
            {
                totalActive,
                transferRequests,
                assigned,
                emergency,
                avgResponseTime = "2m 30s" // Placeholder - implement actual calculation if needed
            };

            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving conversation statistics");
            return StatusCode(500, new { error = "Internal server error", message = ex.Message });
        }
    }

    /// <summary>
    /// Get conversation history with optional filters and pagination
    /// </summary>
    [HttpGet("history")]
    public async Task<IActionResult> GetConversationHistory(
        [FromQuery] string? status,
        [FromQuery] string? transferFilter,
        [FromQuery] string? period,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] int? agentId,
        [FromQuery] int? satisfaction,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25)
    {
        try
        {
            var tenantId = HttpContext.Items["TenantId"] as int? ?? 0;
            if (tenantId == 0)
            {
                return BadRequest(new { error = "Tenant context not available" });
            }

            var query = _context.Conversations
                .Where(c => c.TenantId == tenantId)
                .Include(c => c.Messages)
                .Include(c => c.AssignedAgent)
                .AsQueryable();

            // Apply filters
            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(c => c.Status == status);
            }

            if (!string.IsNullOrEmpty(transferFilter))
            {
                if (transferFilter == "transferred")
                    query = query.Where(c => c.TransferredAt != null);
                else if (transferFilter == "not_transferred")
                    query = query.Where(c => c.TransferredAt == null);
            }

            if (dateFrom.HasValue)
            {
                query = query.Where(c => c.CreatedAt >= dateFrom.Value);
            }

            if (dateTo.HasValue)
            {
                query = query.Where(c => c.CreatedAt <= dateTo.Value);
            }

            if (agentId.HasValue)
            {
                query = query.Where(c => c.AssignedAgentId == agentId.Value);
            }

            // Get total count before pagination
            var totalCount = await query.CountAsync();

            // Apply pagination and get conversations
            var conversationsList = await query
                .OrderByDescending(c => c.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Enrich with booking data
            var conversations = conversationsList.Select(c =>
            {
                var booking = _context.Bookings
                    .Where(b => b.TenantId == tenantId && b.Phone == c.WaUserPhone)
                    .OrderByDescending(b => b.CheckinDate)
                    .FirstOrDefault();

                var rating = _context.Ratings
                    .Where(r => r.ConversationId == c.Id)
                    .FirstOrDefault();

                return new
                {
                    id = c.Id,
                    guestPhone = c.WaUserPhone,
                    guestName = booking?.GuestName,
                    roomNumber = booking?.RoomNumber,
                    startedAt = c.CreatedAt,
                    endedAt = c.TransferCompletedAt,
                    duration = c.TransferCompletedAt.HasValue
                        ? (c.TransferCompletedAt.Value - c.CreatedAt).TotalMinutes.ToString("F1") + " min"
                        : "N/A",
                    messageCount = c.Messages.Count,
                    status = c.Status,
                    lastAgent = c.AssignedAgent?.UserName,
                    satisfaction = rating?.Score,
                    transferredToHuman = c.TransferredAt.HasValue,
                    transferReason = c.TransferReason,
                    tenantId = c.TenantId
                };
            }).ToList();

            return Ok(new
            {
                data = conversations,
                total = totalCount,
                page,
                pageSize
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving conversation history");
            return StatusCode(500, new { error = "Internal server error", message = ex.Message });
        }
    }

    /// <summary>
    /// Get conversation history statistics with optional filters
    /// </summary>
    [HttpGet("history/statistics")]
    public async Task<IActionResult> GetHistoryStatistics(
        [FromQuery] string? status,
        [FromQuery] string? transferFilter,
        [FromQuery] string? period,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] int? agentId,
        [FromQuery] int? satisfaction)
    {
        try
        {
            var tenantId = HttpContext.Items["TenantId"] as int? ?? 0;
            if (tenantId == 0)
            {
                return BadRequest(new { error = "Tenant context not available" });
            }

            var query = _context.Conversations
                .Where(c => c.TenantId == tenantId)
                .Include(c => c.Messages)
                .AsQueryable();

            // Apply filters
            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(c => c.Status == status);
            }

            if (!string.IsNullOrEmpty(transferFilter))
            {
                if (transferFilter == "transferred")
                    query = query.Where(c => c.TransferredAt != null);
                else if (transferFilter == "not_transferred")
                    query = query.Where(c => c.TransferredAt == null);
            }

            if (dateFrom.HasValue)
            {
                query = query.Where(c => c.CreatedAt >= dateFrom.Value);
            }

            if (dateTo.HasValue)
            {
                query = query.Where(c => c.CreatedAt <= dateTo.Value);
            }

            if (agentId.HasValue)
            {
                query = query.Where(c => c.AssignedAgentId == agentId.Value);
            }

            var conversations = await query.ToListAsync();

            var totalConversations = conversations.Count;
            var completedConversations = conversations.Count(c => c.Status == "Closed");
            var transferredConversations = conversations.Count(c => c.TransferredAt != null);
            var totalMessageCount = conversations.Sum(c => c.Messages.Count);

            // Calculate average duration for completed conversations
            var completedWithDuration = conversations
                .Where(c => c.Status == "Closed" && c.TransferCompletedAt.HasValue)
                .Select(c => (c.TransferCompletedAt!.Value - c.CreatedAt).TotalMinutes)
                .ToList();

            var avgDurationMinutes = completedWithDuration.Any()
                ? completedWithDuration.Average()
                : 0.0;

            var stats = new
            {
                totalConversations,
                completedConversations,
                transferredConversations,
                avgSatisfaction = 0.0, // TODO: Implement ratings if needed
                avgDurationMinutes,
                totalMessageCount
            };

            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving history statistics");
            return StatusCode(500, new { error = "Internal server error", message = ex.Message });
        }
    }

    /// <summary>
    /// Get transfer details for a conversation
    /// </summary>
    [HttpGet("{id}/transfer-details")]
    public async Task<IActionResult> GetTransferDetails(int id)
    {
        try
        {
            var tenantId = HttpContext.Items["TenantId"] as int? ?? 0;
            if (tenantId == 0)
            {
                return BadRequest(new { error = "Tenant context not available" });
            }

            var conversation = await _context.Conversations
                .Where(c => c.Id == id && c.TenantId == tenantId)
                .Include(c => c.AssignedAgent)
                .Include(c => c.Messages.OrderByDescending(m => m.CreatedAt).Take(10))
                .FirstOrDefaultAsync();

            if (conversation == null)
            {
                return NotFound(new { error = "Conversation not found" });
            }

            // Get transfer record from ConversationTransfers if exists
            var transfer = await _context.ConversationTransfers
                .Where(t => t.ConversationId == id && t.TenantId == tenantId)
                .Include(t => t.FromAgent)
                .Include(t => t.ToAgent)
                .OrderByDescending(t => t.TransferredAt)
                .FirstOrDefaultAsync();

            var transferDetails = new
            {
                conversationId = conversation.Id,
                guestPhone = conversation.WaUserPhone,
                wasTransferred = conversation.TransferredAt != null,
                transferredAt = conversation.TransferredAt,
                transferReason = conversation.TransferReason ?? "Not specified",
                transferType = transfer?.FromSystem == true ? "Automatic" : "Manual",
                fromAgent = transfer?.FromAgent?.UserName ?? "Bot",
                toAgent = conversation.AssignedAgent?.UserName ?? "Unassigned",
                transferSummary = conversation.TransferSummary ?? "No summary available",
                conversationStatus = conversation.Status,
                transferCompletedAt = conversation.TransferCompletedAt,
                recentMessages = conversation.Messages
                    .OrderByDescending(m => m.CreatedAt)
                    .Take(10)
                    .Select(m => new
                    {
                        text = m.Body,
                        isFromGuest = m.Direction == "Inbound",
                        timestamp = m.CreatedAt,
                        sender = m.Direction == "Inbound" ? "Guest" : "Bot"
                    })
                    .Reverse()
                    .ToList()
            };

            return Ok(transferDetails);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving transfer details for conversation {ConversationId}", id);
            return StatusCode(500, new { error = "Internal server error", message = ex.Message });
        }
    }

    /// <summary>
    /// Export conversation as PDF or text
    /// </summary>
    [HttpGet("{id}/export")]
    public async Task<IActionResult> ExportConversation(int id, [FromQuery] string format = "pdf")
    {
        try
        {
            var tenantId = HttpContext.Items["TenantId"] as int? ?? 0;
            if (tenantId == 0)
            {
                return BadRequest(new { error = "Tenant context not available" });
            }

            var conversation = await _context.Conversations
                .Where(c => c.Id == id && c.TenantId == tenantId)
                .Include(c => c.Messages.OrderBy(m => m.CreatedAt))
                .Include(c => c.AssignedAgent)
                .GroupJoin(
                    _context.Bookings.Where(b => b.TenantId == tenantId),
                    c => c.WaUserPhone,
                    b => b.Phone,
                    (c, bookings) => new { Conversation = c, Booking = bookings.FirstOrDefault() }
                )
                .FirstOrDefaultAsync();

            if (conversation == null)
            {
                return NotFound(new { error = "Conversation not found" });
            }

            var guestName = conversation.Booking?.GuestName ?? "Unknown Guest";
            var roomNumber = conversation.Booking?.RoomNumber ?? "-";
            var agentName = conversation.Conversation.AssignedAgent?.UserName ?? "Bot";

            if (format.ToLower() == "pdf")
            {
                // Generate professional PDF using QuestPDF
                var pdfBytes = GenerateConversationPdf(
                    conversation.Conversation,
                    guestName,
                    roomNumber,
                    agentName
                );
                return File(pdfBytes, "application/pdf", $"conversation-{id}.pdf");
            }
            else
            {
                // Generate text export
                var content = new System.Text.StringBuilder();
                content.AppendLine("=== CONVERSATION SUMMARY ===");
                content.AppendLine($"Guest: {guestName}");
                content.AppendLine($"Phone: {conversation.Conversation.WaUserPhone}");
                content.AppendLine($"Room: {roomNumber}");
                content.AppendLine($"Date: {conversation.Conversation.CreatedAt:yyyy-MM-dd HH:mm}");
                content.AppendLine($"Status: {conversation.Conversation.Status}");
                content.AppendLine($"Handled by: {agentName}");
                content.AppendLine($"Total Messages: {conversation.Conversation.Messages.Count}");
                content.AppendLine();
                content.AppendLine("=== MESSAGES ===");
                content.AppendLine();

                foreach (var message in conversation.Conversation.Messages.OrderBy(m => m.CreatedAt))
                {
                    var sender = message.Direction == "Inbound" ? guestName : agentName;
                    content.AppendLine($"[{message.CreatedAt:HH:mm}] {sender}:");
                    content.AppendLine(message.Body);
                    content.AppendLine();
                }

                var bytes = System.Text.Encoding.UTF8.GetBytes(content.ToString());
                return File(bytes, "text/plain", $"conversation-{id}.txt");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting conversation {ConversationId}", id);
            return StatusCode(500, new { error = "Internal server error", message = ex.Message });
        }
    }

    private byte[] GenerateConversationPdf(Conversation conversation, string guestName, string roomNumber, string agentName)
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

        var pdfDocument = new Services.ConversationPdfDocument(conversation, guestName, roomNumber, agentName);

        return pdfDocument.GeneratePdf();
    }
}

public class AssignAgentRequest
{
    public int AgentId { get; set; }
}
