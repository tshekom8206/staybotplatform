using Quartz;
using Hostr.Api.Services;

namespace Hostr.Api.Jobs;

/// <summary>
/// Quartz job that processes due scheduled messages every 5 minutes
/// </summary>
[DisallowConcurrentExecution]
public class ProactiveMessageJob : IJob
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<ProactiveMessageJob> _logger;

    public ProactiveMessageJob(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<ProactiveMessageJob> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("ProactiveMessageJob started at {Time}", DateTime.UtcNow);

        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var proactiveService = scope.ServiceProvider.GetRequiredService<IProactiveMessageService>();

            await proactiveService.ProcessDueMessagesAsync();

            _logger.LogInformation("ProactiveMessageJob completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ProactiveMessageJob failed");
            throw; // Quartz will handle retry logic
        }
    }
}
