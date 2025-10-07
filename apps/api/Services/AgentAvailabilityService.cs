using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Hostr.Api.Data;
using Hostr.Api.Models;
using System.Text.Json;

namespace Hostr.Api.Services;

public interface IAgentAvailabilityService
{
    Task<List<AvailableAgent>> GetAvailableAgentsAsync(int tenantId, string? department = null, TransferPriority priority = TransferPriority.Normal);
    Task<AgentStatus?> GetAgentStatusAsync(int tenantId, int agentId);
    Task<bool> UpdateAgentStatusAsync(int tenantId, int agentId, AgentAvailabilityState state, string? statusMessage = null);
    Task<bool> AssignConversationToAgentAsync(int conversationId, int agentId, TransferReason reason);
    Task<bool> ReleaseConversationFromAgentAsync(int conversationId, string releaseReason);
    Task<AgentWorkload> GetAgentWorkloadAsync(int tenantId, int agentId);
    Task<List<AgentStatus>> GetDepartmentAgentStatusAsync(int tenantId, string department);
    Task<TransferRouting> DetermineOptimalTransferRoutingAsync(int tenantId, TransferRequest request);
    Task RegisterAgentHeartbeatAsync(int tenantId, int agentId);
    Task<List<AgentStatus>> GetExpiredAgentSessionsAsync(TimeSpan sessionTimeout);
    Task CleanupExpiredAgentSessionsAsync();
}

public enum AgentAvailabilityState
{
    Available = 1,
    Busy = 2,
    Away = 3,
    DoNotDisturb = 4,
    Offline = 5
}

public enum TransferPriority
{
    Low = 1,
    Normal = 2,
    High = 3,
    Urgent = 4,
    Emergency = 5
}

public enum TransferReason
{
    UserRequested = 1,
    SystemEscalation = 2,
    ComplexityLimit = 3,
    EmergencyHandoff = 4,
    SpecialistRequired = 5,
    QualityAssurance = 6
}

public class TransferRequest
{
    public int ConversationId { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public string? PreferredDepartment { get; set; }
    public TransferPriority Priority { get; set; } = TransferPriority.Normal;
    public TransferReason Reason { get; set; } = TransferReason.UserRequested;
    public string RequestMessage { get; set; } = string.Empty;
    public Dictionary<string, object> ConversationContext { get; set; } = new();
    public List<string> RequiredSkills { get; set; } = new();
    public bool IsEmergency { get; set; }
}

public class AvailableAgent
{
    public int AgentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public List<string> Skills { get; set; } = new();
    public AgentAvailabilityState State { get; set; }
    public int CurrentWorkload { get; set; }
    public int MaxConcurrentChats { get; set; } = 3;
    public double AvailabilityScore { get; set; }
    public DateTime LastActivity { get; set; }
    public TimeSpan AverageResponseTime { get; set; }
    public string? StatusMessage { get; set; }
}

public class AgentStatus
{
    public int AgentId { get; set; }
    public int TenantId { get; set; }
    public AgentAvailabilityState State { get; set; }
    public string? StatusMessage { get; set; }
    public DateTime LastHeartbeat { get; set; }
    public DateTime SessionStarted { get; set; }
    public List<int> ActiveConversations { get; set; } = new();
    public string Department { get; set; } = string.Empty;
    public List<string> Skills { get; set; } = new();
    public int MaxConcurrentChats { get; set; } = 3;
}

public class AgentWorkload
{
    public int AgentId { get; set; }
    public int ActiveConversations { get; set; }
    public int PendingTasks { get; set; }
    public double UtilizationPercentage { get; set; }
    public TimeSpan AverageResponseTime { get; set; }
    public List<ConversationSummary> ActiveChats { get; set; } = new();
}

public class ConversationSummary
{
    public int ConversationId { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime LastMessageAt { get; set; }
    public string Priority { get; set; } = "Normal";
    public string Status { get; set; } = "Active";
}

public class TransferRouting
{
    public bool CanTransfer { get; set; }
    public AvailableAgent? RecommendedAgent { get; set; }
    public List<AvailableAgent> AvailableAgents { get; set; } = new();
    public string? UnavailabilityReason { get; set; }
    public TimeSpan? EstimatedWaitTime { get; set; }
    public List<string> AlternativeDepartments { get; set; } = new();
    public TransferStrategy RecommendedStrategy { get; set; }
}

public enum TransferStrategy
{
    ImmediateTransfer = 1,
    QueuedTransfer = 2,
    ScheduledCallback = 3,
    EscalateToManager = 4,
    CreateTicket = 5
}

public class AgentAvailabilityService : IAgentAvailabilityService
{
    private readonly HostrDbContext _context;
    private readonly ILogger<AgentAvailabilityService> _logger;
    private readonly IMemoryCache _cache;
    private readonly INotificationService _notificationService;

