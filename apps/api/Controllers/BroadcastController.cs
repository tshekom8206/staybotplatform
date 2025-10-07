using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.ComponentModel.DataAnnotations;
using Hostr.Api.Services;
using Hostr.Api.Models;
using Hostr.Contracts.DTOs.Common;

namespace Hostr.Api.Controllers;

[ApiController]
[Route("api/tenant/[controller]")]
[Authorize]
public class BroadcastController : ControllerBase
{
    private readonly IBroadcastService _broadcastService;
    private readonly ILogger<BroadcastController> _logger;

    public BroadcastController(
        IBroadcastService broadcastService,
        ILogger<BroadcastController> logger)
    {
        _broadcastService = broadcastService;
        _logger = logger;
    }

    /// <summary>
    /// Test emergency broadcast without authentication (temporary test endpoint)
    /// </summary>
    /// <param name="request">Broadcast request details</param>
    /// <returns>Broadcast status and ID</returns>
    [HttpPost("test")]
    [AllowAnonymous]
    public async Task<IActionResult> TestEmergencyBroadcast([FromBody] TestBroadcastRequest request)
    {
        try
        {
            var tenantId = 1; // Use existing panoramaview tenant ID
            
            // Validate message type
            var validTypes = new[] { "emergency", "power_outage", "water_outage", "internet_down", "custom" };
            if (!validTypes.Contains(request.MessageType.ToLower()))
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Error = $"Invalid message type. Valid types: {string.Join(", ", validTypes)}"
                });
            }

            // For custom messages, require custom message content
            if (request.MessageType.ToLower() == "custom" && string.IsNullOrWhiteSpace(request.CustomMessage))
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Error = "Custom message content is required for custom message type"
                });
            }

            var (success, message, broadcastId) = await _broadcastService.SendEmergencyBroadcastAsync(
                tenantId,
                request.MessageType,
                request.CustomMessage,
                request.EstimatedRestorationTime,
                "TEST_USER",
                request.BroadcastScope
            );

            if (success)
            {
                _logger.LogInformation("TEST: Emergency broadcast initiated by tenant {TenantId}: {BroadcastId}", tenantId, broadcastId);
                
                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Data = new { Message = message, BroadcastId = broadcastId }
                });
            }
            else
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Error = message
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing test emergency broadcast request");
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Error = "Internal server error while processing broadcast request"
            });
        }
    }

    /// <summary>
    /// Send emergency broadcast message to all active guests
    /// </summary>
    /// <param name="request">Broadcast request details</param>
    /// <returns>Broadcast status and ID</returns>
    [HttpPost]
    public async Task<IActionResult> SendEmergencyBroadcast([FromBody] EmergencyBroadcastRequest request)
    {
        try
        {
            var tenantId = int.Parse(HttpContext.Items["TenantId"]?.ToString() ?? "0");
            
            if (tenantId == 0)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Error = "Invalid tenant context"
                });
            }

            // Validate message type
            var validTypes = new[] { "emergency", "power_outage", "water_outage", "internet_down", "custom" };
            if (!validTypes.Contains(request.MessageType.ToLower()))
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Error = $"Invalid message type. Valid types: {string.Join(", ", validTypes)}"
                });
            }

            // For custom messages, require custom message content
            if (request.MessageType.ToLower() == "custom" && string.IsNullOrWhiteSpace(request.CustomMessage))
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Error = "Custom message content is required for custom message type"
                });
            }

            var (success, message, broadcastId) = await _broadcastService.SendEmergencyBroadcastAsync(
                tenantId,
                request.MessageType,
                request.CustomMessage,
                request.EstimatedRestorationTime,
                "API_User", // Could be enhanced to get actual user ID from JWT
                request.BroadcastScope
            );

            if (success)
            {
                _logger.LogInformation("Emergency broadcast initiated by tenant {TenantId}: {BroadcastId}", tenantId, broadcastId);
                
                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Data = new { Message = message, BroadcastId = broadcastId }
                });
            }
            else
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Error = message
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing emergency broadcast request");
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Error = "Internal server error while processing broadcast request"
            });
        }
    }

    /// <summary>
    /// Get broadcast status and delivery details
    /// </summary>
    /// <param name="broadcastId">Broadcast ID</param>
    /// <returns>Broadcast status and delivery statistics</returns>
    [HttpGet("{broadcastId}")]
    public async Task<IActionResult> GetBroadcastStatus(int broadcastId)
    {
        try
        {
            var tenantId = int.Parse(HttpContext.Items["TenantId"]?.ToString() ?? "0");
            
            var broadcast = await _broadcastService.GetBroadcastStatusAsync(broadcastId, tenantId);
            
            if (broadcast == null)
            {
                return NotFound(new ApiResponse<object>
                {
                    Success = false,
                    Error = "Broadcast not found"
                });
            }

            var response = new BroadcastStatusResponse
            {
                Id = broadcast.Id,
                MessageType = broadcast.MessageType,
                Content = broadcast.Content,
                EstimatedRestorationTime = broadcast.EstimatedRestorationTime,
                Status = broadcast.Status,
                TotalRecipients = broadcast.TotalRecipients,
                SuccessfulDeliveries = broadcast.SuccessfulDeliveries,
                FailedDeliveries = broadcast.FailedDeliveries,
                CreatedAt = broadcast.CreatedAt,
                CompletedAt = broadcast.CompletedAt,
                CreatedBy = broadcast.CreatedBy
            };

            return Ok(new ApiResponse<BroadcastStatusResponse>
            {
                Success = true,
                Data = response
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving broadcast status for ID {BroadcastId}", broadcastId);
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Error = "Internal server error while retrieving broadcast status"
            });
        }
    }

    /// <summary>
    /// Get recent broadcast history for tenant
    /// </summary>
    /// <param name="limit">Number of records to return (default: 10, max: 50)</param>
    /// <returns>List of recent broadcasts</returns>
    [HttpGet("history")]
    public async Task<IActionResult> GetBroadcastHistory([FromQuery] int limit = 10)
    {
        try
        {
            var tenantId = int.Parse(HttpContext.Items["TenantId"]?.ToString() ?? "0");

            // Limit the maximum records returned
            limit = Math.Min(Math.Max(limit, 1), 50);

            var broadcasts = await _broadcastService.GetRecentBroadcastsAsync(tenantId, limit);

            var response = broadcasts.Select(b => new BroadcastHistoryItem
            {
                Id = b.Id,
                MessageType = b.MessageType,
                Status = b.Status,
                TotalRecipients = b.TotalRecipients,
                SuccessfulDeliveries = b.SuccessfulDeliveries,
                FailedDeliveries = b.FailedDeliveries,
                CreatedAt = b.CreatedAt,
                CompletedAt = b.CompletedAt,
                CreatedBy = b.CreatedBy
            }).ToList();

            return Ok(new ApiResponse<List<BroadcastHistoryItem>>
            {
                Success = true,
                Data = response
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving broadcast history for tenant");
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Error = "Internal server error while retrieving broadcast history"
            });
        }
    }

    /// <summary>
    /// Send general broadcast message to selected recipient groups
    /// </summary>
    /// <param name="request">General broadcast request details</param>
    /// <returns>Broadcast status and ID</returns>
    [HttpPost("general")]
    public async Task<IActionResult> SendGeneralBroadcast([FromBody] GeneralBroadcastRequest request)
    {
        try
        {
            var tenantId = int.Parse(HttpContext.Items["TenantId"]?.ToString() ?? "0");

            if (tenantId == 0)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Error = "Invalid tenant context"
                });
            }

            var (success, message, broadcastId) = await _broadcastService.SendGeneralBroadcastAsync(
                tenantId,
                request.Title,
                request.Content,
                request.Recipients,
                request.Priority,
                request.ScheduledAt,
                "API_User" // Could be enhanced to get actual user ID from JWT
            );

            if (success)
            {
                _logger.LogInformation("General broadcast initiated by tenant {TenantId}: {BroadcastId}", tenantId, broadcastId);

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Data = new { Message = message, BroadcastId = broadcastId }
                });
            }
            else
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Error = message
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing general broadcast request");
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Error = "Internal server error while processing broadcast request"
            });
        }
    }

    /// <summary>
    /// Get available recipient groups for tenant
    /// </summary>
    /// <returns>List of available recipient groups with counts</returns>
    [HttpGet("recipients")]
    public async Task<IActionResult> GetRecipientGroups()
    {
        try
        {
            var tenantId = int.Parse(HttpContext.Items["TenantId"]?.ToString() ?? "0");

            var recipients = await _broadcastService.GetRecipientGroupsAsync(tenantId);

            return Ok(new ApiResponse<List<Services.RecipientGroup>>
            {
                Success = true,
                Data = recipients
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving recipient groups for tenant");
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Error = "Internal server error while retrieving recipient groups"
            });
        }
    }

    /// <summary>
    /// Get all broadcast templates for the tenant
    /// </summary>
    /// <returns>List of broadcast templates</returns>
    [HttpGet("templates")]
    public async Task<IActionResult> GetTemplates()
    {
        try
        {
            var tenantId = int.Parse(HttpContext.Items["TenantId"]?.ToString() ?? "0");

            var templates = await _broadcastService.GetTemplatesAsync(tenantId);

            return Ok(new ApiResponse<IEnumerable<BroadcastTemplate>>
            {
                Success = true,
                Data = templates
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving templates for tenant");
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Error = "Internal server error while retrieving templates"
            });
        }
    }

    /// <summary>
    /// Get a specific broadcast template by ID
    /// </summary>
    /// <param name="templateId">Template ID</param>
    /// <returns>Broadcast template</returns>
    [HttpGet("templates/{templateId}")]
    public async Task<IActionResult> GetTemplate(int templateId)
    {
        try
        {
            var tenantId = int.Parse(HttpContext.Items["TenantId"]?.ToString() ?? "0");

            var template = await _broadcastService.GetTemplateByIdAsync(templateId, tenantId);

            if (template == null)
            {
                return NotFound(new ApiResponse<object>
                {
                    Success = false,
                    Error = "Template not found"
                });
            }

            return Ok(new ApiResponse<BroadcastTemplate>
            {
                Success = true,
                Data = template
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving template {TemplateId}", templateId);
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Error = "Internal server error while retrieving template"
            });
        }
    }

    /// <summary>
    /// Create a new broadcast template
    /// </summary>
    /// <param name="request">Template details</param>
    /// <returns>Created template</returns>
    [HttpPost("templates")]
    public async Task<IActionResult> CreateTemplate([FromBody] CreateTemplateRequest request)
    {
        try
        {
            var tenantId = int.Parse(HttpContext.Items["TenantId"]?.ToString() ?? "0");

            var template = new BroadcastTemplate
            {
                TenantId = tenantId,
                Name = request.Name,
                Category = request.Category,
                Subject = request.Subject,
                Content = request.Content,
                IsActive = request.IsActive,
                IsDefault = request.IsDefault,
                CreatedBy = "API_User" // Could be enhanced to get actual user ID from JWT
            };

            var createdTemplate = await _broadcastService.CreateTemplateAsync(template);

            return CreatedAtAction(nameof(GetTemplate), new { templateId = createdTemplate.Id },
                new ApiResponse<BroadcastTemplate>
                {
                    Success = true,
                    Data = createdTemplate
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating template");
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Error = "Internal server error while creating template"
            });
        }
    }

    /// <summary>
    /// Update an existing broadcast template
    /// </summary>
    /// <param name="templateId">Template ID</param>
    /// <param name="request">Updated template details</param>
    /// <returns>Updated template</returns>
    [HttpPut("templates/{templateId}")]
    public async Task<IActionResult> UpdateTemplate(int templateId, [FromBody] UpdateTemplateRequest request)
    {
        try
        {
            var tenantId = int.Parse(HttpContext.Items["TenantId"]?.ToString() ?? "0");

            var template = new BroadcastTemplate
            {
                TenantId = tenantId,
                Name = request.Name,
                Category = request.Category,
                Subject = request.Subject,
                Content = request.Content,
                IsActive = request.IsActive,
                IsDefault = request.IsDefault
            };

            var updatedTemplate = await _broadcastService.UpdateTemplateAsync(templateId, template);

            if (updatedTemplate == null)
            {
                return NotFound(new ApiResponse<object>
                {
                    Success = false,
                    Error = "Template not found"
                });
            }

            return Ok(new ApiResponse<BroadcastTemplate>
            {
                Success = true,
                Data = updatedTemplate
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating template {TemplateId}", templateId);
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Error = "Internal server error while updating template"
            });
        }
    }

    /// <summary>
    /// Delete a broadcast template
    /// </summary>
    /// <param name="templateId">Template ID</param>
    /// <returns>Success status</returns>
    [HttpDelete("templates/{templateId}")]
    public async Task<IActionResult> DeleteTemplate(int templateId)
    {
        try
        {
            var tenantId = int.Parse(HttpContext.Items["TenantId"]?.ToString() ?? "0");

            var success = await _broadcastService.DeleteTemplateAsync(templateId, tenantId);

            if (!success)
            {
                return NotFound(new ApiResponse<object>
                {
                    Success = false,
                    Error = "Template not found"
                });
            }

            return Ok(new ApiResponse<object>
            {
                Success = true,
                Data = new { Message = "Template deleted successfully" }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting template {TemplateId}", templateId);
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Error = "Internal server error while deleting template"
            });
        }
    }

    /// <summary>
    /// Set a template as default for its category
    /// </summary>
    /// <param name="templateId">Template ID</param>
    /// <returns>Success status</returns>
    [HttpPost("templates/{templateId}/set-default")]
    public async Task<IActionResult> SetDefaultTemplate(int templateId)
    {
        try
        {
            var tenantId = int.Parse(HttpContext.Items["TenantId"]?.ToString() ?? "0");

            // Get the template to know its category
            var template = await _broadcastService.GetTemplateByIdAsync(templateId, tenantId);
            if (template == null)
            {
                return NotFound(new ApiResponse<object>
                {
                    Success = false,
                    Error = "Template not found"
                });
            }

            var success = await _broadcastService.SetDefaultTemplateAsync(templateId, tenantId, template.Category);

            if (!success)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Error = "Failed to set template as default"
                });
            }

            return Ok(new ApiResponse<object>
            {
                Success = true,
                Data = new { Message = "Template set as default successfully" }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting template {TemplateId} as default", templateId);
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Error = "Internal server error while setting default template"
            });
        }
    }
}

