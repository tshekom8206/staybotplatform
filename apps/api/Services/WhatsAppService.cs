using System.Text;
using System.Text.Json;
using System.Linq;
using Hostr.Contracts.DTOs.WhatsApp;
using Hostr.Api.Data;
using Hostr.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Hostr.Api.Services;

public interface IWhatsAppApiClient
{
    Task<bool> SendMessageAsync(string phoneNumberId, OutboundMessage message, string accessToken);
    Task<bool> VerifyWebhookAsync(string mode, string verifyToken, string challenge);
}

public interface IWhatsAppService
{
    Task<(bool Success, string? Response)> ProcessInboundMessageAsync(WebhookPayload payload);
    Task<bool> SendTextMessageAsync(int tenantId, string toPhone, string message);
    Task<bool> SendTemplateMessageAsync(int tenantId, string toPhone, string templateName, string language = "en");
    Task<bool> SendImageAsync(int tenantId, string toPhone, string imageUrl, string? caption = null);

    // Enhanced methods with detailed error reporting for fallback logic
    Task<(bool Success, string? ErrorMessage)> SendTextMessageWithDetailsAsync(int tenantId, string toPhone, string message);
    Task<(bool Success, string? ErrorMessage)> SendImageWithDetailsAsync(int tenantId, string toPhone, string imageUrl, string? caption = null);

    // Template message with parameters
    Task<(bool Success, string? ErrorMessage)> SendTemplateWithParametersAsync(
        int tenantId,
        string toPhone,
        string templateName,
        List<string> bodyParameters,
        string? buttonUrlParameter = null,
        string language = "en");
}

