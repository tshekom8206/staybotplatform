using Microsoft.EntityFrameworkCore;
using Hostr.Api.Data;
using Hostr.Api.Models;
using System.Text.Json;

namespace Hostr.Api.Services;

public interface IBookingService
{
    Task<Booking?> GetBookingAsync(int tenantId, int bookingId);
    Task<List<Booking>> GetGuestBookingsAsync(int tenantId, string guestPhone, bool activeOnly = false);
    Task<BookingModification> CreateModificationRequestAsync(
        int tenantId,
        int bookingId,
        int? conversationId,
        string modificationType,
        string requestDetails,
        string? requestedBy = null,
        DateOnly? newCheckinDate = null,
        DateOnly? newCheckoutDate = null,
        string? newGuestName = null,
        string? newPhone = null,
        decimal? feeDifference = null,
        string? reason = null);
    Task<bool> ProcessModificationRequestAsync(
        int tenantId,
        int modificationId,
        bool approved,
        int processedBy,
        string? rejectionReason = null,
        string? staffNotes = null);
    Task<List<BookingModification>> GetPendingModificationsAsync(int tenantId);
    Task<(bool IsModificationRequest, string ModificationType, object? ParsedDetails)> DetectModificationRequestAsync(
        TenantContext tenantContext,
        string message,
        string guestPhone);
    Task RecordBookingChangeAsync(
        int tenantId,
        int bookingId,
        int? modificationId,
        string changeType,
        string? oldValue,
        string? newValue,
        string? changedBy,
        string? changeReason = null);
}

public class BookingService : IBookingService
{
    private readonly HostrDbContext _context;
    private readonly ILogger<BookingService> _logger;

    // Keywords for detecting different types of modification requests
    private static readonly Dictionary<string, string[]> ModificationKeywords = new()
    {
        {"date_change", new[] {"change dates", "modify dates", "different dates", "reschedule", "move booking", "change checkin", "change checkout"}},
        {"guest_change", new[] {"change name", "different guest", "modify guest", "update name", "wrong name"}},
        {"cancellation", new[] {"cancel", "cancelled", "refund", "can't come", "cannot come", "emergency cancellation"}},
        {"extension", new[] {"extend", "stay longer", "extra night", "additional days", "checkout later"}}
    };

    public BookingService(HostrDbContext context, ILogger<BookingService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Booking?> GetBookingAsync(int tenantId, int bookingId)
    {
        using var scope = new TenantScope(_context, tenantId);
        
        return await _context.Bookings
            .Include(b => b.Ratings)
            .FirstOrDefaultAsync(b => b.Id == bookingId);
    }

    public async Task<List<Booking>> GetGuestBookingsAsync(int tenantId, string guestPhone, bool activeOnly = false)
    {
        using var scope = new TenantScope(_context, tenantId);
        
        var query = _context.Bookings
            .Where(b => b.Phone == guestPhone);
            
        if (activeOnly)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            query = query.Where(b => b.Status == "Confirmed" || b.Status == "CheckedIn" && 
                               (b.CheckinDate <= today && b.CheckoutDate >= today));
        }
        
        return await query
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();
    }

    public async Task<BookingModification> CreateModificationRequestAsync(
        int tenantId,
        int bookingId,
        int? conversationId,
        string modificationType,
        string requestDetails,
        string? requestedBy = null,
        DateOnly? newCheckinDate = null,
        DateOnly? newCheckoutDate = null,
        string? newGuestName = null,
        string? newPhone = null,
        decimal? feeDifference = null,
        string? reason = null)
    {
        using var scope = new TenantScope(_context, tenantId);
        
        // Get original booking details
        var booking = await _context.Bookings.FindAsync(bookingId);
        if (booking == null || booking.TenantId != tenantId)
            throw new ArgumentException("Booking not found");

        var modification = new BookingModification
        {
            TenantId = tenantId,
            BookingId = bookingId,
            ConversationId = conversationId,
            ModificationType = modificationType,
            RequestDetails = requestDetails,
            
            // Original values
            OriginalCheckinDate = booking.CheckinDate,
            OriginalCheckoutDate = booking.CheckoutDate,
            OriginalGuestName = booking.GuestName,
            OriginalPhone = booking.Phone,
            
            // Requested changes
            NewCheckinDate = newCheckinDate,
            NewCheckoutDate = newCheckoutDate,
            NewGuestName = newGuestName,
            NewPhone = newPhone,
            
            RequestedBy = requestedBy,
            FeeDifference = feeDifference,
            Reason = reason,
            Status = "Pending",
            RequiresApproval = DetermineIfApprovalRequired(modificationType, booking, newCheckinDate, newCheckoutDate),
            RequestedAt = DateTime.UtcNow
        };

        _context.BookingModifications.Add(modification);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Booking modification request created: {ModificationId} - Type: {Type} for Booking {BookingId}", 
            modification.Id, modificationType, bookingId);

