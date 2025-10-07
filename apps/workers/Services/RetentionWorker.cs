using Microsoft.EntityFrameworkCore;
using Quartz;
using Hostr.Workers.Data;

namespace Hostr.Workers.Services;

public interface IRetentionService
{
    Task<int> PurgeOldDataAsync(int tenantId, int retentionDays);
}

public class RetentionService : IRetentionService
{
    private readonly WorkersDbContext _context;
    private readonly ILogger<RetentionService> _logger;

    public RetentionService(WorkersDbContext context, ILogger<RetentionService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<int> PurgeOldDataAsync(int tenantId, int retentionDays)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);
        var totalPurged = 0;

        try
        {
            // Purge old messages (POPIA compliance)
            var oldMessages = await _context.Messages
                .Where(m => m.TenantId == tenantId && m.CreatedAt < cutoffDate)
                .ToListAsync();

            _context.Messages.RemoveRange(oldMessages);
            totalPurged += oldMessages.Count;

            // Purge old conversations that have no recent messages
            var oldConversations = await _context.Conversations
                .Where(c => c.TenantId == tenantId && 
                           c.CreatedAt < cutoffDate &&
                           !_context.Messages.Any(m => m.ConversationId == c.Id && m.CreatedAt >= cutoffDate))
                .ToListAsync();

            _context.Conversations.RemoveRange(oldConversations);
            totalPurged += oldConversations.Count;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Purged {MessageCount} messages and {ConversationCount} conversations for tenant {TenantId}", 
                oldMessages.Count, oldConversations.Count, tenantId);

            return totalPurged;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error purging data for tenant {TenantId}", tenantId);
            return 0;
        }
    }
}

[DisallowConcurrentExecution]
public class RetentionWorker : IJob
{
    private readonly WorkersDbContext _context;
    private readonly IRetentionService _retentionService;
    private readonly ILogger<RetentionWorker> _logger;

    public RetentionWorker(
        WorkersDbContext context,
        IRetentionService retentionService,
        ILogger<RetentionWorker> logger)
    {
        _context = context;
        _retentionService = retentionService;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("Starting retention worker");

        try
        {
            var tenants = await _context.Tenants
                .Where(t => t.Status == "Active" && t.RetentionDays > 0)
                .ToListAsync();

            var totalPurged = 0;

            foreach (var tenant in tenants)
            {
                var purged = await _retentionService.PurgeOldDataAsync(tenant.Id, tenant.RetentionDays);
                totalPurged += purged;
            }

            _logger.LogInformation("Retention worker completed. Purged {TotalRecords} records across {TenantCount} tenants", 
                totalPurged, tenants.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in retention worker");
        }
    }
}