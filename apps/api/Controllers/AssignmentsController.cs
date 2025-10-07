using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Hostr.Api.Data;
using Hostr.Api.Models;

namespace Hostr.Api.Controllers;

[ApiController]
[Route("api/assignments")]
public class AssignmentsController : ControllerBase
{
    private readonly HostrDbContext _context;
    private readonly ILogger<AssignmentsController> _logger;

    public AssignmentsController(HostrDbContext context, ILogger<AssignmentsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Get all active conversation assignments (conversations currently assigned to agents)
    /// </summary>
    [HttpGet("active")]
    public async Task<ActionResult<IEnumerable<object>>> GetActiveAssignments()
    {
        var tenantId = HttpContext.Items["TenantId"] as int? ?? 0;

        try
        {
            var activeAssignments = await _context.ConversationTransfers
                .Include(ct => ct.Conversation)
                    .ThenInclude(c => c.Messages.OrderByDescending(m => m.CreatedAt).Take(1))
                .Include(ct => ct.ToAgent)
                .Include(ct => ct.FromAgent)
                .Where(ct => ct.TenantId == tenantId
                    && ct.Status == "Completed"
                    && ct.ReleasedAt == null)
                .OrderByDescending(ct => ct.TransferredAt)
                .Select(ct => new
                {
                    id = ct.Id,
                    conversationId = ct.ConversationId,
                    agentId = ct.ToAgentId,
                    agentName = ct.ToAgent != null ? ct.ToAgent.UserName : "Unknown Agent",
                    agentEmail = ct.ToAgent != null ? ct.ToAgent.Email : "",
                    guestPhone = ct.Conversation.WaUserPhone,
                    guestName = (string?)null, // Can be enhanced with booking lookup
                    roomNumber = (string?)null, // Can be enhanced with booking lookup
                    assignedAt = ct.TransferredAt,
                    releasedAt = ct.ReleasedAt,
                    status = ct.ReleasedAt == null ? "Active" : "Completed",
                    priority = DeterminePriority(ct),
                    responseTime = CalculateResponseTime(ct.TransferredAt),
                    responseTimeSeconds = (int)(DateTime.UtcNow - ct.TransferredAt).TotalSeconds,
                    lastActivity = ct.Conversation.Messages.Any()
                        ? ct.Conversation.Messages.Max(m => m.CreatedAt)
                        : ct.TransferredAt,
                    messageCount = ct.Conversation.Messages.Count(),
                    transferHistory = new[] { new
                    {
                        id = ct.Id,
                        fromAgentId = ct.FromAgentId,
                        fromAgentName = ct.FromAgent != null ? ct.FromAgent.UserName : "System",
                        toAgentId = ct.ToAgentId,
                        toAgentName = ct.ToAgent != null ? ct.ToAgent.UserName : "Unknown",
                        transferredAt = ct.TransferredAt,
                        reason = ct.TransferReason,
                        status = ct.Status
                    }},
                    tenantId = ct.TenantId
                })
                .ToListAsync();

            _logger.LogInformation($"Retrieved {activeAssignments.Count} active assignments for tenant {tenantId}");
            return Ok(activeAssignments);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active assignments");
            return StatusCode(500, new { error = "Failed to retrieve active assignments" });
        }
    }

    /// <summary>
    /// Get assignment history with optional filters
    /// </summary>
    [HttpGet("history")]
    public async Task<ActionResult<IEnumerable<object>>> GetAssignmentHistory(
        [FromQuery] string? status = null,
        [FromQuery] int? agentId = null,
        [FromQuery] string? priority = null,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        [FromQuery] string? roomNumber = null)
    {
        var tenantId = HttpContext.Items["TenantId"] as int? ?? 0;

        try
        {
            var query = _context.ConversationTransfers
                .Include(ct => ct.Conversation)
                    .ThenInclude(c => c.Messages)
                .Include(ct => ct.ToAgent)
                .Include(ct => ct.FromAgent)
                .Where(ct => ct.TenantId == tenantId);

            // Apply filters
            if (!string.IsNullOrEmpty(status))
            {
                if (status == "Active")
                    query = query.Where(ct => ct.Status == "Completed" && ct.ReleasedAt == null);
                else if (status == "Completed")
                    query = query.Where(ct => ct.ReleasedAt != null);
                else
                    query = query.Where(ct => ct.Status == status);
            }

            if (agentId.HasValue)
            {
                query = query.Where(ct => ct.ToAgentId == agentId.Value);
            }

            if (dateFrom.HasValue)
            {
                query = query.Where(ct => ct.TransferredAt >= dateFrom.Value);
            }

            if (dateTo.HasValue)
            {
                query = query.Where(ct => ct.TransferredAt <= dateTo.Value);
            }

            var assignments = await query
                .OrderByDescending(ct => ct.TransferredAt)
                .Select(ct => new
                {
                    id = ct.Id,
                    conversationId = ct.ConversationId,
                    agentId = ct.ToAgentId,
                    agentName = ct.ToAgent != null ? ct.ToAgent.UserName : "Unknown Agent",
                    agentEmail = ct.ToAgent != null ? ct.ToAgent.Email : "",
                    guestPhone = ct.Conversation.WaUserPhone,
                    guestName = (string?)null,
                    roomNumber = (string?)null,
                    assignedAt = ct.TransferredAt,
                    releasedAt = ct.ReleasedAt,
                    status = ct.ReleasedAt == null ? "Active" : "Completed",
                    priority = DeterminePriority(ct),
                    responseTime = CalculateResponseTime(ct.TransferredAt),
                    responseTimeSeconds = (int)(DateTime.UtcNow - ct.TransferredAt).TotalSeconds,
                    lastActivity = ct.Conversation.Messages.Any()
                        ? ct.Conversation.Messages.Max(m => m.CreatedAt)
                        : ct.TransferredAt,
                    messageCount = ct.Conversation.Messages.Count(),
                    tenantId = ct.TenantId
                })
                .ToListAsync();

            return Ok(assignments);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving assignment history");
            return StatusCode(500, new { error = "Failed to retrieve assignment history" });
        }
    }

    /// <summary>
    /// Get assignment statistics
    /// </summary>
    [HttpGet("statistics")]
    public async Task<ActionResult<object>> GetAssignmentStatistics()
    {
        var tenantId = HttpContext.Items["TenantId"] as int? ?? 0;

        try
        {
            var today = DateTime.UtcNow.Date;

            // Active assignments
            var activeAssignments = await _context.ConversationTransfers
                .Where(ct => ct.TenantId == tenantId
                    && ct.Status == "Completed"
                    && ct.ReleasedAt == null)
                .CountAsync();

            // Completed today
            var completedToday = await _context.ConversationTransfers
                .Where(ct => ct.TenantId == tenantId
                    && ct.ReleasedAt != null
                    && ct.ReleasedAt.Value >= today)
                .CountAsync();

            // Total assignments
            var totalAssignments = await _context.ConversationTransfers
                .Where(ct => ct.TenantId == tenantId)
                .CountAsync();

            // Average response time (in seconds)
            var assignmentsWithDuration = await _context.ConversationTransfers
                .Where(ct => ct.TenantId == tenantId && ct.ReleasedAt != null)
                .Select(ct => (ct.ReleasedAt.Value - ct.TransferredAt).TotalSeconds)
                .ToListAsync();

            var avgResponseTimeSeconds = assignmentsWithDuration.Any()
                ? (int)assignmentsWithDuration.Average()
                : 0;

            var avgResponseTime = FormatDuration(avgResponseTimeSeconds);

            // Agents online (users with Agent role and recent activity)
            var fiveMinutesAgo = DateTime.UtcNow.AddMinutes(-5);
            var totalAgentsOnline = await _context.UserTenants
                .Where(ut => ut.TenantId == tenantId && ut.Role == "Agent")
                .Join(_context.Users,
                    ut => ut.UserId,
                    u => u.Id,
                    (ut, u) => new { User = u })
                .Where(x => x.User.IsActive)
                .CountAsync();

            // Utilization rate (active assignments / (total agents * max concurrent))
            var totalAgents = await _context.UserTenants
                .Where(ut => ut.TenantId == tenantId && ut.Role == "Agent")
                .CountAsync();

            var utilizationRate = totalAgents > 0
                ? (double)activeAssignments / (totalAgents * 5) * 100 // Assuming max 5 concurrent per agent
                : 0;

            var statistics = new
            {
                totalAssignments = totalAssignments,
                activeAssignments = activeAssignments,
                completedToday = completedToday,
                avgResponseTimeSeconds = avgResponseTimeSeconds,
                avgResponseTime = avgResponseTime,
                totalAgentsOnline = totalAgentsOnline,
                utilizationRate = Math.Round(utilizationRate, 2)
            };

            return Ok(statistics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving assignment statistics");
            return StatusCode(500, new { error = "Failed to retrieve statistics" });
        }
    }

    /// <summary>
    /// Get agent performance metrics
    /// </summary>
    [HttpGet("agent-performance")]
    public async Task<ActionResult<IEnumerable<object>>> GetAgentPerformance()
    {
        var tenantId = HttpContext.Items["TenantId"] as int? ?? 0;

        try
        {
            var today = DateTime.UtcNow.Date;
            var fiveMinutesAgo = DateTime.UtcNow.AddMinutes(-5);

            var agents = await _context.UserTenants
                .Where(ut => ut.TenantId == tenantId && ut.Role == "Agent")
                .Join(_context.Users,
                    ut => ut.UserId,
                    u => u.Id,
                    (ut, u) => new { Id = u.Id, Name = u.UserName, Email = u.Email, IsActive = u.IsActive })
                .ToListAsync();

            var performance = new List<object>();

            foreach (var agent in agents)
            {
                // Active assignments
                var activeAssignments = await _context.ConversationTransfers
                    .Where(ct => ct.TenantId == tenantId
                        && ct.ToAgentId == agent.Id
                        && ct.Status == "Completed"
                        && ct.ReleasedAt == null)
                    .CountAsync();

                // Completed today
                var completedToday = await _context.ConversationTransfers
                    .Where(ct => ct.TenantId == tenantId
                        && ct.ToAgentId == agent.Id
                        && ct.ReleasedAt != null
                        && ct.ReleasedAt.Value >= today)
                    .CountAsync();

                // Average response time
                var durations = await _context.ConversationTransfers
                    .Where(ct => ct.TenantId == tenantId
                        && ct.ToAgentId == agent.Id
                        && ct.ReleasedAt != null)
                    .Select(ct => (ct.ReleasedAt.Value - ct.TransferredAt).TotalSeconds)
                    .ToListAsync();

                var avgResponseTimeSeconds = durations.Any() ? (int)durations.Average() : 0;

                // Total messages handled
                var totalMessages = await _context.ConversationTransfers
                    .Where(ct => ct.TenantId == tenantId && ct.ToAgentId == agent.Id)
                    .SelectMany(ct => ct.Conversation.Messages)
                    .CountAsync();

                // Customer satisfaction (from ratings)
                var ratings = await _context.Ratings
                    .Where(r => r.TenantId == tenantId
                        && r.ConversationId != null
                        && _context.ConversationTransfers.Any(ct =>
                            ct.ConversationId == r.ConversationId
                            && ct.ToAgentId == agent.Id))
                    .Select(r => r.Score)
                    .ToListAsync();

                var avgRating = ratings.Any() ? ratings.Average() : (double?)null;

                performance.Add(new
                {
                    agentId = agent.Id,
                    agentName = agent.Name,
                    activeAssignments = activeAssignments,
                    completedToday = completedToday,
                    avgResponseTimeSeconds = avgResponseTimeSeconds,
                    avgResponseTime = FormatDuration(avgResponseTimeSeconds),
                    totalMessagesHandled = totalMessages,
                    customerSatisfactionRating = avgRating.HasValue ? Math.Round(avgRating.Value, 2) : (double?)null,
                    isOnline = agent.IsActive,
                    lastActivity = (DateTime?)null // Not tracked for agents from Users table
                });
            }

            return Ok(performance.OrderByDescending(p => ((dynamic)p).activeAssignments));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving agent performance");
            return StatusCode(500, new { error = "Failed to retrieve agent performance" });
        }
    }

    // Helper methods
    private static string DeterminePriority(ConversationTransfer transfer)
    {
        // Determine priority based on various factors
        var now = DateTime.UtcNow;
        var age = (now - transfer.TransferredAt).TotalMinutes;

        if (transfer.TransferReason != null &&
            (transfer.TransferReason.ToLower().Contains("emergency") ||
             transfer.TransferReason.ToLower().Contains("urgent")))
        {
            return "Emergency";
        }

        if (age > 30) // Waiting more than 30 minutes
        {
            return "High";
        }

        return "Normal";
    }

    private static string CalculateResponseTime(DateTime startTime)
    {
        var duration = DateTime.UtcNow - startTime;
        return FormatDuration((int)duration.TotalSeconds);
    }

    private static string FormatDuration(int seconds)
    {
        if (seconds < 60)
            return $"{seconds}s";

        if (seconds < 3600)
        {
            var minutes = seconds / 60;
            var secs = seconds % 60;
            return $"{minutes}m {secs}s";
        }

        var hours = seconds / 3600;
        var mins = (seconds % 3600) / 60;
        return $"{hours}h {mins}m";
    }

    /// <summary>
    /// Transfer assignment to another agent
    /// </summary>
    [HttpPost("{id}/transfer")]
    public async Task<ActionResult> TransferAssignment(int id, [FromBody] TransferAssignmentRequest request)
    {
        var tenantId = HttpContext.Items["TenantId"] as int? ?? 0;

        try
        {
            var transfer = await _context.ConversationTransfers
                .Include(ct => ct.Conversation)
                .FirstOrDefaultAsync(ct => ct.Id == id && ct.TenantId == tenantId);

            if (transfer == null)
            {
                return NotFound(new { error = "Assignment not found" });
            }

            if (transfer.ReleasedAt != null)
            {
                return BadRequest(new { error = "Cannot transfer a released assignment" });
            }

            // Create new transfer record
            var newTransfer = new ConversationTransfer
            {
                ConversationId = transfer.ConversationId,
                TenantId = tenantId,
                FromAgentId = transfer.ToAgentId,
                ToAgentId = request.NewAgentId,
                TransferReason = request.Reason,
                TransferredAt = DateTime.UtcNow,
                Status = "Pending"
            };

            _context.ConversationTransfers.Add(newTransfer);

            // Release the old transfer
            transfer.ReleasedAt = DateTime.UtcNow;
            transfer.ReleaseReason = "Transferred to another agent";

            await _context.SaveChangesAsync();

            _logger.LogInformation($"Assignment {id} transferred from agent {transfer.ToAgentId} to agent {request.NewAgentId}");
            return Ok(new { message = "Assignment transferred successfully", newTransferId = newTransfer.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error transferring assignment {id}");
            return StatusCode(500, new { error = "Failed to transfer assignment" });
        }
    }

    /// <summary>
    /// Mark assignment as completed
    /// </summary>
    [HttpPost("{id}/complete")]
    public async Task<ActionResult> CompleteAssignment(int id, [FromBody] CompleteAssignmentRequest request)
    {
        var tenantId = HttpContext.Items["TenantId"] as int? ?? 0;

        try
        {
            var transfer = await _context.ConversationTransfers
                .Include(ct => ct.Conversation)
                .FirstOrDefaultAsync(ct => ct.Id == id && ct.TenantId == tenantId);

            if (transfer == null)
            {
                return NotFound(new { error = "Assignment not found" });
            }

            if (transfer.ReleasedAt != null)
            {
                return BadRequest(new { error = "Assignment is already completed" });
            }

            // Mark transfer as released (completed)
            transfer.ReleasedAt = DateTime.UtcNow;
            transfer.ReleaseReason = "Completed";
            transfer.Notes = request.CompletionNotes;
            transfer.Status = "Completed";

            // Update conversation status
            if (transfer.Conversation != null)
            {
                transfer.Conversation.Status = "Closed";
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation($"Assignment {id} marked as completed by agent {transfer.ToAgentId}");
            return Ok(new { message = "Assignment completed successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error completing assignment {id}");
            return StatusCode(500, new { error = "Failed to complete assignment" });
        }
    }

    /// <summary>
    /// Release assignment (remove agent assignment)
    /// </summary>
    [HttpPost("{id}/release")]
    public async Task<ActionResult> ReleaseAssignment(int id, [FromBody] ReleaseAssignmentRequest request)
    {
        var tenantId = HttpContext.Items["TenantId"] as int? ?? 0;

        try
        {
            var transfer = await _context.ConversationTransfers
                .Include(ct => ct.Conversation)
                .FirstOrDefaultAsync(ct => ct.Id == id && ct.TenantId == tenantId);

            if (transfer == null)
            {
                return NotFound(new { error = "Assignment not found" });
            }

            if (transfer.ReleasedAt != null)
            {
                return BadRequest(new { error = "Assignment is already released" });
            }

            // Release the assignment
            transfer.ReleasedAt = DateTime.UtcNow;
            transfer.ReleaseReason = request.ReleaseReason;
            transfer.Status = "Released";

            // Update conversation to remove agent assignment
            if (transfer.Conversation != null)
            {
                transfer.Conversation.AssignedAgentId = null;
                transfer.Conversation.Status = "Active"; // Return to active status
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation($"Assignment {id} released from agent {transfer.ToAgentId}. Reason: {request.ReleaseReason}");
            return Ok(new { message = "Assignment released successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error releasing assignment {id}");
            return StatusCode(500, new { error = "Failed to release assignment" });
        }
    }
}

// Request models
public class TransferAssignmentRequest
{
    public int NewAgentId { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class CompleteAssignmentRequest
{
    public string? CompletionNotes { get; set; }
}

public class ReleaseAssignmentRequest
{
    public string ReleaseReason { get; set; } = string.Empty;
}
