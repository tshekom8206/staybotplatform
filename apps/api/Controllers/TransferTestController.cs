using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Hostr.Api.Services;
using Hostr.Api.Models;
using Hostr.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Hostr.Api.Controllers;

[ApiController]
[Route("api/test/transfer")]
[AllowAnonymous] // Bypass authentication and tenant middleware
public class TransferTestController : ControllerBase
{
    private readonly IHumanTransferService _humanTransferService;
    private readonly IConversationHandoffService _conversationHandoffService;
    private readonly HostrDbContext _context;
    private readonly ILogger<TransferTestController> _logger;

    public TransferTestController(
        IHumanTransferService humanTransferService,
        IConversationHandoffService conversationHandoffService,
        HostrDbContext context,
        ILogger<TransferTestController> logger)
    {
        _humanTransferService = humanTransferService;
        _conversationHandoffService = conversationHandoffService;
        _context = context;
        _logger = logger;
    }

    [HttpPost("test-detection")]
    public async Task<IActionResult> TestTransferDetection([FromBody] TransferTestRequest request)
    {
        try
        {
            // Manually set tenant context to bypass middleware
            HttpContext.Items["TenantId"] = request.TenantId;
            // Create or get a test conversation
            var conversation = await _context.Conversations
                .FirstOrDefaultAsync(c => c.WaUserPhone == request.PhoneNumber);

            if (conversation == null)
            {
                conversation = new Conversation
                {
                    TenantId = request.TenantId,
                    WaUserPhone = request.PhoneNumber,
                    Status = "Active",
                    CreatedAt = DateTime.UtcNow
                };
                _context.Conversations.Add(conversation);
                await _context.SaveChangesAsync();
            }

            // Add the test message to the conversation
            var message = new Message
            {
                TenantId = request.TenantId,
                ConversationId = conversation.Id,
                Direction = "Inbound",
                Body = request.MessageText,
                CreatedAt = DateTime.UtcNow
            };
            _context.Messages.Add(message);
            await _context.SaveChangesAsync();

            // Test transfer detection
            var detectionResult = await _humanTransferService.DetectTransferRequestAsync(
                request.MessageText,
                conversation);

            // If transfer detected, prepare handoff context
            object? handoffContext = null;
            if (detectionResult.ShouldTransfer)
            {
                handoffContext = await _conversationHandoffService.PrepareHandoffContextAsync(
                    conversation.Id,
                    detectionResult.Reason);
            }

            return Ok(new
            {
                TestMessage = request.MessageText,
                TransferDetected = detectionResult.ShouldTransfer,
                DetectionDetails = new
                {
                    Confidence = detectionResult.Confidence,
                    Reason = detectionResult.Reason.ToString(),
                    Priority = detectionResult.Priority.ToString(),
                    Department = detectionResult.Department,
                    DetectionMethod = detectionResult.DetectionMethod,
                    TriggerPhrase = detectionResult.TriggerPhrase
                },
                HandoffContext = handoffContext,
                ConversationId = conversation.Id
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing transfer detection");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("test-scenarios")]
    public IActionResult GetTestScenarios()
    {
        var scenarios = new[]
        {
            new { Message = "I need to speak to a human agent please", ExpectedResult = "Should detect with high confidence" },
            new { Message = "Can I talk to a real person?", ExpectedResult = "Should detect with high confidence" },
            new { Message = "Transfer me to customer service", ExpectedResult = "Should detect with high confidence" },
            new { Message = "This is an emergency, I need help!", ExpectedResult = "Should detect as emergency transfer" },
            new { Message = "I want to order room service", ExpectedResult = "Should NOT detect transfer" },
            new { Message = "What time is breakfast?", ExpectedResult = "Should NOT detect transfer" }
        };

        return Ok(scenarios);
    }
}

public class TransferTestRequest
{
    public int TenantId { get; set; } = 1;
    public string PhoneNumber { get; set; } = "+27123456789";
    public string MessageText { get; set; } = "";
}