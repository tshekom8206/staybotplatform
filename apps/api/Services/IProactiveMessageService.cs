using Hostr.Api.Models;

namespace Hostr.Api.Services;

public interface IProactiveMessageService
{
    /// <summary>
    /// Schedule all proactive messages for a new booking
    /// </summary>
    Task ScheduleMessagesForBookingAsync(int tenantId, Booking booking);

    /// <summary>
    /// Cancel all pending messages for a booking (when booking is cancelled or dates change)
    /// </summary>
    Task CancelMessagesForBookingAsync(int bookingId);

    /// <summary>
    /// Reschedule messages when booking dates are modified
    /// </summary>
    Task RescheduleMessagesForBookingAsync(int tenantId, Booking booking);

    /// <summary>
    /// Schedule Welcome Settled message after actual check-in
    /// Called when booking status changes to CheckedIn
    /// </summary>
    Task ScheduleWelcomeSettledAsync(Booking booking);

    /// <summary>
    /// Process and send all due scheduled messages
    /// Called by the Quartz job every 5 minutes
    /// </summary>
    Task ProcessDueMessagesAsync();

    /// <summary>
    /// Get tenant's proactive message settings, creating defaults if not exists
    /// </summary>
    Task<ProactiveMessageSettings> GetOrCreateSettingsAsync(int tenantId);

    /// <summary>
    /// Update tenant's proactive message settings
    /// </summary>
    Task<ProactiveMessageSettings> UpdateSettingsAsync(int tenantId, ProactiveMessageSettings settings);
}
