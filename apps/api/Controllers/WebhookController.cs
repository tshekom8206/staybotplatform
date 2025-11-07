using Microsoft.AspNetCore.Mvc;
using Hostr.Contracts.DTOs.WhatsApp;
using Hostr.Api.Services;
using Hostr.Api.Data;
using Hostr.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Hostr.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class WebhookController : ControllerBase
{
    private readonly IWhatsAppApiClient _whatsAppClient;
    private readonly IRatingService _ratingService;
    private readonly HostrDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<WebhookController> _logger;
    private readonly IMemoryCache _cache;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public WebhookController(
        IWhatsAppApiClient whatsAppClient,
        IRatingService ratingService,
        HostrDbContext context,
        IConfiguration configuration,
        ILogger<WebhookController> logger,
        IMemoryCache cache,
        IServiceScopeFactory serviceScopeFactory)
    {
        _whatsAppClient = whatsAppClient;
        _ratingService = ratingService;
        _context = context;
        _configuration = configuration;
        _logger = logger;
        _cache = cache;
        _serviceScopeFactory = serviceScopeFactory;
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
    public IActionResult ReceiveMessage([FromBody] WebhookPayload payload)
    {
        try
        {
            _logger.LogInformation("Received webhook payload with {EntryCount} entries", payload.Entry.Count);

            // Extract all message IDs for deduplication
            var messageIds = payload.Entry
                .SelectMany(e => e.Changes)
                .SelectMany(c => c.Value.Messages)
                .Select(m => m.Id)
                .Where(id => !string.IsNullOrEmpty(id))
                .ToList();

            // Check if any of these messages have already been processed
            var alreadyProcessed = messageIds.Any(msgId => _cache.TryGetValue($"msg_{msgId}", out _));

            if (alreadyProcessed)
            {
                _logger.LogInformation("Duplicate webhook detected, message IDs: {MessageIds}", string.Join(", ", messageIds));
                return Ok(new { success = true, message = "Already processed" });
            }

            // Mark messages as being processed (cache for 5 minutes)
            foreach (var msgId in messageIds)
            {
                _cache.Set($"msg_{msgId}", true, TimeSpan.FromMinutes(5));
            }

            // Return 200 OK immediately to prevent Facebook retries
            var response = Ok(new { success = true, message = "Processing" });

            // Process the message in the background with a new scope
            _ = Task.Run(async () =>
            {
                try
                {
                    _logger.LogInformation("🚀 Starting background processing for {MessageCount} messages", messageIds.Count);

                    using var scope = _serviceScopeFactory.CreateScope();
                    var whatsAppService = scope.ServiceProvider.GetRequiredService<IWhatsAppService>();
                    var (success, response) = await whatsAppService.ProcessInboundMessageAsync(payload);

                    if (success)
                    {
                        _logger.LogInformation("✅ Background processing completed successfully. Response: {Response}",
                            response ?? "(no response)");
                    }
                    else
                    {
                        _logger.LogWarning("⚠️ Background processing completed with success=false");
                    }
                }
                catch (DbUpdateException dbEx)
                {
                    _logger.LogError(dbEx, "❌ DATABASE ERROR in background webhook processing. Inner: {Inner}",
                        dbEx.InnerException?.Message ?? "No inner exception");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ GENERAL ERROR in background webhook processing. Type: {Type}, Message: {Message}",
                        ex.GetType().Name, ex.Message);
                }
            });

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling webhook");
            return Ok(new { success = true, message = "Error logged" }); // Still return 200 to prevent retries
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