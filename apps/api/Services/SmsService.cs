using System.Text;
using System.Text.Json;

namespace Hostr.Api.Services;

public interface ISmsService
{
    Task<bool> SendMessageAsync(string toPhone, string messageText);

    // Enhanced method with detailed error reporting for fallback logic
    Task<(bool Success, string? ErrorMessage)> SendMessageWithDetailsAsync(string toPhone, string messageText);
}

public class SmsService : ISmsService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SmsService> _logger;

    public SmsService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<SmsService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<bool> SendMessageAsync(string toPhone, string messageText)
    {
        try
        {
            var apiKey = _configuration["ClickaTell:ApiKey"];
            var from = _configuration["ClickaTell:From"]; // Don't set default - omit if not configured

            if (string.IsNullOrEmpty(apiKey))
            {
                _logger.LogError("ClickaTell API key not configured");
                return false;
            }

            _logger.LogInformation("Sending SMS to {Phone}, length={Length}", toPhone, messageText.Length);

            // Build request body - only include "from" if it's configured
            var requestBody = string.IsNullOrEmpty(from)
                ? new { content = messageText, to = new[] { toPhone } }
                : (object)new { content = messageText, to = new[] { toPhone }, from = from };

            var json = JsonSerializer.Serialize(requestBody);
            _logger.LogInformation("ClickaTell request payload: {Payload}", json);

            // Create HTTP request message to properly set Authorization header
            using var httpClient = new HttpClient();
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://platform.clickatell.com/messages");

            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            request.Headers.TryAddWithoutValidation("Authorization", apiKey);
            request.Headers.TryAddWithoutValidation("User-Agent", "StayBot/1.0");
            request.Headers.TryAddWithoutValidation("Accept", "application/json");

            _logger.LogInformation("Sending HTTP POST to ClickaTell API for {Phone}", toPhone);

            var response = await httpClient.SendAsync(request);

            var responseBody = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("ClickaTell API response - Status: {StatusCode} ({StatusCodeNum}), Body: {ResponseBody}",
                response.StatusCode, (int)response.StatusCode, responseBody);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("SMS sent successfully to {Phone}. Status: {Status}, Response: {Response}",
                    toPhone, response.StatusCode, responseBody);
                return true;
            }
            else
            {
                _logger.LogError("SMS failed to {Phone}. Status: {Status}, Response: {Response}",
                    toPhone, response.StatusCode, responseBody);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending SMS to {Phone}", toPhone);
            return false;
        }
    }

    public async Task<(bool Success, string? ErrorMessage)> SendMessageWithDetailsAsync(string toPhone, string messageText)
    {
        try
        {
            var apiKey = _configuration["ClickaTell:ApiKey"];
            var from = _configuration["ClickaTell:From"];

            if (string.IsNullOrEmpty(apiKey))
            {
                var errorMsg = "ClickaTell API key not configured";
                _logger.LogError(errorMsg);
                return (false, errorMsg);
            }

            _logger.LogInformation("Sending SMS to {Phone}, length={Length}", toPhone, messageText.Length);

            // Build request body - only include "from" if it's configured
            var requestBody = string.IsNullOrEmpty(from)
                ? new { content = messageText, to = new[] { toPhone } }
                : (object)new { content = messageText, to = new[] { toPhone }, from = from };

            var json = JsonSerializer.Serialize(requestBody);
            _logger.LogInformation("ClickaTell request payload: {Payload}", json);

            // Create HTTP request message to properly set Authorization header
            using var httpClient = new HttpClient();
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://platform.clickatell.com/messages");

            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            request.Headers.TryAddWithoutValidation("Authorization", apiKey);
            request.Headers.TryAddWithoutValidation("User-Agent", "StayBot/1.0");
            request.Headers.TryAddWithoutValidation("Accept", "application/json");

            _logger.LogInformation("Sending HTTP POST to ClickaTell API for {Phone}", toPhone);

            var response = await httpClient.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("ClickaTell API response - Status: {StatusCode} ({StatusCodeNum}), Body: {ResponseBody}",
                response.StatusCode, (int)response.StatusCode, responseBody);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("SMS sent successfully to {Phone}. Status: {Status}, Response: {Response}",
                    toPhone, response.StatusCode, responseBody);
                return (true, null);
            }
            else
            {
                var errorMsg = $"ClickaTell API error: {response.StatusCode} - {responseBody}";
                _logger.LogError("SMS failed to {Phone}. Status: {Status}, Response: {Response}",
                    toPhone, response.StatusCode, responseBody);
                return (false, errorMsg);
            }
        }
        catch (HttpRequestException ex)
        {
            var errorMsg = $"SMS API network error: {ex.Message}";
            _logger.LogError(ex, "Error sending SMS to {Phone}", toPhone);
            return (false, errorMsg);
        }
        catch (Exception ex)
        {
            var errorMsg = $"SMS send error: {ex.Message}";
            _logger.LogError(ex, "Error sending SMS to {Phone}", toPhone);
            return (false, errorMsg);
        }
    }
}