    // Cache keys
    private const string AGENT_STATUS_CACHE_KEY = "agent_status_{0}_{1}"; // tenantId, agentId
    private const string DEPARTMENT_AGENTS_CACHE_KEY = "department_agents_{0}_{1}"; // tenantId, department
    private const string AGENT_WORKLOAD_CACHE_KEY = "agent_workload_{0}_{1}"; // tenantId, agentId

    // Cache durations
    private static readonly TimeSpan AgentStatusCacheDuration = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan WorkloadCacheDuration = TimeSpan.FromMinutes(5);

    // Session management
    private static readonly TimeSpan DefaultSessionTimeout = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromMinutes(2);

    public AgentAvailabilityService(
        HostrDbContext context,
        ILogger<AgentAvailabilityService> logger,
        IMemoryCache cache,
        INotificationService notificationService)
    {
        _context = context;
        _logger = logger;
        _cache = cache;
        _notificationService = notificationService;
    }

    public async Task<List<AvailableAgent>> GetAvailableAgentsAsync(int tenantId, string? department = null, TransferPriority priority = TransferPriority.Normal)
    {
        try
        {
            using var scope = new TenantScope(_context, tenantId);

            var cacheKey = $"available_agents_{tenantId}_{department}_{priority}";

            if (_cache.TryGetValue(cacheKey, out List<AvailableAgent>? cachedAgents))
            {
                return cachedAgents ?? new List<AvailableAgent>();
            }

            // Get staff members who can handle conversations
            // Note: For now, we'll get all active users. Department filtering can be enhanced later
            var potentialAgents = await _context.Users
                .Where(u => u.IsActive)
                .Select(u => new AvailableAgent
                {
                    AgentId = u.Id,
                    Name = u.UserName ?? u.Email, // Use UserName or Email as display name
                    Email = u.Email ?? "",
                    Department = department ?? "General"
                })
                .ToListAsync();

            var availableAgents = new List<AvailableAgent>();

            foreach (var agent in potentialAgents)
            {
                var status = await GetAgentStatusAsync(tenantId, agent.AgentId);
                var workload = await GetAgentWorkloadAsync(tenantId, agent.AgentId);

                // Check if agent is available based on status and workload
                if (IsAgentAvailable(status, workload, priority))
                {
                    agent.State = status?.State ?? AgentAvailabilityState.Offline;
                    agent.CurrentWorkload = workload.ActiveConversations;
                    agent.AvailabilityScore = CalculateAvailabilityScore(status, workload);
                    agent.LastActivity = status?.LastHeartbeat ?? DateTime.UtcNow.AddHours(-1);
                    agent.AverageResponseTime = workload.AverageResponseTime;
                    agent.StatusMessage = status?.StatusMessage;

                    availableAgents.Add(agent);
                }
            }

            // Sort by availability score (best agents first)
            var sortedAgents = availableAgents
                .OrderByDescending(a => a.AvailabilityScore)
                .ThenBy(a => a.CurrentWorkload)
                .ToList();

            // Cache for a short duration
            _cache.Set(cacheKey, sortedAgents, TimeSpan.FromMinutes(1));

            _logger.LogInformation("Found {Count} available agents for tenant {TenantId}, department: {Department}, priority: {Priority}",
                sortedAgents.Count, tenantId, department ?? "Any", priority);

            return sortedAgents;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available agents for tenant {TenantId}", tenantId);
            return new List<AvailableAgent>();
        }
    }

