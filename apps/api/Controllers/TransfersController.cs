using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Hostr.Api.Data;
using Hostr.Api.Models;
using Hostr.Api.Services;

namespace Hostr.Api.Controllers;

[ApiController]
[Route("api/transfers")]
public class TransfersController : ControllerBase
{
    private readonly HostrDbContext _context;
    private readonly ILogger<TransfersController> _logger;

    public TransfersController(
        HostrDbContext context,
        ILogger<TransfersController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Get all transfer requests from database (all statuses)
    /// </summary>
    [HttpGet("queue")]
    public async Task<IActionResult> GetTransferQueue()
    {
        try
        {
            var tenantId = HttpContext.Items["TenantId"] as int? ?? 0;

            if (tenantId == 0)
            {
                return BadRequest(new { error = "Tenant context not available" });
            }

            var transfersQuery = await _context.ConversationTransfers
                .Where(t => t.TenantId == tenantId)
                .Include(t => t.Conversation)
                .Include(t => t.ToAgent)
                .OrderByDescending(t => t.TransferredAt)
                .ToListAsync();

            var transfers = transfersQuery.Select(t =>
            {
                var booking = _context.Bookings
                    .Where(b => b.TenantId == tenantId && b.Phone == t.Conversation.WaUserPhone)
                    .OrderByDescending(b => b.CheckinDate)
                    .FirstOrDefault();

                return new
                {
                    id = t.Id,
                    conversationId = t.ConversationId,
                    guestPhone = t.Conversation.WaUserPhone,
                    guestName = booking?.GuestName,
                    roomNumber = booking?.RoomNumber,
                    transferReason = t.TransferReason,
                    priority = t.TransferReason.ToLower().Contains("emergency") || t.TransferReason.ToLower().Contains("urgent")
                        ? "Emergency"
                        : t.TransferReason.ToLower().Contains("high")
                            ? "High"
                            : "Normal",
                    detectionMethod = t.FromSystem ? "System" : "Manual",
                    triggerPhrase = t.Notes ?? "",
                    requestedAt = t.TransferredAt,
                    status = t.Status == "Pending"
                        ? "Pending"
                        : t.Status == "Completed" && t.Conversation.Status == "TransferredToAgent"
                            ? "InProgress"
                            : t.Status == "Completed" && t.Conversation.Status == "Closed"
                                ? "Completed"
                                : t.Status,
                    assignedAgent = t.ToAgent?.UserName,
                    tenantId = t.TenantId
                };
            }).ToList();

            return Ok(transfers);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving transfer queue");
            return StatusCode(500, new { error = "Internal server error", message = ex.Message });
        }
    }

    /// <summary>
    /// Get transfer queue statistics
    /// </summary>
    [HttpGet("statistics")]
    public async Task<IActionResult> GetTransferStatistics()
    {
        try
        {
            var tenantId = HttpContext.Items["TenantId"] as int? ?? 0;

            if (tenantId == 0)
            {
                return BadRequest(new { error = "Tenant context not available" });
            }

            var pendingCount = await _context.ConversationTransfers
                .Where(t => t.TenantId == tenantId && t.Status == "Pending")
                .CountAsync();

            var emergencyCount = await _context.ConversationTransfers
                .Where(t => t.TenantId == tenantId &&
                           t.Status == "Pending" &&
                           (t.TransferReason.Contains("emergency") || t.TransferReason.Contains("urgent")))
                .CountAsync();

            var inProgressCount = await _context.ConversationTransfers
                .Where(t => t.TenantId == tenantId &&
                           t.Status == "Completed" &&
                           t.Conversation.Status == "TransferredToAgent")
                .CountAsync();

            var completedTodayCount = await _context.ConversationTransfers
                .Where(t => t.TenantId == tenantId &&
                           t.Status == "Completed" &&
                           t.Conversation.Status == "Closed" &&
                           t.Conversation.TransferCompletedAt != null &&
                           t.Conversation.TransferCompletedAt >= DateTime.UtcNow.Date)
                .CountAsync();

            return Ok(new
            {
                pendingTransfers = pendingCount,
                emergencyTransfers = emergencyCount,
                inProgressTransfers = inProgressCount,
                completedToday = completedTodayCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving transfer statistics");
            return StatusCode(500, new { error = "Internal server error", message = ex.Message });
        }
    }

    /// <summary>
    /// Get a specific transfer request with details
    /// </summary>
    [HttpGet("{id}/details")]
    public async Task<IActionResult> GetTransferDetails(int id)
    {
        try
        {
            var tenantId = HttpContext.Items["TenantId"] as int? ?? 0;

            if (tenantId == 0)
            {
                return BadRequest(new { error = "Tenant not found" });
            }

            var transfer = await _context.ConversationTransfers
                .Where(t => t.Id == id && t.TenantId == tenantId)
                .Include(t => t.Conversation)
                    .ThenInclude(c => c.Messages.OrderByDescending(m => m.CreatedAt).Take(10))
                .Include(t => t.ToAgent)
                .FirstOrDefaultAsync();

            if (transfer == null)
            {
                return NotFound(new { error = "Transfer request not found" });
            }

            var booking = await _context.Bookings
                .Where(b => b.TenantId == tenantId && b.Phone == transfer.Conversation.WaUserPhone)
                .OrderByDescending(b => b.CheckinDate)
                .FirstOrDefaultAsync();

            return Ok(new
            {
                id = transfer.Id,
                conversationId = transfer.ConversationId,
                guestPhone = transfer.Conversation.WaUserPhone,
                guestName = booking?.GuestName,
                roomNumber = booking?.RoomNumber,
                transferReason = transfer.TransferReason,
                priority = transfer.TransferReason.ToLower().Contains("emergency") || transfer.TransferReason.ToLower().Contains("urgent")
                    ? "Emergency"
                    : transfer.TransferReason.ToLower().Contains("high")
                        ? "High"
                        : "Normal",
                detectionMethod = transfer.FromSystem ? "System" : "Manual",
                triggerPhrase = transfer.Notes ?? "",
                status = transfer.Status == "Pending"
                    ? "Pending"
                    : transfer.Status == "Completed" && transfer.Conversation.Status == "TransferredToAgent"
                        ? "InProgress"
                        : transfer.Status,
                requestedAt = transfer.TransferredAt,
                assignedAgent = transfer.ToAgent?.UserName,
                handoffContext = new
                {
                    handoffSummary = transfer.Notes ?? "",
                    recentMessages = transfer.Conversation.Messages
                        .OrderByDescending(m => m.CreatedAt)
                        .Take(10)
                        .Select(m => new
                        {
                            text = m.Body,
                            isFromGuest = m.Direction == "Inbound",
                            timestamp = m.CreatedAt
                        })
                        .ToList()
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving transfer details {TransferId}", id);
            return StatusCode(500, new { error = "Internal server error", message = ex.Message });
        }
    }

    /// <summary>
    /// Get a specific transfer request
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetTransfer(int id)
    {
        try
        {
            var tenantId = HttpContext.Items["TenantId"] as int? ?? 0;

            if (tenantId == 0)
            {
                return BadRequest(new { error = "Tenant not found" });
            }

            var transfer = await _context.ConversationTransfers
                .Where(t => t.Id == id && t.TenantId == tenantId)
                .Include(t => t.Conversation)
                    .ThenInclude(c => c.Messages.OrderByDescending(m => m.CreatedAt).Take(10))
                .Include(t => t.ToAgent)
                .FirstOrDefaultAsync();

            if (transfer == null)
            {
                return NotFound(new { error = "Transfer request not found" });
            }

            return Ok(new
            {
                id = transfer.Id,
                conversationId = transfer.ConversationId,
                guestPhone = transfer.Conversation.WaUserPhone,
                transferReason = transfer.TransferReason,
                status = transfer.Status,
                requestedAt = transfer.TransferredAt,
                assignedAgent = transfer.ToAgent?.UserName,
                handoffContext = new
                {
                    conversationSummary = transfer.Notes ?? "",
                    recentMessages = transfer.Conversation.Messages
                        .OrderByDescending(m => m.CreatedAt)
                        .Take(10)
                        .Select(m => new
                        {
                            text = m.Body,
                            isFromGuest = m.Direction == "Inbound",
                            timestamp = m.CreatedAt
                        })
                        .ToList()
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving transfer request {TransferId}", id);
            return StatusCode(500, new { error = "Internal server error", message = ex.Message });
        }
    }

    /// <summary>
    /// Accept a transfer request and assign to current agent
    /// </summary>
    [HttpPost("{id}/accept")]
    public async Task<IActionResult> AcceptTransfer(int id, [FromBody] AcceptTransferRequest request)
    {
        try
        {
            var tenantId = HttpContext.Items["TenantId"] as int? ?? 0;

            if (tenantId == 0)
            {
                return BadRequest(new { error = "Tenant not found" });
            }

            var transfer = await _context.ConversationTransfers
                .Where(t => t.Id == id && t.TenantId == tenantId && t.Status == "Pending")
                .Include(t => t.Conversation)
                .FirstOrDefaultAsync();

            if (transfer == null)
            {
                return NotFound(new { error = "Transfer request not found or already processed" });
            }

            // Update transfer status
            transfer.Status = "Completed";
            transfer.ToAgentId = request.AgentId;

            // Update conversation assignment
            transfer.Conversation.AssignedAgentId = request.AgentId;
            transfer.Conversation.Status = "TransferredToAgent";
            transfer.Conversation.TransferredAt = DateTime.UtcNow;
            transfer.Conversation.TransferCompletedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Transfer {TransferId} accepted by agent {AgentId} for conversation {ConversationId}",
                id, request.AgentId, transfer.ConversationId);

            return Ok(new { success = true, message = "Transfer accepted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error accepting transfer {TransferId}", id);
            return StatusCode(500, new { error = "Internal server error", message = ex.Message });
        }
    }

    /// <summary>
    /// Complete a transfer that is in progress
    /// </summary>
    [HttpPost("{id}/complete")]
    public async Task<IActionResult> CompleteTransfer(int id, [FromBody] CompleteTransferRequest? request)
    {
        try
        {
            var tenantId = HttpContext.Items["TenantId"] as int? ?? 0;

            if (tenantId == 0)
            {
                return BadRequest(new { error = "Tenant not found" });
            }

            var transfer = await _context.ConversationTransfers
                .Where(t => t.Id == id && t.TenantId == tenantId)
                .Include(t => t.Conversation)
                .FirstOrDefaultAsync();

            if (transfer == null)
            {
                return NotFound(new { error = "Transfer request not found" });
            }

            // Update conversation status to close it
            transfer.Conversation.Status = "Closed";
            transfer.Conversation.TransferCompletedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Transfer {TransferId} completed for conversation {ConversationId}",
                id, transfer.ConversationId);

            return Ok(new { success = true, message = "Transfer completed successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing transfer {TransferId}", id);
            return StatusCode(500, new { error = "Internal server error", message = ex.Message });
        }
    }

    /// <summary>
    /// Reject a transfer request
    /// </summary>
    [HttpPost("{id}/reject")]
    public async Task<IActionResult> RejectTransfer(int id, [FromBody] RejectTransferRequest request)
    {
        try
        {
            var tenantId = HttpContext.Items["TenantId"] as int? ?? 0;

            if (tenantId == 0)
            {
                return BadRequest(new { error = "Tenant not found" });
            }

            var transfer = await _context.ConversationTransfers
                .Where(t => t.Id == id && t.TenantId == tenantId && t.Status == "Pending")
                .FirstOrDefaultAsync();

            if (transfer == null)
            {
                return NotFound(new { error = "Transfer request not found or already processed" });
            }

            transfer.Status = "Released";
            transfer.ReleasedAt = DateTime.UtcNow;
            transfer.ReleaseReason = request.Reason ?? "Rejected by agent";

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Transfer {TransferId} rejected for conversation {ConversationId}. Reason: {Reason}",
                id, transfer.ConversationId, request.Reason);

            return Ok(new { success = true, message = "Transfer rejected" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting transfer {TransferId}", id);
            return StatusCode(500, new { error = "Internal server error", message = ex.Message });
        }
    }
}

public class AcceptTransferRequest
{
    public int AgentId { get; set; }
}

public class RejectTransferRequest
{
    public string? Reason { get; set; }
}

public class CompleteTransferRequest
{
    public string? CompletionNotes { get; set; }
}
