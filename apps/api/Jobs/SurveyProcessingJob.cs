using Quartz;
using Hostr.Api.Services;

namespace Hostr.Api.Jobs;

public class SurveyProcessingJob : IJob
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<SurveyProcessingJob> _logger;

    public SurveyProcessingJob(IServiceScopeFactory serviceScopeFactory, ILogger<SurveyProcessingJob> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("Starting survey processing job");

        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var orchestrationService = scope.ServiceProvider.GetRequiredService<ISurveyOrchestrationService>();

            await orchestrationService.ProcessCheckoutsForSurveysAsync();

            _logger.LogInformation("Survey processing job completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing survey processing job");
        }
    }
}