    public async Task<AgentStatus?> GetAgentStatusAsync(int tenantId, int agentId)
    {
        try
        {
            var cacheKey = string.Format(AGENT_STATUS_CACHE_KEY, tenantId, agentId);

            if (_cache.TryGetValue(cacheKey, out AgentStatus? cachedStatus))
            {
                return cachedStatus;
            }

            using var scope = new TenantScope(_context, tenantId);

            // Get agent from Users table
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == agentId);

            if (user == null)
            {
                return null;
            }

            // Get active conversations assigned to this agent via ConversationTransfer
            var activeConversations = await _context.ConversationTransfers
                .Where(ct => ct.TenantId == tenantId &&
                            ct.ToAgentId == agentId &&
                            ct.Status == "Completed" &&
                            ct.ReleasedAt == null)
                .Select(ct => ct.ConversationId)
                .ToListAsync();

            // Create agent status (in production, this would be stored in a dedicated table)
            var status = new AgentStatus
            {
                AgentId = agentId,
                TenantId = tenantId,
                State = AgentAvailabilityState.Available, // Default - would be managed by agent client
                LastHeartbeat = DateTime.UtcNow, // Would be updated by agent heartbeat
                SessionStarted = DateTime.UtcNow.AddHours(-2), // Default
                ActiveConversations = activeConversations,
                Department = "General", // Would be determined from user/department mapping
                Skills = new List<string> { "general_support" }, // Would be stored in user profile
                MaxConcurrentChats = 3
            };

            // Cache the status
            _cache.Set(cacheKey, status, AgentStatusCacheDuration);

            return status;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting agent status for agent {AgentId} in tenant {TenantId}", agentId, tenantId);
            return null;
        }
    }

    public async Task<bool> UpdateAgentStatusAsync(int tenantId, int agentId, AgentAvailabilityState state, string? statusMessage = null)
    {
        try
        {
            using var scope = new TenantScope(_context, tenantId);

            // In production, this would update a dedicated AgentStatus table
            // For now, we'll update the cache and log the status change

            var cacheKey = string.Format(AGENT_STATUS_CACHE_KEY, tenantId, agentId);
            var existingStatus = await GetAgentStatusAsync(tenantId, agentId);

            if (existingStatus != null)
            {
                existingStatus.State = state;
                existingStatus.StatusMessage = statusMessage;
                existingStatus.LastHeartbeat = DateTime.UtcNow;

                // Update cache
                _cache.Set(cacheKey, existingStatus, AgentStatusCacheDuration);

                // Clear related caches
                ClearAgentRelatedCaches(tenantId, agentId);

                _logger.LogInformation("Updated agent {AgentId} status to {State} for tenant {TenantId}. Message: {Message}",
                    agentId, state, tenantId, statusMessage);

                // Notify about agent status change
                await _notificationService.NotifyAgentStatusChangedAsync(tenantId, agentId, state.ToString(), statusMessage);

                return true;
            }

            _logger.LogWarning("Cannot update status for non-existent agent {AgentId} in tenant {TenantId}", agentId, tenantId);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating agent status for agent {AgentId} in tenant {TenantId}", agentId, tenantId);
            return false;
        }
    }

    public async Task<bool> AssignConversationToAgentAsync(int conversationId, int agentId, TransferReason reason)
    {
        try
        {
            var conversation = await _context.Conversations
                .FirstOrDefaultAsync(c => c.Id == conversationId);

            if (conversation == null)
            {
                _logger.LogWarning("Cannot assign non-existent conversation {ConversationId} to agent {AgentId}",
                    conversationId, agentId);
                return false;
            }

            using var scope = new TenantScope(_context, conversation.TenantId);

            // Check agent availability
            var agentStatus = await GetAgentStatusAsync(conversation.TenantId, agentId);
            var workload = await GetAgentWorkloadAsync(conversation.TenantId, agentId);

            if (!IsAgentAvailable(agentStatus, workload, TransferPriority.Normal))
            {
                _logger.LogWarning("Agent {AgentId} is not available for conversation assignment", agentId);
                return false;
            }

            // Mark conversation as assigned (we'll use the transfer record as the assignment)
            conversation.Status = "AgentAssigned";

            // Create transfer record
            var transferRecord = new ConversationTransfer
            {
                ConversationId = conversationId,
                TenantId = conversation.TenantId,
                FromSystem = true,
                ToAgentId = agentId,
                TransferReason = reason.ToString(),
                TransferredAt = DateTime.UtcNow,
                Status = "Completed"
            };

            _context.ConversationTransfers.Add(transferRecord);
            await _context.SaveChangesAsync();

            // Clear relevant caches
            ClearAgentRelatedCaches(conversation.TenantId, agentId);

            // Notify agent and system about the assignment
            await _notificationService.NotifyConversationAssignedAsync(conversation.TenantId, agentId, conversationId);

            _logger.LogInformation("Assigned conversation {ConversationId} to agent {AgentId} for reason: {Reason}",
                conversationId, agentId, reason);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning conversation {ConversationId} to agent {AgentId}",
                conversationId, agentId);
            return false;
        }
    }

