using Microsoft.EntityFrameworkCore;
using Quartz;
using Hostr.Workers.Data;
using Hostr.Workers.Models;

namespace Hostr.Workers.Services;

public interface IAnalyticsService
{
    Task<UsageDaily> CalculateDailyUsageAsync(int tenantId, DateOnly date);
}

public class AnalyticsService : IAnalyticsService
{
    private readonly WorkersDbContext _context;
    private readonly ILogger<AnalyticsService> _logger;

    public AnalyticsService(WorkersDbContext context, ILogger<AnalyticsService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<UsageDaily> CalculateDailyUsageAsync(int tenantId, DateOnly date)
    {
        var startDate = date.ToDateTime(TimeOnly.MinValue);
        var endDate = date.ToDateTime(TimeOnly.MaxValue);

        var messages = await _context.Messages
            .Where(m => m.TenantId == tenantId && 
                       m.CreatedAt >= startDate && 
                       m.CreatedAt <= endDate)
            .ToListAsync();

        var messagesIn = messages.Count(m => m.Direction == "Inbound");
        var messagesOut = messages.Count(m => m.Direction == "Outbound");
        var tokensIn = messages.Where(m => m.TokensPrompt.HasValue).Sum(m => m.TokensPrompt!.Value);
        var tokensOut = messages.Where(m => m.TokensCompletion.HasValue).Sum(m => m.TokensCompletion!.Value);

        // TODO: Calculate upsell revenue from completed bookings/orders
        var upsellRevenue = 0;

        return new UsageDaily
        {
            TenantId = tenantId,
            Date = date,
            MessagesIn = messagesIn,
            MessagesOut = messagesOut,
            TokensIn = tokensIn,
            TokensOut = tokensOut,
            UpsellRevenueCents = upsellRevenue,
            CreatedAt = DateTime.UtcNow
        };
    }
}

[DisallowConcurrentExecution]
public class AnalyticsRollupWorker : IJob
{
    private readonly WorkersDbContext _context;
    private readonly IAnalyticsService _analyticsService;
    private readonly ILogger<AnalyticsRollupWorker> _logger;

    public AnalyticsRollupWorker(
        WorkersDbContext context,
        IAnalyticsService analyticsService,
        ILogger<AnalyticsRollupWorker> logger)
    {
        _context = context;
        _analyticsService = analyticsService;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("Starting analytics rollup worker");

        try
        {
            var yesterday = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));

            var activeTenants = await _context.Tenants
                .Where(t => t.Status == "Active")
                .ToListAsync();

            var processedCount = 0;

            foreach (var tenant in activeTenants)
            {
                // Check if usage data already exists for yesterday
                var existingUsage = await _context.UsageDaily
                    .FirstOrDefaultAsync(u => u.TenantId == tenant.Id && u.Date == yesterday);

                if (existingUsage == null)
                {
                    // Calculate and save usage data
                    var usage = await _analyticsService.CalculateDailyUsageAsync(tenant.Id, yesterday);
                    _context.UsageDaily.Add(usage);
                    processedCount++;
                }
                else
                {
                    // Update existing usage data
                    var updatedUsage = await _analyticsService.CalculateDailyUsageAsync(tenant.Id, yesterday);
                    existingUsage.MessagesIn = updatedUsage.MessagesIn;
                    existingUsage.MessagesOut = updatedUsage.MessagesOut;
                    existingUsage.TokensIn = updatedUsage.TokensIn;
                    existingUsage.TokensOut = updatedUsage.TokensOut;
                    existingUsage.UpsellRevenueCents = updatedUsage.UpsellRevenueCents;
                }
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Analytics rollup completed. Processed {ProcessedCount} tenants for date {Date}", 
                processedCount, yesterday);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in analytics rollup worker");
        }
    }
}