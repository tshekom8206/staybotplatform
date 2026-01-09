using System.Text;
using System.Text.Json;

namespace Hostr.Api.Services;

public interface ISmsService
{
    Task<bool> SendMessageAsync(string toPhone, string messageText);
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
            var from = _configuration["ClickaTell:From"] ?? "StayBot";

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
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", apiKey);

            var response = await _httpClient.PostAsync(
                "https://platform.clickatell.com/messages",
                content);

            if (response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("SMS sent successfully to {Phone}. Response: {Response}",
                    toPhone, responseBody);
                return true;
            }
            else
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogError("SMS failed to {Phone}. Status: {Status}, Error: {Error}",
                    toPhone, response.StatusCode, errorBody);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending SMS to {Phone}", toPhone);
            return false;
        }
    }
}