    public async Task<bool> ReleaseConversationFromAgentAsync(int conversationId, string releaseReason)
    {
        try
        {
            var conversation = await _context.Conversations
                .FirstOrDefaultAsync(c => c.Id == conversationId);

            if (conversation == null)
            {
                return false;
            }

            using var scope = new TenantScope(_context, conversation.TenantId);

            // Find active transfer record for this conversation
            var activeTransfer = await _context.ConversationTransfers
                .Where(ct => ct.ConversationId == conversationId &&
                            ct.Status == "Completed" &&
                            ct.ReleasedAt == null)
                .FirstOrDefaultAsync();

            if (activeTransfer == null)
            {
                return false;
            }

            var agentId = activeTransfer.ToAgentId!.Value;

            // Update conversation
            conversation.Status = "Active"; // Return to automated handling

            // Update transfer record
            activeTransfer.Status = "Released";
            activeTransfer.ReleasedAt = DateTime.UtcNow;
            activeTransfer.ReleaseReason = releaseReason;

            await _context.SaveChangesAsync();

            // Clear relevant caches
            ClearAgentRelatedCaches(conversation.TenantId, agentId);

            _logger.LogInformation("Released conversation {ConversationId} from agent {AgentId}: {Reason}",
                conversationId, agentId, releaseReason);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error releasing conversation {ConversationId}", conversationId);
            return false;
        }
    }

    public async Task<AgentWorkload> GetAgentWorkloadAsync(int tenantId, int agentId)
    {
        try
        {
            var cacheKey = string.Format(AGENT_WORKLOAD_CACHE_KEY, tenantId, agentId);

            if (_cache.TryGetValue(cacheKey, out AgentWorkload? cachedWorkload))
            {
                return cachedWorkload!;
            }

            using var scope = new TenantScope(_context, tenantId);

            // Get active conversations via transfer records
            var activeConversations = await _context.ConversationTransfers
                .Include(ct => ct.Conversation)
                .Where(ct => ct.TenantId == tenantId &&
                            ct.ToAgentId == agentId &&
                            ct.Status == "Completed" &&
                            ct.ReleasedAt == null)
                .Select(ct => new ConversationSummary
                {
                    ConversationId = ct.ConversationId,
                    PhoneNumber = ct.Conversation.WaUserPhone,
                    StartedAt = ct.Conversation.CreatedAt,
                    LastMessageAt = ct.TransferredAt, // Use transfer time as last activity
                    Status = ct.Conversation.Status
                })
                .ToListAsync();

            // Get pending tasks
            var pendingTasks = await _context.StaffTasks
                .CountAsync(st => st.TenantId == tenantId &&
                                 st.AssignedToId == agentId &&
                                 st.Status == "Open");

            var workload = new AgentWorkload
            {
                AgentId = agentId,
                ActiveConversations = activeConversations.Count,
                PendingTasks = pendingTasks,
                UtilizationPercentage = CalculateUtilizationPercentage(activeConversations.Count, 3), // Max 3 concurrent
                AverageResponseTime = TimeSpan.FromMinutes(5), // Would be calculated from actual response times
                ActiveChats = activeConversations
            };

            // Cache workload
            _cache.Set(cacheKey, workload, WorkloadCacheDuration);

            return workload;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting agent workload for agent {AgentId} in tenant {TenantId}", agentId, tenantId);
            return new AgentWorkload { AgentId = agentId };
        }
    }

