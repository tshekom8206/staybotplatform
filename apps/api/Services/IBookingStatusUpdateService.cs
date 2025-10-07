namespace Hostr.Api.Services;

public interface IBookingStatusUpdateService
{
    Task UpdateBookingStatusesAsync();
    Task<int> ProcessPendingCheckinsAsync();
    Task<int> ProcessPendingCheckoutsAsync();
    Task<BookingStatusUpdateResult> GetLastUpdateResultAsync();
}

public class BookingStatusUpdateResult
{
    public DateTime ExecutedAt { get; set; }
    public int CheckinsProcessed { get; set; }
    public int CheckoutsProcessed { get; set; }
    public int ErrorsEncountered { get; set; }
    public string? LastError { get; set; }
    public TimeSpan ExecutionDuration { get; set; }
}