public class WhatsAppApiClient : IWhatsAppApiClient
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<WhatsAppApiClient> _logger;

    public WhatsAppApiClient(HttpClient httpClient, IConfiguration configuration, ILogger<WhatsAppApiClient> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<bool> SendMessageAsync(string phoneNumberId, OutboundMessage message, string accessToken)
    {
        try
        {
            var apiUrl = _configuration["WhatsApp:ApiUrl"];
            var url = $"{apiUrl}/{phoneNumberId}/messages";

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");

            // CRITICAL: WhatsApp has a 1600 character limit - split into multiple messages if needed
            var chunks = SplitMessageIntoChunks(message.Text?.Body ?? "", 1500);

            bool allSuccess = true;
            for (int i = 0; i < chunks.Count; i++)
            {
                var chunkMessage = new OutboundMessage
                {
                    To = message.To,
                    Text = new OutboundText { Body = chunks[i] }
                };

                _logger.LogInformation("Sending message part {Part}/{Total} to {Phone}, Body length={Length}",
                    i + 1, chunks.Count, message.To, chunks[i].Length);

                var json = JsonSerializer.Serialize(chunkMessage);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(url, content);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Message part {Part}/{Total} sent successfully to {Phone}",
                        i + 1, chunks.Count, message.To);

                    // Small delay between messages to avoid rate limiting
                    if (i < chunks.Count - 1)
                    {
                        await Task.Delay(200);
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to send message part {Part}/{Total} to {Phone}. Status: {Status}, Error: {Error}",
                        i + 1, chunks.Count, message.To, response.StatusCode, errorContent);
                    allSuccess = false;
                }
            }

            return allSuccess;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception sending message to {Phone}", message.To);
            return false;
        }
    }

    public Task<bool> VerifyWebhookAsync(string mode, string verifyToken, string challenge)
    {
        var configuredToken = _configuration["WhatsApp:VerifyToken"];

        _logger.LogInformation("Webhook verification: mode={Mode}, receivedToken={ReceivedToken}, configuredToken={ConfiguredToken}, tokensMatch={Match}",
            mode, verifyToken, configuredToken, verifyToken == configuredToken);

        var isValid = mode == "subscribe" && verifyToken == configuredToken;

        if (!isValid)
        {
            _logger.LogWarning("Verification failed - mode: {Mode}, tokensMatch: {Match}",
                mode, verifyToken == configuredToken);
        }

        return Task.FromResult(isValid);
    }

    private string NormalizePhoneNumber(string phoneNumber)
    {
        if (string.IsNullOrEmpty(phoneNumber))
        {
            return phoneNumber;
        }

        // Remove all spaces and special characters except + and numbers
        var cleaned = phoneNumber.Trim().Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "");

        // If it starts with +, keep it as is
        if (cleaned.StartsWith("+"))
        {
            return cleaned;
        }

        // If it starts with 27 (South Africa country code without +), add the +
        if (cleaned.StartsWith("27") && cleaned.Length >= 11)
        {
            return "+" + cleaned;
        }

        // If it starts with 0 (local SA number), replace with +27
        if (cleaned.StartsWith("0") && cleaned.Length >= 10)
        {
            return "+27" + cleaned.Substring(1);
        }

        // If it's just digits without country code, assume SA and add +27
        if (cleaned.All(char.IsDigit) && cleaned.Length >= 9 && cleaned.Length <= 10)
        {
            return "+27" + cleaned;
        }

        // Return as is if we can't determine the format
        _logger.LogWarning("Unable to normalize phone number format: {Phone}", phoneNumber);
        return phoneNumber;
    }

    private List<string> SplitMessageIntoChunks(string message, int maxChunkSize = 1500)
    {
        var chunks = new List<string>();

        // If message fits in one chunk, return as-is
        if (message.Length <= maxChunkSize)
        {
            chunks.Add(message);
            return chunks;
        }

        // Try to split by sentences first (. ! ? followed by space or newline)
        var sentences = new List<string>();
        var currentSentence = new StringBuilder();

        for (int i = 0; i < message.Length; i++)
        {
            currentSentence.Append(message[i]);

            // Check for sentence endings or newlines
            if ((message[i] == '.' || message[i] == '!' || message[i] == '?') &&
                (i == message.Length - 1 || message[i + 1] == ' ' || message[i + 1] == '\n'))
            {
                sentences.Add(currentSentence.ToString());
                currentSentence.Clear();
            }
            else if (message[i] == '\n')
            {
                sentences.Add(currentSentence.ToString());
                currentSentence.Clear();
            }
        }

        // Add any remaining text as a sentence
        if (currentSentence.Length > 0)
        {
            sentences.Add(currentSentence.ToString());
        }

        // Build chunks from sentences
        var currentChunk = new StringBuilder();

        foreach (var sentence in sentences)
        {
            // If a single sentence exceeds max size, hard split it
            if (sentence.Length > maxChunkSize)
            {
                // Save current chunk if not empty
                if (currentChunk.Length > 0)
                {
                    chunks.Add(currentChunk.ToString().Trim());
                    currentChunk.Clear();
                }

                // Hard split the long sentence
                for (int i = 0; i < sentence.Length; i += maxChunkSize)
                {
                    int chunkLength = Math.Min(maxChunkSize, sentence.Length - i);
                    chunks.Add(sentence.Substring(i, chunkLength).Trim());
                }
                continue;
            }

            // If adding this sentence would exceed max size, start new chunk
            if (currentChunk.Length + sentence.Length > maxChunkSize && currentChunk.Length > 0)
            {
                chunks.Add(currentChunk.ToString().Trim());
                currentChunk.Clear();
            }

            currentChunk.Append(sentence);
        }

        // Add the last chunk
        if (currentChunk.Length > 0)
        {
            chunks.Add(currentChunk.ToString().Trim());
        }

        // Add part numbers to chunks (except if only 1 chunk)
        if (chunks.Count > 1)
        {
            for (int i = 0; i < chunks.Count; i++)
            {
                chunks[i] = $"{chunks[i]} (Part {i + 1}/{chunks.Count})";
            }
        }

        _logger.LogInformation("Split message of {Length} chars into {Count} chunks", message.Length, chunks.Count);

        return chunks;
    }
}

