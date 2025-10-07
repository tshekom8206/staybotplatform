using Quartz;
using Hostr.Api.Services;

namespace Hostr.Api.Jobs;

public class BookingStatusUpdateJob : IJob
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<BookingStatusUpdateJob> _logger;

    public BookingStatusUpdateJob(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<BookingStatusUpdateJob> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("Starting booking status update job");

        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var bookingStatusService = scope.ServiceProvider.GetRequiredService<IBookingStatusUpdateService>();

            await bookingStatusService.UpdateBookingStatusesAsync();

            var result = await bookingStatusService.GetLastUpdateResultAsync();

            if (result.CheckinsProcessed > 0 || result.CheckoutsProcessed > 0)
            {
                _logger.LogInformation("Booking status update completed: {CheckinsProcessed} check-ins, {CheckoutsProcessed} check-outs, Duration: {Duration}ms",
                    result.CheckinsProcessed, result.CheckoutsProcessed, result.ExecutionDuration.TotalMilliseconds);
            }
            else
            {
                _logger.LogDebug("No booking status updates needed");
            }

            if (result.ErrorsEncountered > 0)
            {
                _logger.LogWarning("Booking status update completed with {ErrorCount} errors. Last error: {LastError}",
                    result.ErrorsEncountered, result.LastError);
            }

            _logger.LogInformation("Booking status update job completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Booking status update job failed");
            throw; // Re-throw to let Quartz handle job failure
        }
    }
}