    public async Task<List<AgentStatus>> GetDepartmentAgentStatusAsync(int tenantId, string department)
    {
        try
        {
            var cacheKey = string.Format(DEPARTMENT_AGENTS_CACHE_KEY, tenantId, department);

            if (_cache.TryGetValue(cacheKey, out List<AgentStatus>? cachedStatuses))
            {
                return cachedStatuses ?? new List<AgentStatus>();
            }

            using var scope = new TenantScope(_context, tenantId);

            // Get agents in department
            var departmentAgents = await _context.Users
                .Where(u => u.IsActive)
                .Select(u => u.Id)
                .ToListAsync();

            var agentStatuses = new List<AgentStatus>();

            foreach (var agentId in departmentAgents)
            {
                var status = await GetAgentStatusAsync(tenantId, agentId);
                if (status != null && status.Department == department)
                {
                    agentStatuses.Add(status);
                }
            }

            // Cache the results
            _cache.Set(cacheKey, agentStatuses, AgentStatusCacheDuration);

            return agentStatuses;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting department agent status for department {Department} in tenant {TenantId}",
                department, tenantId);
            return new List<AgentStatus>();
        }
    }

    public async Task<TransferRouting> DetermineOptimalTransferRoutingAsync(int tenantId, TransferRequest request)
    {
        try
        {
            var routing = new TransferRouting();

            // Get available agents
            var availableAgents = await GetAvailableAgentsAsync(tenantId, request.PreferredDepartment, request.Priority);

            if (!availableAgents.Any())
            {
                routing.CanTransfer = false;
                routing.UnavailabilityReason = "No agents currently available";
                routing.RecommendedStrategy = TransferStrategy.CreateTicket;

                // Suggest alternative departments
                if (!string.IsNullOrEmpty(request.PreferredDepartment))
                {
                    var alternativeAgents = await GetAvailableAgentsAsync(tenantId, null, request.Priority);
                    routing.AlternativeDepartments = alternativeAgents
                        .Select(a => a.Department)
                        .Distinct()
                        .Where(d => d != request.PreferredDepartment)
                        .ToList();
                }

                return routing;
            }

            // Find best agent match
            var bestAgent = FindBestAgentMatch(availableAgents, request);

            if (bestAgent != null)
            {
                routing.CanTransfer = true;
                routing.RecommendedAgent = bestAgent;
                routing.AvailableAgents = availableAgents.Take(5).ToList(); // Top 5 alternatives
                routing.EstimatedWaitTime = TimeSpan.FromMinutes(1);
                routing.RecommendedStrategy = request.IsEmergency ?
                    TransferStrategy.ImmediateTransfer :
                    TransferStrategy.ImmediateTransfer;
            }
            else
            {
                routing.CanTransfer = false;
                routing.UnavailabilityReason = "No suitable agents match the requirements";
                routing.RecommendedStrategy = TransferStrategy.QueuedTransfer;
                routing.EstimatedWaitTime = TimeSpan.FromMinutes(10);
            }

            return routing;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error determining optimal transfer routing for tenant {TenantId}", tenantId);
            return new TransferRouting
            {
                CanTransfer = false,
                UnavailabilityReason = "System error during routing analysis",
                RecommendedStrategy = TransferStrategy.CreateTicket
            };
        }
    }

    public async Task RegisterAgentHeartbeatAsync(int tenantId, int agentId)
    {
        try
        {
            var cacheKey = string.Format(AGENT_STATUS_CACHE_KEY, tenantId, agentId);
            var status = await GetAgentStatusAsync(tenantId, agentId);

            if (status != null)
            {
                status.LastHeartbeat = DateTime.UtcNow;
                _cache.Set(cacheKey, status, AgentStatusCacheDuration);
            }

            _logger.LogDebug("Registered heartbeat for agent {AgentId} in tenant {TenantId}", agentId, tenantId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering heartbeat for agent {AgentId} in tenant {TenantId}", agentId, tenantId);
        }
    }

    public async Task<List<AgentStatus>> GetExpiredAgentSessionsAsync(TimeSpan sessionTimeout)
    {
        try
        {
            var expiredSessions = new List<AgentStatus>();
            var cutoffTime = DateTime.UtcNow.Subtract(sessionTimeout);

            // This would typically query a dedicated AgentStatus table
            // For now, we'll check cached statuses
            // In production, implement proper session tracking in database

            _logger.LogDebug("Checking for expired agent sessions older than {CutoffTime}", cutoffTime);

            return expiredSessions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting expired agent sessions");
            return new List<AgentStatus>();
        }
    }

    public async Task CleanupExpiredAgentSessionsAsync()
    {
        try
        {
            var expiredSessions = await GetExpiredAgentSessionsAsync(DefaultSessionTimeout);

            foreach (var session in expiredSessions)
            {
                await UpdateAgentStatusAsync(session.TenantId, session.AgentId,
                    AgentAvailabilityState.Offline, "Session expired");

                // Release any assigned conversations
                var activeConversations = session.ActiveConversations;
                foreach (var conversationId in activeConversations)
                {
                    await ReleaseConversationFromAgentAsync(conversationId, "Agent session expired");
                }
            }

            if (expiredSessions.Any())
            {
                _logger.LogInformation("Cleaned up {Count} expired agent sessions", expiredSessions.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up expired agent sessions");
        }
    }

    #region Private Helper Methods

    private bool IsAgentAvailable(AgentStatus? status, AgentWorkload workload, TransferPriority priority)
    {
        if (status == null)
            return false;

        // Check availability state
        if (status.State == AgentAvailabilityState.Offline ||
            status.State == AgentAvailabilityState.DoNotDisturb)
            return false;

        // Check heartbeat recency
        if (DateTime.UtcNow - status.LastHeartbeat > HeartbeatInterval)
            return false;

        // Check workload capacity
        if (workload.ActiveConversations >= status.MaxConcurrentChats)
        {
            // Only allow emergency transfers to overloaded agents
            return priority == TransferPriority.Emergency;
        }

        // Allow if not at capacity or if high priority
        return status.State == AgentAvailabilityState.Available ||
               (status.State == AgentAvailabilityState.Busy && priority >= TransferPriority.High);
    }

    private double CalculateAvailabilityScore(AgentStatus? status, AgentWorkload workload)
    {
        if (status == null)
            return 0.0;

        double score = 1.0;

        // Penalize based on availability state
        score *= status.State switch
        {
            AgentAvailabilityState.Available => 1.0,
            AgentAvailabilityState.Busy => 0.7,
            AgentAvailabilityState.Away => 0.3,
            AgentAvailabilityState.DoNotDisturb => 0.1,
            AgentAvailabilityState.Offline => 0.0,
            _ => 0.5
        };

        // Penalize based on current workload
        double utilizationPenalty = workload.UtilizationPercentage / 100.0;
        score *= (1.0 - utilizationPenalty * 0.5);

        // Bonus for recent activity
        var timeSinceLastActivity = DateTime.UtcNow - status.LastHeartbeat;
        if (timeSinceLastActivity < TimeSpan.FromMinutes(5))
        {
            score *= 1.1; // 10% bonus for very recent activity
        }

        return Math.Max(0.0, Math.Min(1.0, score));
    }

    private double CalculateUtilizationPercentage(int activeConversations, int maxConcurrent)
    {
        if (maxConcurrent <= 0)
            return 0.0;

        return Math.Min(100.0, (double)activeConversations / maxConcurrent * 100.0);
    }

    private AvailableAgent? FindBestAgentMatch(List<AvailableAgent> availableAgents, TransferRequest request)
    {
        var scoredAgents = availableAgents.Select(agent =>
        {
            double score = agent.AvailabilityScore;

            // Bonus for department match
            if (!string.IsNullOrEmpty(request.PreferredDepartment) &&
                agent.Department.Equals(request.PreferredDepartment, StringComparison.OrdinalIgnoreCase))
            {
                score += 0.2;
            }

            // Bonus for required skills match
            if (request.RequiredSkills.Any())
            {
                var matchingSkills = agent.Skills.Intersect(request.RequiredSkills, StringComparer.OrdinalIgnoreCase).Count();
                var skillMatchRatio = (double)matchingSkills / request.RequiredSkills.Count;
                score += skillMatchRatio * 0.3;
            }

            // Penalty for high workload
            score -= agent.CurrentWorkload * 0.1;

            return new { Agent = agent, Score = score };
        })
        .OrderByDescending(x => x.Score)
        .FirstOrDefault();

        return scoredAgents?.Agent;
    }

    private void ClearAgentRelatedCaches(int tenantId, int agentId)
    {
        var agentStatusKey = string.Format(AGENT_STATUS_CACHE_KEY, tenantId, agentId);
        var workloadKey = string.Format(AGENT_WORKLOAD_CACHE_KEY, tenantId, agentId);

        _cache.Remove(agentStatusKey);
        _cache.Remove(workloadKey);

        // Clear department caches (we don't know which department, so clear pattern-based)
        // In production, use a more sophisticated cache invalidation strategy
    }

    #endregion
}