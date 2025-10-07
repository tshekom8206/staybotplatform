using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Hostr.Api.Data;
using Hostr.Api.Models;

namespace Hostr.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AgentsController : ControllerBase
{
    private readonly HostrDbContext _context;
    private readonly ILogger<AgentsController> _logger;

    public AgentsController(HostrDbContext context, ILogger<AgentsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Get all available agents for the current tenant (from Users with Agent role)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAvailableAgents()
    {
        try
        {
            var tenantId = HttpContext.Items["TenantId"] as int? ?? 0;
            if (tenantId == 0)
            {
                return BadRequest(new { error = "Tenant context not available" });
            }

            // Query users with Agent role from UserTenant table
            var agents = await _context.UserTenants
                .Where(ut => ut.TenantId == tenantId && ut.Role == "Agent")
                .Join(_context.Users,
                    ut => ut.UserId,
                    u => u.Id,
                    (ut, u) => new
                    {
                        Id = u.Id,
                        Name = u.UserName,
                        Email = u.Email,
                        Department = "Agent", // Default department
                        State = u.IsActive ? "Available" : "Offline",
                        StatusMessage = (string?)null,
                        MaxConcurrentChats = 5,
                        ActiveConversations = 0, // We'll calculate this from Conversations table if needed
                        IsAvailable = u.IsActive,
                        LastActivity = u.CreatedAt
                    })
                .OrderBy(a => a.ActiveConversations)
                .ThenBy(a => a.Name)
                .ToListAsync();

            return Ok(agents);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving available agents");
            return StatusCode(500, new { error = "Internal server error", message = ex.Message });
        }
    }

    /// <summary>
    /// Get a specific agent by ID (from Users with Agent role)
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetAgent(int id)
    {
        try
        {
            var tenantId = HttpContext.Items["TenantId"] as int? ?? 0;
            if (tenantId == 0)
            {
                return BadRequest(new { error = "Tenant context not available" });
            }

            var agent = await _context.UserTenants
                .Where(ut => ut.TenantId == tenantId && ut.UserId == id && ut.Role == "Agent")
                .Join(_context.Users,
                    ut => ut.UserId,
                    u => u.Id,
                    (ut, u) => new
                    {
                        Id = u.Id,
                        Name = u.UserName,
                        Email = u.Email,
                        Department = "Agent",
                        Skills = new string[] { },
                        State = u.IsActive ? "Available" : "Offline",
                        StatusMessage = (string?)null,
                        MaxConcurrentChats = 5,
                        ActiveConversations = 0,
                        LastActivity = u.CreatedAt,
                        SessionStarted = (DateTime?)null,
                        CreatedAt = u.CreatedAt
                    })
                .FirstOrDefaultAsync();

            if (agent == null)
            {
                return NotFound(new { error = "Agent not found" });
            }

            return Ok(agent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving agent {AgentId}", id);
            return StatusCode(500, new { error = "Internal server error", message = ex.Message });
        }
    }

    /// <summary>
    /// Get workload details for a specific agent
    /// </summary>
    [HttpGet("~/api/agent/{id}/workload")]
    public async Task<IActionResult> GetAgentWorkload(int id)
    {
        try
        {
            var tenantId = HttpContext.Items["TenantId"] as int? ?? 0;
            if (tenantId == 0)
            {
                return BadRequest(new { error = "Tenant context not available" });
            }

            // Verify agent exists for this tenant
            var agentExists = await _context.UserTenants
                .AnyAsync(ut => ut.TenantId == tenantId && ut.UserId == id && ut.Role == "Agent");

            if (!agentExists)
            {
                return NotFound(new { error = "Agent not found" });
            }

            // Get active conversations for this agent
            var activeChats = await _context.Conversations
                .Where(c => c.TenantId == tenantId && c.AssignedAgentId == id && c.Status == "Active")
                .Select(c => new
                {
                    ConversationId = c.Id,
                    PhoneNumber = c.WaUserPhone,
                    StartedAt = c.CreatedAt,
                    LastMessageAt = c.LastBotReplyAt ?? c.CreatedAt,
                    Priority = "Normal",
                    Status = c.Status
                })
                .ToListAsync();

            // Get pending tasks assigned to this agent
            var pendingTasks = await _context.StaffTasks
                .CountAsync(t => t.TenantId == tenantId && t.AssignedToId == id && t.Status == "Pending");

            var activeConversations = activeChats.Count;
            var maxConcurrent = 5; // Default value
            var utilizationPercentage = maxConcurrent > 0 ? (int)Math.Round((double)activeConversations / maxConcurrent * 100) : 0;

            var workload = new
            {
                AgentId = id,
                ActiveConversations = activeConversations,
                PendingTasks = pendingTasks,
                UtilizationPercentage = utilizationPercentage,
                AverageResponseTime = "0s", // Placeholder - would need message history analysis
                ActiveChats = activeChats
            };

            return Ok(workload);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving workload for agent {AgentId}", id);
            return StatusCode(500, new { error = "Internal server error", message = ex.Message });
        }
    }

    /// <summary>
    /// Update agent status (Available, Busy, Away, etc.)
    /// </summary>
    [HttpPut("~/api/agent/{id}/status")]
    public async Task<IActionResult> UpdateAgentStatus(int id, [FromBody] UpdateAgentStatusRequest request)
    {
        try
        {
            var tenantId = HttpContext.Items["TenantId"] as int? ?? 0;
            if (tenantId == 0)
            {
                return BadRequest(new { error = "Tenant context not available" });
            }

            // Verify agent exists for this tenant
            var userTenant = await _context.UserTenants
                .FirstOrDefaultAsync(ut => ut.TenantId == tenantId && ut.UserId == id && ut.Role == "Agent");

            if (userTenant == null)
            {
                return NotFound(new { error = "Agent not found" });
            }

            // Get the user
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (user == null)
            {
                return NotFound(new { error = "User not found" });
            }

            // Update user's IsActive based on status
            user.IsActive = request.State == "Available";

            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "Agent status updated successfully",
                agentId = id,
                state = request.State,
                statusMessage = request.StatusMessage
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating status for agent {AgentId}", id);
            return StatusCode(500, new { error = "Internal server error", message = ex.Message });
        }
    }

    /// <summary>
    /// Get agent statistics for the current tenant
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetAgentStats()
    {
        try
        {
            var tenantId = HttpContext.Items["TenantId"] as int? ?? 0;
            if (tenantId == 0)
            {
                return BadRequest(new { error = "Tenant context not available" });
            }

            // Get all agents for the tenant
            var agents = await _context.UserTenants
                .Where(ut => ut.TenantId == tenantId && ut.Role == "Agent")
                .Join(_context.Users,
                    ut => ut.UserId,
                    u => u.Id,
                    (ut, u) => new
                    {
                        UserId = u.Id,
                        IsActive = u.IsActive
                    })
                .ToListAsync();

            var totalAgents = agents.Count;
            var availableAgents = agents.Count(a => a.IsActive);
            var offlineAgents = agents.Count(a => !a.IsActive);

            // Get active conversations count (agents currently handling conversations)
            var activeConversations = await _context.Conversations
                .Where(c => c.TenantId == tenantId && c.Status == "Active" && c.AssignedAgentId != null)
                .Select(c => c.AssignedAgentId)
                .Distinct()
                .CountAsync();

            var busyAgents = activeConversations;

            // Calculate average workload (conversations per agent)
            var totalActiveConversations = await _context.Conversations
                .Where(c => c.TenantId == tenantId && c.Status == "Active")
                .CountAsync();

            var averageWorkload = totalAgents > 0 ? (double)totalActiveConversations / totalAgents : 0;

            // Get pending transfers
            var pendingTransfers = await _context.ConversationTransfers
                .Where(ct => ct.TenantId == tenantId && ct.Status == "Pending")
                .CountAsync();

            var stats = new
            {
                TotalAgents = totalAgents,
                AvailableAgents = availableAgents - busyAgents, // Available but not busy
                BusyAgents = busyAgents,
                OfflineAgents = offlineAgents,
                AverageWorkload = Math.Round(averageWorkload, 2),
                TotalActiveConversations = totalActiveConversations,
                TotalPendingTransfers = pendingTransfers
            };

            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving agent statistics");
            return StatusCode(500, new { error = "Internal server error", message = ex.Message });
        }
    }
}

public class UpdateAgentStatusRequest
{
    public string State { get; set; } = string.Empty;
    public string? StatusMessage { get; set; }
}
