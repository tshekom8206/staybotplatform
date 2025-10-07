using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Hostr.Api.Data;

namespace Hostr.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UserActivityController : ControllerBase
{
    private readonly HostrDbContext _context;

    public UserActivityController(HostrDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<object>> GetActivities(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? searchTerm = null,
        [FromQuery] string? action = null,
        [FromQuery] string? entity = null,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        [FromQuery] string sortBy = "CreatedAt",
        [FromQuery] string sortDirection = "desc")
    {
        var tenantId = int.Parse(HttpContext.Items["TenantId"]?.ToString() ?? "0");
        if (tenantId == 0) return Unauthorized();

        var query = _context.AuditLogs
            .Include(a => a.ActorUser)
            .Where(a => a.TenantId == tenantId);

        // Apply filters
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(a =>
                a.Action.Contains(searchTerm) ||
                a.Entity.Contains(searchTerm) ||
                (a.ActorUser != null && a.ActorUser.Email.Contains(searchTerm)));
        }

        if (!string.IsNullOrWhiteSpace(action))
        {
            query = query.Where(a => a.Action == action);
        }

        if (!string.IsNullOrWhiteSpace(entity))
        {
            query = query.Where(a => a.Entity == entity);
        }

        if (dateFrom.HasValue)
        {
            query = query.Where(a => a.CreatedAt >= dateFrom.Value);
        }

        if (dateTo.HasValue)
        {
            query = query.Where(a => a.CreatedAt <= dateTo.Value);
        }

        // Get total count
        var totalCount = await query.CountAsync();

        // Apply sorting
        query = sortDirection.ToLower() == "asc"
            ? query.OrderBy(a => a.CreatedAt)
            : query.OrderByDescending(a => a.CreatedAt);

        // Apply pagination
        var activities = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new
            {
                id = a.Id,
                actorUserId = a.ActorUserId,
                actorUserEmail = a.ActorUser != null ? a.ActorUser.Email : "System",
                action = a.Action,
                entity = a.Entity,
                entityId = a.EntityId,
                details = a.DiffJson,
                createdAt = a.CreatedAt
            })
            .ToListAsync();

        var response = new
        {
            activities = activities,
            totalCount = totalCount,
            totalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
            currentPage = page,
            pageSize = pageSize
        };

        return Ok(response);
    }

    [HttpGet("stats")]
    public async Task<ActionResult<object>> GetActivityStats()
    {
        var tenantId = int.Parse(HttpContext.Items["TenantId"]?.ToString() ?? "0");
        if (tenantId == 0) return Unauthorized();

        var now = DateTime.UtcNow;
        var today = now.Date;
        var weekAgo = now.AddDays(-7);

        var allActivities = await _context.AuditLogs
            .Where(a => a.TenantId == tenantId)
            .ToListAsync();

        var todayActivities = allActivities.Count(a => a.CreatedAt >= today);
        var weekActivities = allActivities.Count(a => a.CreatedAt >= weekAgo);

        var activitiesByAction = allActivities
            .GroupBy(a => a.Action)
            .ToDictionary(g => g.Key, g => g.Count());

        var activitiesByEntity = allActivities
            .GroupBy(a => a.Entity)
            .ToDictionary(g => g.Key, g => g.Count());

        var topActiveUsers = await _context.AuditLogs
            .Where(a => a.TenantId == tenantId && a.ActorUserId != null)
            .GroupBy(a => new { a.ActorUserId, a.ActorUser!.Email })
            .Select(g => new
            {
                userId = g.Key.ActorUserId,
                userEmail = g.Key.Email,
                activityCount = g.Count()
            })
            .OrderByDescending(x => x.activityCount)
            .Take(10)
            .ToListAsync();

        var totalActiveUsers = await _context.AuditLogs
            .Where(a => a.TenantId == tenantId && a.ActorUserId != null)
            .Select(a => a.ActorUserId)
            .Distinct()
            .CountAsync();

        var stats = new
        {
            totalActivities = allActivities.Count,
            totalActiveUsers = totalActiveUsers,
            todayActivities = todayActivities,
            weekActivities = weekActivities,
            activitiesByAction = activitiesByAction,
            activitiesByEntity = activitiesByEntity,
            topActiveUsers = topActiveUsers,
            recentActivitySummary = new object[0]
        };

        return Ok(stats);
    }

    [HttpGet("{userId}")]
    public async Task<ActionResult<object>> GetUserActivities(
        int userId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var tenantId = int.Parse(HttpContext.Items["TenantId"]?.ToString() ?? "0");
        if (tenantId == 0) return Unauthorized();

        var query = _context.AuditLogs
            .Include(a => a.ActorUser)
            .Where(a => a.TenantId == tenantId && a.ActorUserId == userId);

        var totalCount = await query.CountAsync();

        var activities = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new
            {
                id = a.Id,
                actorUserId = a.ActorUserId,
                actorUserEmail = a.ActorUser != null ? a.ActorUser.Email : "System",
                action = a.Action,
                entity = a.Entity,
                entityId = a.EntityId,
                details = a.DiffJson,
                createdAt = a.CreatedAt
            })
            .ToListAsync();

        var response = new
        {
            activities = activities,
            totalCount = totalCount,
            totalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
            currentPage = page,
            pageSize = pageSize
        };

        return Ok(response);
    }

    [HttpGet("actions")]
    public async Task<ActionResult<string[]>> GetAvailableActions()
    {
        var tenantId = int.Parse(HttpContext.Items["TenantId"]?.ToString() ?? "0");
        if (tenantId == 0) return Unauthorized();

        var actions = await _context.AuditLogs
            .Where(a => a.TenantId == tenantId)
            .Select(a => a.Action)
            .Distinct()
            .ToListAsync();

        return Ok(actions);
    }

    [HttpGet("entities")]
    public async Task<ActionResult<string[]>> GetAvailableEntities()
    {
        var tenantId = int.Parse(HttpContext.Items["TenantId"]?.ToString() ?? "0");
        if (tenantId == 0) return Unauthorized();

        var entities = await _context.AuditLogs
            .Where(a => a.TenantId == tenantId)
            .Select(a => a.Entity)
            .Distinct()
            .ToListAsync();

        return Ok(entities);
    }
}