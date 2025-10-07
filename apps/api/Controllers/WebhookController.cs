using Microsoft.AspNetCore.Mvc;
using Hostr.Contracts.DTOs.WhatsApp;
using Hostr.Api.Services;
using Hostr.Api.Data;
using Hostr.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Hostr.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class WebhookController : ControllerBase
{
    private readonly IWhatsAppService _whatsAppService;
    private readonly IWhatsAppApiClient _whatsAppClient;
    private readonly IRatingService _ratingService;
    private readonly HostrDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<WebhookController> _logger;

    public WebhookController(
        IWhatsAppService whatsAppService,
        IWhatsAppApiClient whatsAppClient,
        IRatingService ratingService,
        HostrDbContext context,
        IConfiguration configuration,
        ILogger<WebhookController> logger)
    {
        _whatsAppService = whatsAppService;
        _whatsAppClient = whatsAppClient;
        _ratingService = ratingService;
        _context = context;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> VerifyWebhook([FromQuery(Name = "hub.mode")] string mode,
                                                   [FromQuery(Name = "hub.challenge")] string challenge,
                                                   [FromQuery(Name = "hub.verify_token")] string verifyToken)
    {
        _logger.LogInformation("Webhook verification request: mode={Mode}, verifyToken={VerifyToken}", mode, verifyToken);
        
        var isValid = await _whatsAppClient.VerifyWebhookAsync(mode, verifyToken, challenge);
        
        if (isValid)
        {
            _logger.LogInformation("Webhook verification successful");
            return Ok(challenge);
        }
        
        _logger.LogWarning("Webhook verification failed");
        return BadRequest("Verification failed");
    }

    [HttpPost]
    public async Task<IActionResult> ReceiveMessage([FromBody] WebhookPayload payload)
    {
        try
        {
            _logger.LogInformation("Received webhook payload with {EntryCount} entries", payload.Entry.Count);
            
            var (success, response) = await _whatsAppService.ProcessInboundMessageAsync(payload);
            
            if (success)
            {
                return Ok(new { success = true, response = response ?? "Message processed" });
            }
            
            return StatusCode(500, "Failed to process message");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing webhook message");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("twilio")]
    public async Task<IActionResult> ReceiveTwilioMessage()
    {
        try
        {
            var form = await Request.ReadFormAsync();
            
            var messageBody = form["Body"].FirstOrDefault();
            var fromNumber = form["From"].FirstOrDefault()?.Replace("whatsapp:", "");
            var toNumber = form["To"].FirstOrDefault()?.Replace("whatsapp:", "");

            // Ensure phone numbers have proper format for database lookup
            if (!string.IsNullOrEmpty(toNumber) && !toNumber.StartsWith("+"))
            {
                toNumber = "+" + toNumber.TrimStart();
            }
            if (!string.IsNullOrEmpty(fromNumber) && !fromNumber.StartsWith("+"))
            {
                fromNumber = "+" + fromNumber.TrimStart();
            }

            _logger.LogInformation("Received Twilio webhook: From={From}, To={To}, Body={Body}", fromNumber, toNumber, messageBody);
            
            if (string.IsNullOrEmpty(messageBody) || string.IsNullOrEmpty(fromNumber))
            {
                return BadRequest("Missing required fields");
            }

            // Convert Twilio webhook to our internal format
            var payload = new WebhookPayload
            {
                Entry = new List<WebhookEntry>
                {
                    new WebhookEntry
                    {
                        Changes = new List<WebhookChange>
                        {
                            new WebhookChange
                            {
                                Field = "messages",
                                Value = new WebhookValue
                                {
                                    Metadata = new WebhookMetadata
                                    {
                                        PhoneNumberId = toNumber ?? "unknown"
                                    },
                                    Messages = new List<WebhookMessage>
                                    {
                                        new WebhookMessage
                                        {
                                            From = fromNumber,
                                            Type = "text",
                                            Text = new WebhookText { Body = messageBody }
                                        }
                                    },
                                    Contacts = new List<WebhookContact>()
                                }
                            }
                        }
                    }
                }
            };
            
            var (success, response) = await _whatsAppService.ProcessInboundMessageAsync(payload);

            // Try to extract rating from the message after processing
            if (success && !string.IsNullOrEmpty(messageBody))
            {
                try
                {
                    // Get the conversation for rating extraction
                    var conversation = await GetConversationByPhoneAsync(fromNumber);
                    if (conversation != null)
                    {
                        // Use RatingService's comprehensive rating extraction (handles both numbers and words)
                        var rating = await _ratingService.CollectRatingFromChatAsync(
                            conversation.TenantId,
                            conversation.Id,
                            messageBody);

                        if (rating != null)
                        {
                            _logger.LogInformation("GuestRating collected via webhook: {Rating}/5 for conversation {ConversationId}, task {TaskId}, guest {GuestName} room {RoomNumber}",
                                rating.Rating, rating.ConversationId, rating.TaskId, rating.GuestName, rating.RoomNumber);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error collecting rating from message: {Message}", ex.Message);
                    // Continue - don't fail the webhook processing
                }
            }

            if (success)
            {
                // Twilio expects TwiML response - empty response to acknowledge receipt
                // Actual reply will be sent via Twilio API in SendTextMessageAsync
                var twiml = "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Response></Response>";
                return Content(twiml, "application/xml");
            }

            return StatusCode(500, "Failed to process message");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Twilio webhook message");
            return StatusCode(500, "Internal server error");
        }
    }

    private async Task<Conversation?> GetConversationByPhoneAsync(string phoneNumber)
    {
        return await _context.Conversations
            .Where(c => c.WaUserPhone == phoneNumber)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync();
    }

}