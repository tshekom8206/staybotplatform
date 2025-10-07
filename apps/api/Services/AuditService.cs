using Hostr.Api.Data;
using Hostr.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Hostr.Api.Services;

public interface IAuditService
{
    Task LogAsync(string action, string entity, int? entityId = null, string? details = null);
    Task LogAsync(int tenantId, int? userId, string action, string entity, int? entityId = null, string? details = null);
}

public class AuditService : IAuditService
{
    private readonly HostrDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditService(HostrDbContext context, IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task LogAsync(string action, string entity, int? entityId = null, string? details = null)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null) return;

        var tenantId = int.Parse(httpContext.Items["TenantId"]?.ToString() ?? "0");
        if (tenantId == 0) return;

        var userIdClaim = httpContext.User?.FindFirst("sub")?.Value;
        int? userId = userIdClaim != null ? int.Parse(userIdClaim) : null;

        await LogAsync(tenantId, userId, action, entity, entityId, details);
    }

    public async Task LogAsync(int tenantId, int? userId, string action, string entity, int? entityId = null, string? details = null)
    {
        try
        {
            var auditLog = new AuditLog
            {
                TenantId = tenantId,
                ActorUserId = userId,
                Action = action,
                Entity = entity,
                EntityId = entityId,
                DiffJson = details,
                CreatedAt = DateTime.UtcNow
            };

            _context.AuditLogs.Add(auditLog);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Log error but don't throw - audit logging shouldn't break the app
            Console.WriteLine($"Audit logging failed: {ex.Message}");
        }
    }
}
