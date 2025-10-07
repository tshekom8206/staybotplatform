using Hostr.Api.Models;
using Hostr.Api.Data;
using System.Text;

namespace Hostr.Api.Services;

public interface IEmergencyContactService
{
    Task<EmergencyContactResult> NotifyEmergencyServicesAsync(
        EmergencyIncident incident,
        EmergencyProtocol protocol,
        List<EmergencyContact> contacts);

    Task<bool> SendEmergencySMSAsync(
        string phoneNumber,
        string message,
        string emergencyType);

    Task<bool> PlaceEmergencyCallAsync(
        string phoneNumber,
        string message,
        string emergencyType);

    Task<EmergencyContactAttempt> LogContactAttemptAsync(
        int incidentId,
        string contactMethod,
        bool success,
        string details);
}

public class EmergencyContact
{
    public string Name { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string ContactType { get; set; } = "SMS"; // SMS, Voice, Email
    public int Priority { get; set; } = 1;
}

public class EmergencyContactResult
{
    public bool Success { get; set; }
    public List<ContactAttempt> Attempts { get; set; } = new();
    public string? FailureReason { get; set; }
    public DateTime AttemptedAt { get; set; } = DateTime.UtcNow;
}

public class ContactAttempt
{
    public string ContactName { get; set; } = string.Empty;
    public string ContactMethod { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime AttemptedAt { get; set; } = DateTime.UtcNow;
}

public class EmergencyContactAttempt
{
    public int Id { get; set; }
    public int IncidentId { get; set; }
    public string ContactMethod { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Details { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class EmergencyContactService : IEmergencyContactService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmergencyContactService> _logger;
    private readonly IEmailService _emailService;
    private readonly HttpClient _httpClient;
    private readonly HostrDbContext _context;

    public EmergencyContactService(
        IConfiguration configuration,
        ILogger<EmergencyContactService> logger,
        IEmailService emailService,
        HttpClient httpClient,
        HostrDbContext context)
    {
        _configuration = configuration;
        _logger = logger;
        _emailService = emailService;
        _httpClient = httpClient;
        _context = context;
    }

    public async Task<EmergencyContactResult> NotifyEmergencyServicesAsync(
        EmergencyIncident incident,
        EmergencyProtocol protocol,
        List<EmergencyContact> contacts)
    {
        var result = new EmergencyContactResult
        {
            AttemptedAt = DateTime.UtcNow
        };

        if (!contacts.Any())
        {
            result.Success = false;
            result.FailureReason = "No emergency contacts configured";
            _logger.LogWarning("No emergency contacts configured for incident {IncidentId}", incident.Id);
            return result;
        }

        // Sort contacts by priority
        var sortedContacts = contacts.OrderBy(c => c.Priority).ToList();

        // Compose emergency message
        var emergencyMessage = ComposeEmergencyMessage(incident, protocol);

        // Attempt to contact each emergency contact
        foreach (var contact in sortedContacts)
        {
            var attempt = new ContactAttempt
            {
                ContactName = contact.Name,
                ContactMethod = contact.ContactType,
                AttemptedAt = DateTime.UtcNow
            };

            try
            {
                bool success = false;

                switch (contact.ContactType.ToUpper())
                {
                    case "SMS":
                        success = await SendEmergencySMSAsync(contact.PhoneNumber, emergencyMessage, incident.Title);
                        break;
                    case "VOICE":
                        success = await PlaceEmergencyCallAsync(contact.PhoneNumber, emergencyMessage, incident.Title);
                        break;
                    case "EMAIL":
                        success = await SendEmergencyEmailAsync(contact.PhoneNumber, incident.Title, emergencyMessage);
                        break;
                    default:
                        _logger.LogWarning("Unknown contact type: {ContactType}", contact.ContactType);
                        break;
                }

                attempt.Success = success;

                if (success)
                {
                    _logger.LogInformation("Successfully contacted {ContactName} via {ContactMethod} for incident {IncidentId}",
                        contact.Name, contact.ContactType, incident.Id);
                    result.Success = true; // At least one contact succeeded
                }

                // Log attempt to database for audit trail
                await LogContactAttemptAsync(
                    incident.Id,
                    contact.ContactType,
                    success,
                    success ? $"Successfully contacted {contact.Name} at {contact.PhoneNumber}" : "Contact attempt failed");
            }
            catch (Exception ex)
            {
                attempt.Success = false;
                attempt.ErrorMessage = ex.Message;
                _logger.LogError(ex, "Failed to contact {ContactName} via {ContactMethod} for incident {IncidentId}",
                    contact.Name, contact.ContactType, incident.Id);

                // Log failed attempt to database
                await LogContactAttemptAsync(
                    incident.Id,
                    contact.ContactType,
                    false,
                    $"Error contacting {contact.Name}: {ex.Message}");
            }

            result.Attempts.Add(attempt);
        }

        if (!result.Success)
        {
            result.FailureReason = "All contact attempts failed";
        }

        return result;
    }

    public async Task<bool> SendEmergencySMSAsync(
        string phoneNumber,
        string message,
        string emergencyType)
    {
        try
        {
            var accountSid = _configuration["Twilio:AccountSid"];
            var authToken = _configuration["Twilio:AuthToken"];
            var fromNumber = _configuration["Twilio:FromWhatsApp"];

            if (string.IsNullOrEmpty(accountSid) || string.IsNullOrEmpty(authToken) || string.IsNullOrEmpty(fromNumber))
            {
                _logger.LogError("Twilio credentials not configured");
                return false;
            }

            // Remove "whatsapp:" prefix if present
            fromNumber = fromNumber.Replace("whatsapp:", "");

            // Ensure phone number has + prefix
            if (!phoneNumber.StartsWith("+"))
            {
                phoneNumber = "+" + phoneNumber;
            }

            var url = $"https://api.twilio.com/2010-04-01/Accounts/{accountSid}/Messages.json";

            _httpClient.DefaultRequestHeaders.Clear();

            // Twilio uses Basic Authentication
            var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{accountSid}:{authToken}"));
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Basic {credentials}");

            var formParams = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("From", fromNumber),
                new KeyValuePair<string, string>("To", phoneNumber),
                new KeyValuePair<string, string>("Body", $"🚨 EMERGENCY ALERT 🚨\n\n{message}")
            };

            var content = new FormUrlEncodedContent(formParams);
            var response = await _httpClient.PostAsync(url, content);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Emergency SMS sent to {PhoneNumber}", phoneNumber);
                return true;
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("Failed to send emergency SMS to {PhoneNumber}. Status: {Status}, Error: {Error}",
                phoneNumber, response.StatusCode, errorContent);

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send emergency SMS to {PhoneNumber}", phoneNumber);
            return false;
        }
    }

    public async Task<bool> PlaceEmergencyCallAsync(
        string phoneNumber,
        string message,
        string emergencyType)
    {
        try
        {
            // Note: Twilio Voice API requires TwiML configuration
            // This is a simplified implementation that would need TwiML endpoint setup

            _logger.LogWarning("Emergency voice call requested but not fully implemented. " +
                "Would call {PhoneNumber} about {EmergencyType}", phoneNumber, emergencyType);

            // For now, fall back to SMS
            return await SendEmergencySMSAsync(phoneNumber, message, emergencyType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to place emergency call to {PhoneNumber}", phoneNumber);
            return false;
        }
    }

    public async Task<EmergencyContactAttempt> LogContactAttemptAsync(
        int incidentId,
        string contactMethod,
        bool success,
        string details)
    {
        try
        {
            // Get the incident to retrieve TenantId
            var incident = await _context.EmergencyIncidents.FindAsync(incidentId);
            if (incident == null)
            {
                _logger.LogError("Cannot log contact attempt - incident {IncidentId} not found", incidentId);
                return new EmergencyContactAttempt();
            }

            var attempt = new Models.EmergencyContactAttempt
            {
                IncidentId = incidentId,
                TenantId = incident.TenantId,
                ContactMethod = contactMethod,
                Success = success,
                Details = details,
                CreatedAt = DateTime.UtcNow,
                AttemptedBy = "System"
            };

            _context.EmergencyContactAttempts.Add(attempt);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Emergency contact attempt logged: Incident={IncidentId}, Method={Method}, Success={Success}",
                incidentId, contactMethod, success);

            // Map to service model
            return new EmergencyContactAttempt
            {
                IncidentId = attempt.IncidentId,
                ContactMethod = attempt.ContactMethod,
                Success = attempt.Success,
                Details = attempt.Details,
                CreatedAt = attempt.CreatedAt
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist emergency contact attempt for incident {IncidentId}", incidentId);
            return new EmergencyContactAttempt();
        }
    }

    private async Task<bool> SendEmergencyEmailAsync(string emailAddress, string subject, string message)
    {
        try
        {
            return await _emailService.SendEmailAsync(
                emailAddress,
                $"🚨 EMERGENCY: {subject}",
                $"<html><body><h2 style='color: red;'>🚨 EMERGENCY ALERT 🚨</h2><p>{message}</p></body></html>",
                message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send emergency email to {EmailAddress}", emailAddress);
            return false;
        }
    }

    private string ComposeEmergencyMessage(EmergencyIncident incident, EmergencyProtocol protocol)
    {
        return $"""
            Type: {incident.Title}
            Severity: {incident.SeverityLevel}
            Location: {incident.Location ?? "Not specified"}

            Description:
            {incident.Description}

            Reporter: {incident.ReportedBy ?? "Not specified"}
            Time: {incident.ReportedAt:yyyy-MM-dd HH:mm:ss} UTC

            Protocol: {protocol.Title}

            Incident ID: {incident.Id}
            """;
    }
}