public class EmergencyBroadcastRequest
{
    [Required]
    [MaxLength(50)]
    public string MessageType { get; set; } = string.Empty; // emergency, power_outage, water_outage, internet_down, custom

    [MaxLength(1000)]
    public string? CustomMessage { get; set; }

    [MaxLength(100)]
    public string? EstimatedRestorationTime { get; set; }
    
    public BroadcastScope BroadcastScope { get; set; } = BroadcastScope.ActiveOnly;
}

public class BroadcastStatusResponse
{
    public int Id { get; set; }
    public string MessageType { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? EstimatedRestorationTime { get; set; }
    public string Status { get; set; } = string.Empty;
    public int TotalRecipients { get; set; }
    public int SuccessfulDeliveries { get; set; }
    public int FailedDeliveries { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
}

public class BroadcastHistoryItem
{
    public int Id { get; set; }
    public string MessageType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int TotalRecipients { get; set; }
    public int SuccessfulDeliveries { get; set; }
    public int FailedDeliveries { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
}

public class GeneralBroadcastRequest
{
    [Required]
    [MaxLength(100)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Content { get; set; } = string.Empty;

    [Required]
    public List<string> Recipients { get; set; } = new List<string>();

    [Required]
    public string Priority { get; set; } = "medium"; // low, medium, high, urgent

    public DateTime? ScheduledAt { get; set; }
}

public class CreateTemplateRequest
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Category { get; set; } = "general";

    [Required]
    [MaxLength(200)]
    public string Subject { get; set; } = string.Empty;

    [Required]
    public string Content { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public bool IsDefault { get; set; } = false;
}

public class UpdateTemplateRequest
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Category { get; set; } = "general";

    [Required]
    [MaxLength(200)]
    public string Subject { get; set; } = string.Empty;

    [Required]
    public string Content { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public bool IsDefault { get; set; } = false;
}