        return modification;
    }

    public async Task<bool> ProcessModificationRequestAsync(
        int tenantId,
        int modificationId,
        bool approved,
        int processedBy,
        string? rejectionReason = null,
        string? staffNotes = null)
    {
        using var scope = new TenantScope(_context, tenantId);
        
        var modification = await _context.BookingModifications
            .Include(m => m.Booking)
            .FirstOrDefaultAsync(m => m.Id == modificationId && m.TenantId == tenantId);
            
        if (modification == null)
            return false;

        modification.Status = approved ? "Approved" : "Rejected";
        modification.RejectionReason = rejectionReason;
        modification.StaffNotes = staffNotes;
        modification.ProcessedAt = DateTime.UtcNow;
        modification.ProcessedBy = processedBy;

        if (approved)
        {
            await ApplyApprovedModificationAsync(modification);
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Booking modification {Status}: {ModificationId} by staff {StaffId}", 
            modification.Status, modificationId, processedBy);

        return true;
    }

    public async Task<List<BookingModification>> GetPendingModificationsAsync(int tenantId)
    {
        using var scope = new TenantScope(_context, tenantId);
        
        return await _context.BookingModifications
            .Include(m => m.Booking)
            .Include(m => m.Conversation)
            .Where(m => m.Status == "Pending")
            .OrderBy(m => m.RequestedAt)
            .ToListAsync();
    }

    public async Task<(bool IsModificationRequest, string ModificationType, object? ParsedDetails)> DetectModificationRequestAsync(
        TenantContext tenantContext,
        string message,
        string guestPhone)
    {
        try
        {
            using var scope = new TenantScope(_context, tenantContext.TenantId);
            
            var messageLower = message.ToLower();
            
            // Check if guest has any bookings
            var hasBookings = await _context.Bookings
                .AnyAsync(b => b.Phone == guestPhone && 
                         (b.Status == "Confirmed" || b.Status == "CheckedIn"));
                         
            if (!hasBookings)
                return (false, "", null);

            // Detect modification type based on keywords
            foreach (var modificationKv in ModificationKeywords)
            {
                if (modificationKv.Value.Any(keyword => messageLower.Contains(keyword.ToLower())))
                {
                    var modificationType = modificationKv.Key;
                    var parsedDetails = await ParseModificationDetailsAsync(modificationType, message, messageLower);
                    
                    _logger.LogInformation("Booking modification detected: {Type} for guest {Phone} - Message: {Message}", 
                        modificationType, guestPhone, message);
                        
                    return (true, modificationType, parsedDetails);
                }
            }

            return (false, "", null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting booking modification request");
            return (false, "", null);
        }
    }

    public async Task RecordBookingChangeAsync(
        int tenantId,
        int bookingId,
        int? modificationId,
        string changeType,
        string? oldValue,
        string? newValue,
        string? changedBy,
        string? changeReason = null)
    {
        using var scope = new TenantScope(_context, tenantId);
        
        var changeHistory = new BookingChangeHistory
        {
            TenantId = tenantId,
            BookingId = bookingId,
            BookingModificationId = modificationId,
            ChangeType = changeType,
            OldValue = oldValue,
            NewValue = newValue,
            ChangedBy = changedBy,
            ChangeReason = changeReason,
            ChangedAt = DateTime.UtcNow
        };

        _context.BookingChangeHistory.Add(changeHistory);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Booking change recorded: {ChangeType} for Booking {BookingId} by {ChangedBy}", 
            changeType, bookingId, changedBy);
    }

    private async Task ApplyApprovedModificationAsync(BookingModification modification)
    {
        var booking = modification.Booking;
        
        switch (modification.ModificationType)
        {
            case "date_change":
                if (modification.NewCheckinDate.HasValue)
                {
                    await RecordBookingChangeAsync(
                        modification.TenantId,
                        booking.Id,
                        modification.Id,
                        "checkin_date",
                        booking.CheckinDate.ToString(),
                        modification.NewCheckinDate.Value.ToString(),
                        "Staff");
                    booking.CheckinDate = modification.NewCheckinDate.Value;
                }
                
                if (modification.NewCheckoutDate.HasValue)
                {
                    await RecordBookingChangeAsync(
                        modification.TenantId,
                        booking.Id,
                        modification.Id,
                        "checkout_date",
                        booking.CheckoutDate.ToString(),
                        modification.NewCheckoutDate.Value.ToString(),
                        "Staff");
                    booking.CheckoutDate = modification.NewCheckoutDate.Value;
                }
                break;
                
            case "guest_change":
                if (!string.IsNullOrEmpty(modification.NewGuestName))
                {
                    await RecordBookingChangeAsync(
                        modification.TenantId,
                        booking.Id,
                        modification.Id,
                        "guest_name",
                        booking.GuestName,
                        modification.NewGuestName,
                        "Staff");
                    booking.GuestName = modification.NewGuestName;
                }
                
                if (!string.IsNullOrEmpty(modification.NewPhone))
                {
                    await RecordBookingChangeAsync(
                        modification.TenantId,
                        booking.Id,
                        modification.Id,
                        "phone",
                        booking.Phone,
                        modification.NewPhone,
                        "Staff");
                    booking.Phone = modification.NewPhone;
                }
                break;
                
            case "cancellation":
                await RecordBookingChangeAsync(
                    modification.TenantId,
                    booking.Id,
                    modification.Id,
                    "status",
                    booking.Status,
                    "Cancelled",
                    "Staff",
                    modification.Reason);
                booking.Status = "Cancelled";
                break;
        }
    }

    private bool DetermineIfApprovalRequired(string modificationType, Booking booking, DateOnly? newCheckinDate, DateOnly? newCheckoutDate)
    {
        // Simple rules - can be made configurable via database
        return modificationType switch
        {
            "cancellation" => true, // Always require approval for cancellations
            "date_change" => IsDateChangeSignificant(booking, newCheckinDate, newCheckoutDate),
            "guest_change" => true, // Require approval for guest changes
            "extension" => false, // Extensions can be auto-approved
            _ => true
        };
    }

    private bool IsDateChangeSignificant(Booking booking, DateOnly? newCheckinDate, DateOnly? newCheckoutDate)
    {
        // Require approval if changing dates within 48 hours of checkin
        var checkinThreshold = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2));
        
        if (booking.CheckinDate <= checkinThreshold)
            return true;
            
        // Require approval if extending stay by more than 2 days
        if (newCheckoutDate.HasValue && newCheckoutDate.Value.DayNumber - booking.CheckoutDate.DayNumber > 2)
            return true;
            
        return false;
    }

    private async Task<object?> ParseModificationDetailsAsync(string modificationType, string originalMessage, string messageLower)
    {
        // Simple parsing logic - can be enhanced with more sophisticated NLP
        return modificationType switch
        {
            "date_change" => ParseDateChangeDetails(originalMessage, messageLower),
            "guest_change" => ParseGuestChangeDetails(originalMessage, messageLower),
            "cancellation" => ParseCancellationDetails(originalMessage, messageLower),
            "extension" => ParseExtensionDetails(originalMessage, messageLower),
            _ => null
        };
    }

    private object ParseDateChangeDetails(string originalMessage, string messageLower)
    {
        // Look for date patterns in the message
        // This is a simplified version - in practice, you'd use more sophisticated date parsing
        return new
        {
            OriginalMessage = originalMessage,
            DetectedDates = ExtractDatesFromMessage(originalMessage),
            RequiresManualReview = true
        };
    }

    private object ParseGuestChangeDetails(string originalMessage, string messageLower)
    {
        return new
        {
            OriginalMessage = originalMessage,
            PossibleNewName = ExtractNameFromMessage(originalMessage),
            RequiresManualReview = true
        };
    }

    private object ParseCancellationDetails(string originalMessage, string messageLower)
    {
        var urgency = messageLower.Contains("emergency") ? "Emergency" : "Standard";
        
        return new
        {
            OriginalMessage = originalMessage,
            CancellationReason = ExtractCancellationReason(messageLower),
            Urgency = urgency,
            RequiresManualReview = true
        };
    }

    private object ParseExtensionDetails(string originalMessage, string messageLower)
    {
        return new
        {
            OriginalMessage = originalMessage,
            RequestedExtension = ExtractExtensionDuration(messageLower),
            RequiresManualReview = false // Extensions might be auto-processable
        };
    }

    private List<string> ExtractDatesFromMessage(string message)
    {
        // Simplified date extraction - in practice, use a proper date parsing library
        var dates = new List<string>();
        // TODO: Implement proper date extraction logic
        return dates;
    }

    private string? ExtractNameFromMessage(string message)
    {
        // Simplified name extraction
        // TODO: Implement proper name extraction logic
        return null;
    }

    private string ExtractCancellationReason(string messageLower)
    {
        if (messageLower.Contains("emergency"))
            return "Emergency";
        if (messageLower.Contains("sick") || messageLower.Contains("illness"))
            return "Illness";
        if (messageLower.Contains("work") || messageLower.Contains("business"))
            return "Work/Business";
        
        return "Other";
    }

    private string ExtractExtensionDuration(string messageLower)
    {
        if (messageLower.Contains("1 night") || messageLower.Contains("one night"))
            return "1 night";
        if (messageLower.Contains("2 night") || messageLower.Contains("two night"))
            return "2 nights";
        if (messageLower.Contains("week"))
            return "1 week";
            
        return "Unknown duration";
    }
}