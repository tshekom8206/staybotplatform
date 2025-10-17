using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Hostr.Api.Data;
using Hostr.Api.Models;
using Hostr.Api.Services;
using Microsoft.AspNetCore.Identity;

namespace Hostr.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TestController : ControllerBase
{
    private readonly HostrDbContext _context;
    private readonly IMessageRoutingService _messageRoutingService;
    private readonly IActionProcessingService _actionProcessingService;
    private readonly ILogger<TestController> _logger;
    private readonly UserManager<User> _userManager;
    private readonly INotificationService _notificationService;
    private readonly IEmailService _emailService;

    public TestController(
        HostrDbContext context,
        IMessageRoutingService messageRoutingService,
        IActionProcessingService actionProcessingService,
        ILogger<TestController> logger,
        UserManager<User> userManager,
        INotificationService notificationService,
        IEmailService emailService)
    {
        _context = context;
        _messageRoutingService = messageRoutingService;
        _actionProcessingService = actionProcessingService;
        _logger = logger;
        _userManager = userManager;
        _notificationService = notificationService;
        _emailService = emailService;
    }

    [HttpPost("simulate-message")]
    public async Task<IActionResult> SimulateMessage(
        [FromBody] SimulateMessageRequest request)
    {
        try
        {
            // Find or create conversation
            var conversation = await _context.Conversations
                .FirstOrDefaultAsync(c => c.TenantId == request.TenantId && 
                                         c.WaUserPhone == request.PhoneNumber);

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

                _logger.LogInformation("Created new conversation for phone {Phone} and tenant {TenantId}", 
                    request.PhoneNumber, request.TenantId);
            }

            // Create message
            var message = new Message
            {
                ConversationId = conversation.Id,
                TenantId = request.TenantId,
                MessageType = "text",
                Body = request.MessageText,
                Direction = "Inbound",
                CreatedAt = DateTime.UtcNow
            };

            _context.Messages.Add(message);
            await _context.SaveChangesAsync();

            // Process through message routing
            var tenant = await _context.Tenants.FindAsync(request.TenantId);
            if (tenant == null)
            {
                return BadRequest("Tenant not found");
            }

            var tenantContext = new TenantContext
            {
                TenantId = request.TenantId,
                TenantSlug = tenant.Slug
            };

            var routingResult = await _messageRoutingService.RouteMessageAsync(
                tenantContext, conversation, message);

            // Process any actions (create tasks, etc.)
            await _actionProcessingService.ProcessActionsAsync(
                tenantContext, conversation, routingResult);

            return Ok(new
            {
                ConversationId = conversation.Id,
                MessageId = message.Id,
                Response = routingResult.Reply,
                PhoneNumber = request.PhoneNumber,
                TenantId = request.TenantId,
                HasActions = routingResult.Action.HasValue || (routingResult.Actions?.Any() == true)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error simulating message");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("conversations/{tenantId}")]
    public async Task<IActionResult> GetConversations(int tenantId)
    {
        var conversations = await _context.Conversations
            .Where(c => c.TenantId == tenantId)
            .Select(c => new
            {
                c.Id,
                c.WaUserPhone,
                c.Status,
                c.CreatedAt,
                MessageCount = c.Messages.Count()
            })
            .ToListAsync();

        return Ok(conversations);
    }

    [HttpGet("faqs/{tenantId}")]
    public async Task<IActionResult> GetFAQs(int tenantId)
    {
        try
        {
            var faqs = await _context.FAQs
                .IgnoreQueryFilters()
                .Where(f => f.TenantId == tenantId)
                .Select(f => new
                {
                    f.Id,
                    f.Question,
                    f.Answer,
                    f.Language,
                    f.Tags,
                    f.UpdatedAt
                })
                .OrderBy(f => f.Question)
                .ToListAsync();

            return Ok(faqs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving FAQs for tenant {TenantId}", tenantId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("faqs")]
    public async Task<IActionResult> AddFAQ([FromBody] AddFAQRequest request)
    {
        try
        {
            // Check if FAQ with similar question already exists
            var existingFAQ = await _context.FAQs
                .FirstOrDefaultAsync(f => f.TenantId == request.TenantId && 
                                         f.Question.ToLower() == request.Question.ToLower());

            if (existingFAQ != null)
            {
                return BadRequest("FAQ with similar question already exists");
            }

            var faq = new FAQ
            {
                TenantId = request.TenantId,
                Question = request.Question,
                Answer = request.Answer,
                Language = request.Language ?? "en",
                Tags = request.Tags ?? Array.Empty<string>(),
                UpdatedAt = DateTime.UtcNow
            };

            _context.FAQs.Add(faq);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Added new FAQ for tenant {TenantId}: {Question}", 
                request.TenantId, request.Question);

            return Ok(new
            {
                Id = faq.Id,
                Question = faq.Question,
                Answer = faq.Answer,
                Language = faq.Language,
                Tags = faq.Tags,
                UpdatedAt = faq.UpdatedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding FAQ");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("bookings")]
    public async Task<IActionResult> GetBookings()
    {
        try
        {
            var bookings = await _context.Bookings
                .IgnoreQueryFilters()
                .Select(b => new
                {
                    b.Id,
                    b.GuestName,
                    b.Phone,
                    b.Status,
                    b.CheckinDate,
                    b.CheckoutDate,
                    b.TenantId,
                    b.CreatedAt
                })
                .OrderByDescending(b => b.CreatedAt)
                .Take(20)
                .ToListAsync();

            return Ok(bookings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving bookings");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("guest-status/{phoneNumber}")]
    public async Task<ActionResult<object>> TestGuestStatus(string phoneNumber)
    {
        try
        {
            _logger.LogInformation("Testing guest status for phone number: {PhoneNumber}", phoneNumber);
            
            var guestStatus = await _messageRoutingService.DetermineGuestStatusAsync(phoneNumber, 1); // Tenant ID 1
            
            var result = new
            {
                OriginalPhoneNumber = phoneNumber,
                GuestType = guestStatus.Type.ToString(),
                DisplayName = guestStatus.DisplayName,
                IsActive = guestStatus.IsActive,
                CanRequestItems = guestStatus.CanRequestItems,
                CanOrderFood = guestStatus.CanOrderFood,
                StatusMessage = guestStatus.StatusMessage,
                AllowedActions = guestStatus.AllowedActions,
                RestrictedActions = guestStatus.RestrictedActions,
                BookingId = guestStatus.BookingId,
                CheckinDate = guestStatus.CheckinDate,
                CheckoutDate = guestStatus.CheckoutDate,
                BookingStatus = guestStatus.BookingStatus
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing guest status for {PhoneNumber}", phoneNumber);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("emergency-broadcast")]
    public async Task<IActionResult> TestEmergencyBroadcast(
        [FromBody] TestBroadcastRequest request)
    {
        try
        {
            var broadcastService = HttpContext.RequestServices.GetRequiredService<IBroadcastService>();
            var logger = HttpContext.RequestServices.GetRequiredService<ILogger<TestController>>();

            var tenantId = 1; // Use existing panoramaview tenant ID
            
            // Validate message type
            var validTypes = new[] { "emergency", "power_outage", "water_outage", "internet_down", "custom" };
            if (!validTypes.Contains(request.MessageType.ToLower()))
            {
                return BadRequest($"Invalid message type. Valid types: {string.Join(", ", validTypes)}");
            }

            // For custom messages, require custom message content
            if (request.MessageType.ToLower() == "custom" && string.IsNullOrWhiteSpace(request.CustomMessage))
            {
                return BadRequest("Custom message content is required for custom message type");
            }

            var (success, message, broadcastId) = await broadcastService.SendEmergencyBroadcastAsync(
                tenantId,
                request.MessageType,
                request.CustomMessage,
                request.EstimatedRestorationTime,
                "TEST_USER",
                request.BroadcastScope
            );

            if (success)
            {
                logger.LogInformation("TEST: Emergency broadcast initiated by tenant {TenantId}: {BroadcastId}", tenantId, broadcastId);
                
                return Ok(new
                {
                    Success = true,
                    Data = new { Message = message, BroadcastId = broadcastId }
                });
            }
            else
            {
                return BadRequest(new
                {
                    Success = false,
                    Error = message
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing test emergency broadcast request");
            return StatusCode(500, "Internal server error while processing broadcast request");
        }
    }

    [HttpPost("test-department-resolution")]
    public async Task<IActionResult> TestDepartmentResolution([FromBody] TestDepartmentRequest request)
    {
        try
        {
            var tenantDepartmentService = HttpContext.RequestServices.GetRequiredService<ITenantDepartmentService>();

            var department = await tenantDepartmentService.ResolveDepartmentAsync(
                request.TenantId,
                request.ServiceCategory,
                request.ItemName,
                request.RequiresRoomDelivery
            );

            return Ok(new
            {
                TenantId = request.TenantId,
                ServiceCategory = request.ServiceCategory,
                ItemName = request.ItemName,
                RequiresRoomDelivery = request.RequiresRoomDelivery,
                ResolvedDepartment = department
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing department resolution");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("test-action-processing")]
    public async Task<IActionResult> TestActionProcessing([FromBody] TestActionRequest request)
    {
        try
        {
            // Get existing conversation
            var conversation = await _context.Conversations
                .FirstAsync(c => c.Id == request.ConversationId);

            var tenant = await _context.Tenants
                .FirstAsync(t => t.Id == conversation.TenantId);

            var tenantContext = new TenantContext
            {
                TenantId = conversation.TenantId,
                TenantSlug = tenant.Slug
            };

            // Create a mock routing response with our test action
            var routingResponse = new MessageRoutingResponse
            {
                Reply = "Test reply",
                Actions = new List<System.Text.Json.JsonElement> { request.TestAction }
            };

            // Process the action
            await _actionProcessingService.ProcessActionsAsync(
                tenantContext, conversation, routingResponse);

            // Get the created task
            var task = await _context.StaffTasks
                .Where(t => t.ConversationId == conversation.Id)
                .OrderByDescending(t => t.CreatedAt)
                .FirstOrDefaultAsync();

            return Ok(new
            {
                Success = true,
                CreatedTask = task != null ? new
                {
                    task.Id,
                    task.Title,
                    task.Department,
                    task.RoomNumber,
                    task.GuestName,
                    task.Priority,
                    task.Status,
                    task.CreatedAt
                } : null
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing action processing");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        try
        {
            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null)
            {
                return BadRequest("User not found");
            }

            // Remove current password and set new one
            await _userManager.RemovePasswordAsync(user);
            var result = await _userManager.AddPasswordAsync(user, request.NewPassword);

            if (result.Succeeded)
            {
                return Ok(new { message = "Password reset successfully" });
            }

            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting password");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("set-test-password")]
    public async Task<IActionResult> SetTestPassword()
    {
        try
        {
            // Use a known working ASP.NET Identity hash for "Password123!"
            var knownHash = "AQAAAAEAACcQAAAAEPTGNZ3O8J9H8V5z8L6Oe8sLFgAE3F5h8N0o8Y9oLKz5E1g8F4Y3N8h1J7o5K8Z2M=";

            // Update password hash directly using raw SQL to avoid DateTime issues
            var sql = "UPDATE \"AspNetUsers\" SET \"PasswordHash\" = @hash WHERE \"Email\" = @email";
            await _context.Database.ExecuteSqlRawAsync(sql,
                new Npgsql.NpgsqlParameter("@hash", knownHash),
                new Npgsql.NpgsqlParameter("@email", "test@admin.com"));

            return Ok(new { message = "Test password set successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting test password");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("test-signalr-notification")]
    public async Task<IActionResult> TestSignalRNotification([FromQuery] int tenantId = 1)
    {
        try
        {
            // Create a test task to trigger the SignalR notification
            var testTask = new StaffTask
            {
                TenantId = tenantId,
                Title = "1x Android Charger",
                Description = "Guest requested an Android charger for room 101",
                TaskType = "deliver_item",
                Department = "Housekeeping",
                Priority = "Normal",
                Status = "Open",
                RoomNumber = "101",
                Quantity = 1,
                Notes = "Test task created via API test endpoint",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Add to database
            _context.StaffTasks.Add(testTask);
            await _context.SaveChangesAsync();

            // Send SignalR notification
            await _notificationService.NotifyTaskCreatedAsync(tenantId, testTask);

            return Ok(new
            {
                success = true,
                message = "SignalR notification sent successfully",
                taskId = testTask.Id,
                title = testTask.Title,
                tenantId = tenantId,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing SignalR notification");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("seed-aromatherapy-massage")]
    public async Task<IActionResult> SeedAromatherapyMassage()
    {
        try
        {
            // Check if Aromatherapy Massage already exists
            var existing = await _context.Services
                .FirstOrDefaultAsync(s => s.TenantId == 1 && s.Name == "Aromatherapy Massage");

            if (existing != null)
            {
                return Ok(new
                {
                    success = true,
                    message = "Aromatherapy Massage already exists",
                    serviceId = existing.Id
                });
            }

            // Add Aromatherapy Massage service
            var aromatherapyMassage = new Service
            {
                TenantId = 1,
                Name = "Aromatherapy Massage",
                Description = "Therapeutic massage using essential oils and aromatherapy techniques",
                Category = "Wellness",
                IsAvailable = true,
                IsChargeable = true,
                Price = 480.00m,
                Currency = "ZAR",
                PricingUnit = "per session",
                AvailableHours = "9:00 AM - 8:00 PM",
                ContactInfo = "+27 13 795 5555",
                RequiresAdvanceBooking = true,
                AdvanceBookingHours = 2,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Services.Add(aromatherapyMassage);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Added Aromatherapy Massage service with ID {Id}", aromatherapyMassage.Id);

            return Ok(new
            {
                success = true,
                message = "Aromatherapy Massage service added successfully",
                serviceId = aromatherapyMassage.Id
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding Aromatherapy Massage service");
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    [HttpPost("test-email")]
    public async Task<IActionResult> TestEmail([FromBody] TestEmailRequest request)
    {
        try
        {
            var result = await _emailService.SendEmailAsync(
                request.ToEmail,
                "Test Email from Hostr Platform",
                $@"<html>
<body style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px;'>
    <div style='background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); padding: 30px; border-radius: 10px 10px 0 0;'>
        <h1 style='color: white; margin: 0;'>🎉 Email Test Successful!</h1>
    </div>
    <div style='background: #f8f9fa; padding: 30px; border-radius: 0 0 10px 10px;'>
        <h2 style='color: #333;'>Hello from Hostr!</h2>
        <p style='color: #666; line-height: 1.6;'>
            This is a test email from the Hostr Platform. If you're receiving this,
            it means the email system is working correctly! 🚀
        </p>
        <p style='color: #666; line-height: 1.6;'>
            {request.CustomMessage ?? "No custom message provided."}
        </p>
        <div style='background: white; padding: 20px; border-left: 4px solid #667eea; margin: 20px 0;'>
            <p style='margin: 0; color: #333;'><strong>Test Details:</strong></p>
            <p style='margin: 5px 0; color: #666;'>Sent at: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC</p>
            <p style='margin: 5px 0; color: #666;'>From: Hostr Platform</p>
        </div>
        <p style='color: #999; font-size: 12px; margin-top: 30px;'>
            This is an automated test email from Hostr Platform.
            Please do not reply to this email.
        </p>
    </div>
</body>
</html>",
                $"Test Email from Hostr Platform\n\nThis is a test email. If you're reading this, the email system works!\n\n{request.CustomMessage ?? ""}\n\nSent at: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");

            if (result)
            {
                _logger.LogInformation("Test email sent successfully to {Email}", request.ToEmail);
                return Ok(new
                {
                    success = true,
                    message = $"Test email sent successfully to {request.ToEmail}",
                    timestamp = DateTime.UtcNow
                });
            }
            else
            {
                _logger.LogWarning("Failed to send test email to {Email}", request.ToEmail);
                return StatusCode(500, new
                {
                    success = false,
                    error = "Failed to send email. Check server logs for details."
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending test email to {Email}", request.ToEmail);
            return StatusCode(500, new
            {
                success = false,
                error = ex.Message
            });
        }
    }
}

public class TestEmailRequest
{
    public string ToEmail { get; set; } = string.Empty;
    public string? CustomMessage { get; set; }
}

public class TestDepartmentRequest
{
    public int TenantId { get; set; }
    public string ServiceCategory { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public bool RequiresRoomDelivery { get; set; }
}

public class TestActionRequest
{
    public int ConversationId { get; set; }
    public System.Text.Json.JsonElement TestAction { get; set; }
}

public class SimulateMessageRequest
{
    public int TenantId { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public string MessageText { get; set; } = string.Empty;

    // Support both MessageText and Message for backwards compatibility
    public string Message
    {
        get => string.IsNullOrEmpty(MessageText) ? string.Empty : MessageText;
        set => MessageText = value;
    }
}

public class TestBroadcastRequest
{
    public string MessageType { get; set; } = string.Empty; // emergency, power_outage, water_outage, internet_down, custom
    public string? CustomMessage { get; set; }
    public string? EstimatedRestorationTime { get; set; }
    public BroadcastScope BroadcastScope { get; set; } = BroadcastScope.ActiveOnly;
}

public class AddFAQRequest
{
    public int TenantId { get; set; }
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public string? Language { get; set; }
    public string[]? Tags { get; set; }
}

public class ResetPasswordRequest
{
    public string Email { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}