public class WhatsAppService : IWhatsAppService
{
    private readonly HostrDbContext _context;
    private readonly IWhatsAppApiClient _whatsAppClient;
    private readonly IMessageRoutingService _messageRouter;
    private readonly ITenantService _tenantService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<WhatsAppService> _logger;
    private readonly INotificationService _notificationService;
    private readonly IActionProcessingService _actionProcessingService;

    public WhatsAppService(
        HostrDbContext context,
        IWhatsAppApiClient whatsAppClient,
        IMessageRoutingService messageRouter,
        ITenantService tenantService,
        IConfiguration configuration,
        ILogger<WhatsAppService> logger,
        INotificationService notificationService,
        IActionProcessingService actionProcessingService)
    {
        _context = context;
        _whatsAppClient = whatsAppClient;
        _messageRouter = messageRouter;
        _tenantService = tenantService;
        _configuration = configuration;
        _logger = logger;
        _notificationService = notificationService;
        _actionProcessingService = actionProcessingService;
    }

    public async Task<(bool Success, string? Response)> ProcessInboundMessageAsync(WebhookPayload payload)
    {
        try
        {
            string? lastResponse = null;

            foreach (var entry in payload.Entry)
            {
                _logger.LogInformation("Processing entry with {ChangeCount} changes", entry.Changes.Count);
                foreach (var change in entry.Changes)
                {
                    _logger.LogInformation("Processing change with field: '{Field}' (IsNullOrEmpty: {IsEmpty})", change.Field, string.IsNullOrEmpty(change.Field));
                    if (!string.IsNullOrEmpty(change.Field) && change.Field != "messages") continue;

                    var phoneNumberId = change.Value.Metadata.PhoneNumberId;
                    _logger.LogInformation("Processing webhook for phone number ID: {PhoneNumberId}", phoneNumberId);
                    
                    // Resolve tenant from phone number ID
                    var tenantContext = await _tenantService.GetTenantByPhoneNumberIdAsync(phoneNumberId);
                    if (tenantContext == null)
                    {
                        _logger.LogWarning("No tenant found for phone number ID: {PhoneNumberId}", phoneNumberId);
                        continue;
                    }

                    foreach (var message in change.Value.Messages)
                    {
                        var response = await ProcessSingleMessageAsync(tenantContext, message, change.Value.Contacts);
                        if (!string.IsNullOrEmpty(response))
                        {
                            lastResponse = response;
                        }
                    }
                }
            }

            return (true, lastResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing inbound WhatsApp message");
            return (false, null);
        }
    }

    private async Task<string?> ProcessSingleMessageAsync(TenantContext tenantContext, WebhookMessage message, List<WebhookContact> contacts)
    {
        try
        {
            // Normalize phone number to ensure it has + prefix
            var normalizedPhone = NormalizePhoneNumber(message.From);

            // Find or create conversation
            var conversation = await _context.Conversations
                .FirstOrDefaultAsync(c => c.WaUserPhone == normalizedPhone);

            if (conversation == null)
            {
                conversation = new Conversation
                {
                    TenantId = tenantContext.TenantId,
                    WaUserPhone = normalizedPhone,
                    Status = "Active"
                };

                _context.Conversations.Add(conversation);
                await _context.SaveChangesAsync();
            }

            // Save inbound message
            var inboundMessage = new Message
            {
                TenantId = tenantContext.TenantId,
                ConversationId = conversation.Id,
                Direction = "Inbound",
                MessageType = message.Type,
                Body = message.Text?.Body ?? "",
                CreatedAt = DateTime.UtcNow
            };

            try
            {
                _context.Messages.Add(inboundMessage);
                _logger.LogInformation("📥 Adding inbound message to context. ConversationId={ConversationId}, Body='{Body}'",
                    conversation.Id, inboundMessage.Body);

                await _context.SaveChangesAsync();

                _logger.LogInformation("✅ Saved inbound message {MessageId} to database", inboundMessage.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ FAILED to save inbound message for conversation {ConversationId}", conversation.Id);
                throw;
            }

            // Route message and get response
            var response = await _messageRouter.RouteMessageAsync(tenantContext, conversation, inboundMessage);

            // Update message with intent classification if available
            if (!string.IsNullOrEmpty(response.IntentClassification))
            {
                try
                {
                    inboundMessage.IntentClassification = response.IntentClassification;
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("✅ Saved intent classification '{Intent}' for message {MessageId}",
                        response.IntentClassification, inboundMessage.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ FAILED to save intent classification for message {MessageId}", inboundMessage.Id);
                }
            }

            if (!string.IsNullOrEmpty(response.Reply))
            {
                // Send reply and ensure it's saved to conversation history
                var sendSuccess = await SendTextMessageAsync(tenantContext.TenantId, normalizedPhone, response.Reply);

                if (sendSuccess)
                {
                    _logger.LogInformation("Bot response saved to conversation {ConversationId}: '{Reply}'",
                        conversation.Id, response.Reply);
                }

                // Execute any actions (like creating tasks)
                await _actionProcessingService.ProcessActionsAsync(tenantContext, conversation, response);

                return response.Reply;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing single message from {Phone}", message.From);
            return null;
        }
    }

    private string NormalizePhoneNumber(string phoneNumber)
    {
        if (string.IsNullOrEmpty(phoneNumber))
        {
            return phoneNumber;
        }

        // Remove all spaces and special characters except + and numbers
        var cleaned = phoneNumber.Trim().Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "");

        // If it starts with +, keep it as is
        if (cleaned.StartsWith("+"))
        {
            return cleaned;
        }

        // If it starts with 27 (South Africa country code without +), add the +
        if (cleaned.StartsWith("27") && cleaned.Length >= 11)
        {
            return "+" + cleaned;
        }

        // If it starts with 0 (local SA number), replace with +27
        if (cleaned.StartsWith("0") && cleaned.Length >= 10)
        {
            return "+27" + cleaned.Substring(1);
        }

        // If it's just digits without country code, assume SA and add +27
        if (cleaned.All(char.IsDigit) && cleaned.Length >= 9 && cleaned.Length <= 10)
        {
            return "+27" + cleaned;
        }

        // Return as is if we can't determine the format
        _logger.LogWarning("Unable to normalize phone number format: {Phone}", phoneNumber);
        return phoneNumber;
    }

    public async Task<bool> SendTextMessageAsync(int tenantId, string toPhone, string messageText)
    {
        try
        {
            // Get WhatsApp number for tenant (WhatsApp Cloud API)
            // Get shared WhatsApp number (TenantId is null for shared number across all tenants)
            var waNumber = await _context.WhatsAppNumbers
                .FirstOrDefaultAsync(w => w.TenantId == null && w.Status == "Active");

            if (waNumber == null)
            {
                _logger.LogError("No active WhatsApp number found for tenant {TenantId}", tenantId);
                return false;
            }

            var message = new OutboundMessage
            {
                To = toPhone,
                Text = new OutboundText { Body = messageText }
            };

            var success = await _whatsAppClient.SendMessageAsync(waNumber.PhoneNumberId, message, waNumber.PageAccessToken);

            // Save FULL outbound message to database (even if split into multiple WhatsApp messages)
            // This maintains conversation history integrity
            using var scope = new TenantScope(_context, tenantId);

            var conversation = await _context.Conversations
                .FirstOrDefaultAsync(c => c.WaUserPhone == toPhone);

            if (conversation != null)
            {
                try
                {
                    var outboundMessage = new Message
                    {
                        TenantId = tenantId,
                        ConversationId = conversation.Id,
                        Direction = "Outbound",
                        MessageType = "text",
                        Body = messageText, // Save full original message
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.Messages.Add(outboundMessage);
                    _logger.LogInformation("📤 Adding outbound message to context. ConversationId={ConversationId}, Length={Length}",
                        conversation.Id, messageText.Length);

                    // Update conversation in the same transaction
                    conversation.LastBotReplyAt = DateTime.UtcNow;

                    await _context.SaveChangesAsync();

                    _logger.LogInformation("✅ Saved outbound message {MessageId} ({Length} chars) to conversation {ConversationId}",
                        outboundMessage.Id, messageText.Length, conversation.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ FAILED to save outbound message for conversation {ConversationId}", conversation.Id);
                }
            }
            else
            {
                _logger.LogWarning("⚠️ No conversation found for phone {Phone} when saving outbound message", toPhone);
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending text message to {Phone}", toPhone);
            return false;
        }
    }

    public async Task<bool> SendTemplateMessageAsync(int tenantId, string toPhone, string templateName, string language = "en")
    {
        try
        {
            // For now, just send template as text message using the same provider logic
            return await SendTextMessageAsync(tenantId, toPhone, $"Template: {templateName}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending template message to {Phone}", toPhone);
            return false;
        }
    }

    public async Task<(bool Success, string? ErrorMessage)> SendTemplateWithParametersAsync(
        int tenantId,
        string toPhone,
        string templateName,
        List<string> bodyParameters,
        string? buttonUrlParameter = null,
        string language = "en")
    {
        try
        {
            // Get WhatsApp number for tenant
            // Get shared WhatsApp number (TenantId is null for shared number across all tenants)
            var waNumber = await _context.WhatsAppNumbers
                .FirstOrDefaultAsync(w => w.TenantId == null && w.Status == "Active");

            if (waNumber == null)
            {
                var errorMsg = $"No active WhatsApp number found for tenant {tenantId}";
                _logger.LogError(errorMsg);
                return (false, errorMsg);
            }

            _logger.LogInformation("Sending template {TemplateName} to {Phone} for tenant {TenantId}",
                templateName, toPhone, tenantId);

            // Build template message payload
            var components = new List<object>();

            // Add body component with parameters
            if (bodyParameters != null && bodyParameters.Count > 0)
            {
                var bodyParams = bodyParameters.Select(p => new { type = "text", text = p }).ToList();
                components.Add(new
                {
                    type = "body",
                    parameters = bodyParams
                });
            }

            // Add button component with URL parameter if provided
            if (!string.IsNullOrEmpty(buttonUrlParameter))
            {
                components.Add(new
                {
                    type = "button",
                    sub_type = "url",
                    index = "0",
                    parameters = new[]
                    {
                        new { type = "text", text = buttonUrlParameter }
                    }
                });
            }

            var templatePayload = new
            {
                messaging_product = "whatsapp",
                to = toPhone,
                type = "template",
                template = new
                {
                    name = templateName,
                    language = new { code = language },
                    components = components
                }
            };

            // Send via WhatsApp Cloud API
            var apiUrl = $"https://graph.facebook.com/v22.0/{waNumber.PhoneNumberId}/messages";

            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Clear();
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {waNumber.PageAccessToken}");

            var json = JsonSerializer.Serialize(templatePayload);
            _logger.LogInformation("Template payload: {Payload}", json);

            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await httpClient.PostAsync(apiUrl, content);
            var responseBody = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("WhatsApp template API response - Status: {StatusCode}, Body: {ResponseBody}",
                response.StatusCode, responseBody);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Template {TemplateName} sent successfully to {Phone}",
                    templateName, toPhone);

                // Save to conversation history
                var conversation = await _context.Conversations
                    .FirstOrDefaultAsync(c => c.WaUserPhone == toPhone);

                if (conversation != null)
                {
                    var messageRecord = new Message
                    {
                        TenantId = tenantId,
                        ConversationId = conversation.Id,
                        Direction = "Outbound",
                        Body = $"[Template: {templateName}]",
                        MessageType = "template",
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.Messages.Add(messageRecord);
                    conversation.LastBotReplyAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("✅ Saved template message to conversation {ConversationId}", conversation.Id);
                }

                return (true, null);
            }
            else
            {
                var errorMsg = $"WhatsApp template API error: {response.StatusCode} - {responseBody}";
                _logger.LogError("Template {TemplateName} failed to {Phone}. Status: {Status}, Response: {Response}",
                    templateName, toPhone, response.StatusCode, responseBody);
                return (false, errorMsg);
            }
        }
        catch (HttpRequestException ex)
        {
            var errorMsg = $"WhatsApp template API network error: {ex.Message}";
            _logger.LogError(ex, "Network error sending template to {Phone}", toPhone);
            return (false, errorMsg);
        }
        catch (Exception ex)
        {
            var errorMsg = $"Template send error: {ex.Message}";
            _logger.LogError(ex, "Error sending template message to {Phone}", toPhone);
            return (false, errorMsg);
        }
    }

    public async Task<bool> SendImageAsync(int tenantId, string toPhone, string imageUrl, string? caption = null)
    {
        try
        {
            // Get WhatsApp number for tenant
            // Get shared WhatsApp number (TenantId is null for shared number across all tenants)
            var waNumber = await _context.WhatsAppNumbers
                .FirstOrDefaultAsync(w => w.TenantId == null && w.Status == "Active");

            if (waNumber == null)
            {
                _logger.LogError("No active WhatsApp number found for tenant {TenantId}", tenantId);
                return false;
            }

            var message = new OutboundMessage
            {
                To = toPhone,
                Type = "image",
                Image = new OutboundImage
                {
                    Link = imageUrl,
                    Caption = caption
                }
            };

            // Send via WhatsApp API
            var apiUrl = $"https://graph.facebook.com/v18.0/{waNumber.PhoneNumberId}/messages";

            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {waNumber.PageAccessToken}");

            var json = System.Text.Json.JsonSerializer.Serialize(message);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync(apiUrl, content);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Image sent successfully to {Phone} with caption: {Caption}",
                    toPhone, caption?.Substring(0, Math.Min(50, caption?.Length ?? 0)));

                // Save outbound message to database
                using var scope = new TenantScope(_context, tenantId);

                var conversation = await _context.Conversations
                    .FirstOrDefaultAsync(c => c.WaUserPhone == toPhone);

                if (conversation != null)
                {
                    var outboundMessage = new Message
                    {
                        TenantId = tenantId,
                        ConversationId = conversation.Id,
                        Direction = "Outbound",
                        MessageType = "image",
                        Body = caption ?? "[Image]",
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.Messages.Add(outboundMessage);
                    conversation.LastBotReplyAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Saved image message {MessageId} to conversation {ConversationId}",
                        outboundMessage.Id, conversation.Id);
                }

                return true;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to send image to {Phone}. Status: {Status}, Error: {Error}",
                    toPhone, response.StatusCode, errorContent);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending image to {Phone}", toPhone);
            return false;
        }
    }

    public async Task<(bool Success, string? ErrorMessage)> SendTextMessageWithDetailsAsync(int tenantId, string toPhone, string messageText)
    {
        try
        {
            // Get WhatsApp number for tenant
            // Get shared WhatsApp number (TenantId is null for shared number across all tenants)
            var waNumber = await _context.WhatsAppNumbers
                .FirstOrDefaultAsync(w => w.TenantId == null && w.Status == "Active");

            if (waNumber == null)
            {
                var errorMsg = $"No active WhatsApp number found for tenant {tenantId}";
                _logger.LogError(errorMsg);
                return (false, errorMsg);
            }

            var message = new OutboundMessage
            {
                To = toPhone,
                Text = new OutboundText { Body = messageText }
            };

            var success = await _whatsAppClient.SendMessageAsync(waNumber.PhoneNumberId, message, waNumber.PageAccessToken);

            if (!success)
            {
                return (false, "WhatsApp API returned failure");
            }

            // Save outbound message to database
            using var scope = new TenantScope(_context, tenantId);

            var conversation = await _context.Conversations
                .FirstOrDefaultAsync(c => c.WaUserPhone == toPhone);

            if (conversation != null)
            {
                try
                {
                    var outboundMessage = new Message
                    {
                        TenantId = tenantId,
                        ConversationId = conversation.Id,
                        Direction = "Outbound",
                        MessageType = "text",
                        Body = messageText,
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.Messages.Add(outboundMessage);
                    conversation.LastBotReplyAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("✅ Saved outbound message {MessageId} to conversation {ConversationId}",
                        outboundMessage.Id, conversation.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ FAILED to save outbound message for conversation {ConversationId}", conversation.Id);
                }
            }

            return (true, null);
        }
        catch (HttpRequestException ex)
        {
            var errorMsg = $"WhatsApp API network error: {ex.Message}";
            _logger.LogError(ex, "Error sending text message to {Phone}", toPhone);
            return (false, errorMsg);
        }
        catch (Exception ex)
        {
            var errorMsg = $"WhatsApp send error: {ex.Message}";
            _logger.LogError(ex, "Error sending text message to {Phone}", toPhone);
            return (false, errorMsg);
        }
    }

    public async Task<(bool Success, string? ErrorMessage)> SendImageWithDetailsAsync(int tenantId, string toPhone, string imageUrl, string? caption = null)
    {
        try
        {
            // Get WhatsApp number for tenant
            // Get shared WhatsApp number (TenantId is null for shared number across all tenants)
            var waNumber = await _context.WhatsAppNumbers
                .FirstOrDefaultAsync(w => w.TenantId == null && w.Status == "Active");

            if (waNumber == null)
            {
                var errorMsg = $"No active WhatsApp number found for tenant {tenantId}";
                _logger.LogError(errorMsg);
                return (false, errorMsg);
            }

            var message = new OutboundMessage
            {
                To = toPhone,
                Type = "image",
                Image = new OutboundImage
                {
                    Link = imageUrl,
                    Caption = caption
                }
            };

            // Send via WhatsApp API
            var apiUrl = $"https://graph.facebook.com/v18.0/{waNumber.PhoneNumberId}/messages";

            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {waNumber.PageAccessToken}");

            var json = System.Text.Json.JsonSerializer.Serialize(message);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync(apiUrl, content);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Image sent successfully to {Phone} with caption: {Caption}",
                    toPhone, caption?.Substring(0, Math.Min(50, caption?.Length ?? 0)));

                // Save outbound message to database
                using var scope = new TenantScope(_context, tenantId);

                var conversation = await _context.Conversations
                    .FirstOrDefaultAsync(c => c.WaUserPhone == toPhone);

                if (conversation != null)
                {
                    var outboundMessage = new Message
                    {
                        TenantId = tenantId,
                        ConversationId = conversation.Id,
                        Direction = "Outbound",
                        MessageType = "image",
                        Body = caption ?? "[Image]",
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.Messages.Add(outboundMessage);
                    conversation.LastBotReplyAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Saved image message {MessageId} to conversation {ConversationId}",
                        outboundMessage.Id, conversation.Id);
                }

                return (true, null);
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                var errorMsg = $"WhatsApp API error: {response.StatusCode} - {errorContent}";
                _logger.LogError("Failed to send image to {Phone}. Status: {Status}, Error: {Error}",
                    toPhone, response.StatusCode, errorContent);
                return (false, errorMsg);
            }
        }
        catch (HttpRequestException ex)
        {
            var errorMsg = $"WhatsApp API network error: {ex.Message}";
            _logger.LogError(ex, "Error sending image to {Phone}", toPhone);
            return (false, errorMsg);
        }
        catch (Exception ex)
        {
            var errorMsg = $"WhatsApp send error: {ex.Message}";
            _logger.LogError(ex, "Error sending image to {Phone}", toPhone);
            return (false, errorMsg);
        }
    }

}

public class TenantScope : IDisposable
{
    private readonly HostrDbContext _context;

    public TenantScope(HostrDbContext context, int tenantId)
    {
        _context = context;
        // Set tenant context in HttpContext items
        // This is a simplified implementation - in production, use proper scoping
    }

    public void Dispose()
    {
        // Cleanup if needed
    }
}