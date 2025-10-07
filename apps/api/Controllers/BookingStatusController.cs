using Microsoft.AspNetCore.Mvc;
using Hostr.Api.Services;

namespace Hostr.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class BookingStatusController : ControllerBase
{
    private readonly IBookingStatusUpdateService _bookingStatusService;
    private readonly ILogger<BookingStatusController> _logger;

    public BookingStatusController(
        IBookingStatusUpdateService bookingStatusService,
        ILogger<BookingStatusController> logger)
    {
        _bookingStatusService = bookingStatusService;
        _logger = logger;
    }

    /// <summary>
    /// Manually trigger booking status update process
    /// </summary>
    [HttpPost("update")]
    public async Task<IActionResult> UpdateBookingStatuses()
    {
        try
        {
            _logger.LogInformation("Manual booking status update triggered");

            await _bookingStatusService.UpdateBookingStatusesAsync();
            var result = await _bookingStatusService.GetLastUpdateResultAsync();

            return Ok(new
            {
                success = true,
                message = "Booking status update completed",
                checkinsProcessed = result.CheckinsProcessed,
                checkoutsProcessed = result.CheckoutsProcessed,
                executionTime = result.ExecutionDuration.TotalMilliseconds,
                errors = result.ErrorsEncountered,
                lastError = result.LastError
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during manual booking status update");
            return StatusCode(500, new
            {
                success = false,
                message = "Failed to update booking statuses",
                error = ex.Message
            });
        }
    }

    /// <summary>
    /// Get the result of the last booking status update
    /// </summary>
    [HttpGet("last-result")]
    public async Task<IActionResult> GetLastUpdateResult()
    {
        try
        {
            var result = await _bookingStatusService.GetLastUpdateResultAsync();

            return Ok(new
            {
                executedAt = result.ExecutedAt,
                checkinsProcessed = result.CheckinsProcessed,
                checkoutsProcessed = result.CheckoutsProcessed,
                executionDuration = result.ExecutionDuration.TotalMilliseconds,
                errorsEncountered = result.ErrorsEncountered,
                lastError = result.LastError
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving last update result");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Process pending check-ins only
    /// </summary>
    [HttpPost("process-checkins")]
    public async Task<IActionResult> ProcessPendingCheckins()
    {
        try
        {
            var processed = await _bookingStatusService.ProcessPendingCheckinsAsync();

            return Ok(new
            {
                success = true,
                message = $"Processed {processed} check-ins",
                checkinsProcessed = processed
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing pending check-ins");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Process pending check-outs only
    /// </summary>
    [HttpPost("process-checkouts")]
    public async Task<IActionResult> ProcessPendingCheckouts()
    {
        try
        {
            var processed = await _bookingStatusService.ProcessPendingCheckoutsAsync();

            return Ok(new
            {
                success = true,
                message = $"Processed {processed} check-outs",
                checkoutsProcessed = processed
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing pending check-outs");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}