using Quartz;
using Hostr.Api.Services;

namespace Hostr.Api.Jobs;

public class GuestMetricsJob : IJob
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<GuestMetricsJob> _logger;

    public GuestMetricsJob(IServiceScopeFactory serviceScopeFactory, ILogger<GuestMetricsJob> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("Starting guest metrics update job");

        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var guestLifecycleService = scope.ServiceProvider.GetRequiredService<IGuestLifecycleService>();

            await guestLifecycleService.UpdateAllGuestMetricsAsync();

            _logger.LogInformation("Guest metrics update job completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing guest metrics update job");
        }
    }
}