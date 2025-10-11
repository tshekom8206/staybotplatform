using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Hostr.Api.Data;
using Hostr.Api.Models;
using Hostr.Api.DTOs;

namespace Hostr.Api.Controllers;

[ApiController]
[Route("api/admin")]
public class AdminBusinessRulesController : ControllerBase
{
    private readonly HostrDbContext _context;
    private readonly ILogger<AdminBusinessRulesController> _logger;

    public AdminBusinessRulesController(HostrDbContext context, ILogger<AdminBusinessRulesController> logger)
    {
        _context = context;
        _logger = logger;
    }

    private async Task CreateAuditLog(int tenantId, string action, string entity, int entityId, object? changesBefore = null, object? changesAfter = null)
    {
        try
        {
            var diffJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                before = changesBefore,
                after = changesAfter
            });

            var auditLog = new AuditLog
            {
                TenantId = tenantId,
                ActorUserId = null, // TODO: Get from authentication context when implemented
                Action = action,
                Entity = entity,
                EntityId = entityId,
                DiffJson = diffJson,
                CreatedAt = DateTime.UtcNow
            };

            _context.AuditLogs.Add(auditLog);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating audit log for {Action} on {Entity} {EntityId}", action, entity, entityId);
            // Don't throw - audit logging failure shouldn't break the main operation
        }
    }

    private static string? ParseJsonProperty(string? json, string propertyName)
    {
        if (string.IsNullOrEmpty(json)) return null;

        try
        {
            var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty(propertyName, out var property))
            {
                return property.ToString();
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    #region Service Business Rules

    // GET: api/admin/services/{tenantId}
    [HttpGet("services/{tenantId}")]
    public async Task<ActionResult<List<ServiceWithRulesDTO>>> GetServices(
        int tenantId,
        [FromQuery] string? searchTerm = null,
        [FromQuery] string? category = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] bool? hasRules = null,
        [FromQuery] string? sortBy = "name",
        [FromQuery] string? sortDirection = "asc")
    {
        try
        {
            var query = _context.Services
                .Where(s => s.TenantId == tenantId)
                .AsQueryable();

            // Apply filters
            if (!string.IsNullOrEmpty(searchTerm))
            {
                var term = searchTerm.ToLower();
                query = query.Where(s => s.Name.ToLower().Contains(term) ||
                                        (s.Description != null && s.Description.ToLower().Contains(term)));
            }

            if (!string.IsNullOrEmpty(category))
            {
                query = query.Where(s => s.Category == category);
            }

            if (isActive.HasValue)
            {
                query = query.Where(s => s.IsAvailable == isActive.Value);
            }

            // Get rule counts
            var services = await query.Select(s => new
            {
                Service = s,
                RuleCount = _context.ServiceBusinessRules.Count(r => r.ServiceId == s.Id),
                ActiveRuleCount = _context.ServiceBusinessRules.Count(r => r.ServiceId == s.Id && r.IsActive)
            }).ToListAsync();

            if (hasRules.HasValue && hasRules.Value)
            {
                services = services.Where(s => s.RuleCount > 0).ToList();
            }
            else if (hasRules.HasValue && !hasRules.Value)
            {
                services = services.Where(s => s.RuleCount == 0).ToList();
            }

            // Map to DTOs
            var result = services.Select(s => new ServiceWithRulesDTO
            {
                Id = s.Service.Id,
                TenantId = s.Service.TenantId,
                Name = s.Service.Name,
                Category = s.Service.Category,
                Description = s.Service.Description,
                IsActive = s.Service.IsAvailable,
                RuleCount = s.RuleCount,
                ActiveRuleCount = s.ActiveRuleCount,
                LastModified = s.Service.UpdatedAt,
                CreatedAt = s.Service.CreatedAt,
                UpdatedAt = s.Service.UpdatedAt
            }).ToList();

            // Apply sorting
            result = sortBy?.ToLower() switch
            {
                "category" => sortDirection == "desc" ? result.OrderByDescending(s => s.Category).ToList() : result.OrderBy(s => s.Category).ToList(),
                "rulecount" => sortDirection == "desc" ? result.OrderByDescending(s => s.RuleCount).ToList() : result.OrderBy(s => s.RuleCount).ToList(),
                "lastmodified" => sortDirection == "desc" ? result.OrderByDescending(s => s.LastModified).ToList() : result.OrderBy(s => s.LastModified).ToList(),
                _ => sortDirection == "desc" ? result.OrderByDescending(s => s.Name).ToList() : result.OrderBy(s => s.Name).ToList()
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting services for tenant {TenantId}", tenantId);
            return StatusCode(500, new { error = "An error occurred while retrieving services" });
        }
    }

    // GET: api/admin/services/{tenantId}/{serviceId}
    [HttpGet("services/{tenantId}/{serviceId}")]
    public async Task<ActionResult<ServiceWithRulesDTO>> GetService(int tenantId, int serviceId)
    {
        try
        {
            var service = await _context.Services
                .Where(s => s.TenantId == tenantId && s.Id == serviceId)
                .FirstOrDefaultAsync();

            if (service == null)
            {
                return NotFound(new { error = "Service not found" });
            }

            var ruleCount = await _context.ServiceBusinessRules.CountAsync(r => r.ServiceId == serviceId);
            var activeRuleCount = await _context.ServiceBusinessRules.CountAsync(r => r.ServiceId == serviceId && r.IsActive);

            var result = new ServiceWithRulesDTO
            {
                Id = service.Id,
                TenantId = service.TenantId,
                Name = service.Name,
                Category = service.Category,
                Description = service.Description,
                IsActive = service.IsAvailable,
                RuleCount = ruleCount,
                ActiveRuleCount = activeRuleCount,
                LastModified = service.UpdatedAt,
                CreatedAt = service.CreatedAt,
                UpdatedAt = service.UpdatedAt
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting service {ServiceId} for tenant {TenantId}", serviceId, tenantId);
            return StatusCode(500, new { error = "An error occurred while retrieving the service" });
        }
    }

    // GET: api/admin/services/{tenantId}/{serviceId}/rules
    [HttpGet("services/{tenantId}/{serviceId}/rules")]
    public async Task<ActionResult<List<ServiceBusinessRule>>> GetServiceRules(int tenantId, int serviceId)
    {
        try
        {
            var rules = await _context.ServiceBusinessRules
                .Where(r => r.TenantId == tenantId && r.ServiceId == serviceId)
                .OrderByDescending(r => r.Priority)
                .ThenBy(r => r.CreatedAt)
                .ToListAsync();

            return Ok(rules);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting rules for service {ServiceId}", serviceId);
            return StatusCode(500, new { error = "An error occurred while retrieving service rules" });
        }
    }

    // POST: api/admin/services/{tenantId}/{serviceId}/rules
    [HttpPost("services/{tenantId}/{serviceId}/rules")]
    public async Task<ActionResult<ServiceBusinessRule>> CreateServiceRule(
        int tenantId,
        int serviceId,
        [FromBody] CreateServiceRuleRequest request)
    {
        try
        {
            // Verify service exists
            var serviceExists = await _context.Services
                .AnyAsync(s => s.TenantId == tenantId && s.Id == serviceId);

            if (!serviceExists)
            {
                return NotFound(new { error = "Service not found" });
            }

            var rule = new ServiceBusinessRule
            {
                TenantId = tenantId,
                ServiceId = serviceId,
                RuleType = request.RuleType,
                RuleKey = request.RuleKey,
                RuleValue = request.RuleValue,
                ValidationMessage = request.ValidationMessage,
                Priority = request.Priority,
                IsActive = request.IsActive,
                MinConfidenceScore = request.MinConfidenceScore,
                Notes = request.Notes,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.ServiceBusinessRules.Add(rule);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created service rule {RuleId} for service {ServiceId}", rule.Id, serviceId);

            return CreatedAtAction(nameof(GetServiceRules), new { tenantId, serviceId }, rule);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating service rule for service {ServiceId}", serviceId);
            return StatusCode(500, new { error = "An error occurred while creating the service rule" });
        }
    }

    // PUT: api/admin/services/{tenantId}/{serviceId}/rules/{ruleId}
    [HttpPut("services/{tenantId}/{serviceId}/rules/{ruleId}")]
    public async Task<ActionResult<ServiceBusinessRule>> UpdateServiceRule(
        int tenantId,
        int serviceId,
        int ruleId,
        [FromBody] UpdateServiceRuleRequest request)
    {
        try
        {
            var rule = await _context.ServiceBusinessRules
                .Where(r => r.TenantId == tenantId && r.ServiceId == serviceId && r.Id == ruleId)
                .FirstOrDefaultAsync();

            if (rule == null)
            {
                return NotFound(new { error = "Rule not found" });
            }

            rule.RuleType = request.RuleType;
            rule.RuleKey = request.RuleKey;
            rule.RuleValue = request.RuleValue;
            rule.ValidationMessage = request.ValidationMessage;
            rule.Priority = request.Priority;
            rule.IsActive = request.IsActive;
            rule.MinConfidenceScore = request.MinConfidenceScore;
            rule.Notes = request.Notes;
            rule.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Updated service rule {RuleId}", ruleId);

            return Ok(rule);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating service rule {RuleId}", ruleId);
            return StatusCode(500, new { error = "An error occurred while updating the service rule" });
        }
    }

    // DELETE: api/admin/services/{tenantId}/{serviceId}/rules/{ruleId}
    [HttpDelete("services/{tenantId}/{serviceId}/rules/{ruleId}")]
    public async Task<ActionResult> DeleteServiceRule(int tenantId, int serviceId, int ruleId)
    {
        try
        {
            var rule = await _context.ServiceBusinessRules
                .Where(r => r.TenantId == tenantId && r.ServiceId == serviceId && r.Id == ruleId)
                .FirstOrDefaultAsync();

            if (rule == null)
            {
                return NotFound(new { error = "Rule not found" });
            }

            _context.ServiceBusinessRules.Remove(rule);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Deleted service rule {RuleId}", ruleId);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting service rule {RuleId}", ruleId);
            return StatusCode(500, new { error = "An error occurred while deleting the service rule" });
        }
    }

    // PATCH: api/admin/services/{tenantId}/{serviceId}/rules/{ruleId}/toggle
    [HttpPatch("services/{tenantId}/{serviceId}/rules/{ruleId}/toggle")]
    public async Task<ActionResult> ToggleServiceRule(
        int tenantId,
        int serviceId,
        int ruleId,
        [FromBody] ToggleRequest request)
    {
        try
        {
            var rule = await _context.ServiceBusinessRules
                .Where(r => r.TenantId == tenantId && r.ServiceId == serviceId && r.Id == ruleId)
                .FirstOrDefaultAsync();

            if (rule == null)
            {
                return NotFound(new { error = "Rule not found" });
            }

            rule.IsActive = request.IsActive;
            rule.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Toggled service rule {RuleId} to {IsActive}", ruleId, request.IsActive);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling service rule {RuleId}", ruleId);
            return StatusCode(500, new { error = "An error occurred while toggling the service rule" });
        }
    }

    #endregion

    #region Request Item Rules

    // GET: api/admin/request-items/{tenantId}
    [HttpGet("request-items/{tenantId}")]
    public async Task<ActionResult<List<RequestItemWithRulesDTO>>> GetRequestItems(
        int tenantId,
        [FromQuery] string? searchTerm = null,
        [FromQuery] string? category = null,
        [FromQuery] string? department = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] bool? hasRules = null,
        [FromQuery] string? sortBy = "name",
        [FromQuery] string? sortDirection = "asc")
    {
        try
        {
            var query = _context.RequestItems
                .Where(r => r.TenantId == tenantId)
                .AsQueryable();

            // Apply filters
            if (!string.IsNullOrEmpty(searchTerm))
            {
                var term = searchTerm.ToLower();
                query = query.Where(r => r.Name.ToLower().Contains(term) ||
                                        (r.Description != null && r.Description.ToLower().Contains(term)));
            }

            if (!string.IsNullOrEmpty(category))
            {
                query = query.Where(r => r.Category == category);
            }

            if (!string.IsNullOrEmpty(department))
            {
                query = query.Where(r => r.Department == department);
            }

            if (isActive.HasValue)
            {
                query = query.Where(r => r.IsAvailable == isActive.Value);
            }

            // Get rule counts
            var items = await query.Select(item => new
            {
                Item = item,
                RuleCount = _context.RequestItemRules.Count(r => r.RequestItemId == item.Id),
                ActiveRuleCount = _context.RequestItemRules.Count(r => r.RequestItemId == item.Id && r.IsActive)
            }).ToListAsync();

            if (hasRules.HasValue && hasRules.Value)
            {
                items = items.Where(i => i.RuleCount > 0).ToList();
            }
            else if (hasRules.HasValue && !hasRules.Value)
            {
                items = items.Where(i => i.RuleCount == 0).ToList();
            }

            // Map to DTOs
            var result = items.Select(i => new RequestItemWithRulesDTO
            {
                Id = i.Item.Id,
                TenantId = i.Item.TenantId,
                Name = i.Item.Name,
                Description = i.Item.Description,
                Category = i.Item.Category,
                Department = i.Item.Department,
                StockQuantity = i.Item.StockCount,
                IsActive = i.Item.IsAvailable,
                RuleCount = i.RuleCount,
                ActiveRuleCount = i.ActiveRuleCount,
                LastModified = i.Item.UpdatedAt,
                CreatedAt = DateTime.UtcNow, // RequestItem doesn't have CreatedAt
                UpdatedAt = i.Item.UpdatedAt
            }).ToList();

            // Apply sorting
            result = sortBy?.ToLower() switch
            {
                "category" => sortDirection == "desc" ? result.OrderByDescending(i => i.Category).ToList() : result.OrderBy(i => i.Category).ToList(),
                "department" => sortDirection == "desc" ? result.OrderByDescending(i => i.Department).ToList() : result.OrderBy(i => i.Department).ToList(),
                "rulecount" => sortDirection == "desc" ? result.OrderByDescending(i => i.RuleCount).ToList() : result.OrderBy(i => i.RuleCount).ToList(),
                "lastmodified" => sortDirection == "desc" ? result.OrderByDescending(i => i.LastModified).ToList() : result.OrderBy(i => i.LastModified).ToList(),
                _ => sortDirection == "desc" ? result.OrderByDescending(i => i.Name).ToList() : result.OrderBy(i => i.Name).ToList()
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting request items for tenant {TenantId}", tenantId);
            return StatusCode(500, new { error = "An error occurred while retrieving request items" });
        }
    }

    // GET: api/admin/request-items/{tenantId}/{itemId}/rules
    [HttpGet("request-items/{tenantId}/{itemId}/rules")]
    public async Task<ActionResult<List<RequestItemRule>>> GetRequestItemRules(int tenantId, int itemId)
    {
        try
        {
            var rules = await _context.RequestItemRules
                .Where(r => r.TenantId == tenantId && r.RequestItemId == itemId)
                .OrderByDescending(r => r.Priority)
                .ThenBy(r => r.CreatedAt)
                .ToListAsync();

            return Ok(rules);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting rules for request item {ItemId}", itemId);
            return StatusCode(500, new { error = "An error occurred while retrieving request item rules" });
        }
    }

    // POST: api/admin/request-items/{tenantId}/{itemId}/rules
    [HttpPost("request-items/{tenantId}/{itemId}/rules")]
    public async Task<ActionResult<RequestItemRule>> CreateRequestItemRule(
        int tenantId,
        int itemId,
        [FromBody] CreateRequestItemRuleRequest request)
    {
        try
        {
            // Verify item exists
            var itemExists = await _context.RequestItems
                .AnyAsync(i => i.TenantId == tenantId && i.Id == itemId);

            if (!itemExists)
            {
                return NotFound(new { error = "Request item not found" });
            }

            var rule = new RequestItemRule
            {
                TenantId = tenantId,
                RequestItemId = itemId,
                RuleType = request.RuleType,
                RuleKey = request.RuleKey,
                RuleValue = request.RuleValue,
                ValidationMessage = request.ValidationMessage,
                MaxPerRoom = request.MaxPerRoom,
                MaxPerGuest = request.MaxPerGuest,
                RequiresActiveBooking = request.RequiresActiveBooking,
                RestrictedHours = request.RestrictedHours,
                Priority = request.Priority,
                IsActive = request.IsActive,
                MinConfidenceScore = request.MinConfidenceScore,
                Notes = request.Notes,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.RequestItemRules.Add(rule);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created request item rule {RuleId} for item {ItemId}", rule.Id, itemId);

            return CreatedAtAction(nameof(GetRequestItemRules), new { tenantId, itemId }, rule);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating request item rule for item {ItemId}", itemId);
            return StatusCode(500, new { error = "An error occurred while creating the request item rule" });
        }
    }

    // PUT: api/admin/request-items/{tenantId}/{itemId}/rules/{ruleId}
    [HttpPut("request-items/{tenantId}/{itemId}/rules/{ruleId}")]
    public async Task<ActionResult<RequestItemRule>> UpdateRequestItemRule(
        int tenantId,
        int itemId,
        int ruleId,
        [FromBody] UpdateRequestItemRuleRequest request)
    {
        try
        {
            var rule = await _context.RequestItemRules
                .Where(r => r.TenantId == tenantId && r.RequestItemId == itemId && r.Id == ruleId)
                .FirstOrDefaultAsync();

            if (rule == null)
            {
                return NotFound(new { error = "Rule not found" });
            }

            rule.RuleType = request.RuleType;
            rule.RuleKey = request.RuleKey;
            rule.RuleValue = request.RuleValue;
            rule.ValidationMessage = request.ValidationMessage;
            rule.MaxPerRoom = request.MaxPerRoom;
            rule.MaxPerGuest = request.MaxPerGuest;
            rule.RequiresActiveBooking = request.RequiresActiveBooking;
            rule.RestrictedHours = request.RestrictedHours;
            rule.Priority = request.Priority;
            rule.IsActive = request.IsActive;
            rule.MinConfidenceScore = request.MinConfidenceScore;
            rule.Notes = request.Notes;
            rule.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Updated request item rule {RuleId}", ruleId);

            return Ok(rule);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating request item rule {RuleId}", ruleId);
            return StatusCode(500, new { error = "An error occurred while updating the request item rule" });
        }
    }

    // DELETE: api/admin/request-items/{tenantId}/{itemId}/rules/{ruleId}
    [HttpDelete("request-items/{tenantId}/{itemId}/rules/{ruleId}")]
    public async Task<ActionResult> DeleteRequestItemRule(int tenantId, int itemId, int ruleId)
    {
        try
        {
            var rule = await _context.RequestItemRules
                .Where(r => r.TenantId == tenantId && r.RequestItemId == itemId && r.Id == ruleId)
                .FirstOrDefaultAsync();

            if (rule == null)
            {
                return NotFound(new { error = "Rule not found" });
            }

            _context.RequestItemRules.Remove(rule);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Deleted request item rule {RuleId}", ruleId);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting request item rule {RuleId}", ruleId);
            return StatusCode(500, new { error = "An error occurred while deleting the request item rule" });
        }
    }

    #endregion

    #region Upsell Items

    // GET: api/admin/upsell-items/{tenantId}
    [HttpGet("upsell-items/{tenantId}")]
    public async Task<ActionResult<List<UpsellItem>>> GetUpsellItems(int tenantId)
    {
        try
        {
            var items = await _context.UpsellItems
                .IgnoreQueryFilters()
                .Where(u => u.TenantId == tenantId)
                .OrderBy(u => u.Title)
                .ToListAsync();

            return Ok(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting upsell items for tenant {TenantId}", tenantId);
            return StatusCode(500, new { error = "An error occurred while retrieving upsell items" });
        }
    }

    // POST: api/admin/upsell-items/{tenantId}
    [HttpPost("upsell-items/{tenantId}")]
    public async Task<ActionResult<UpsellItem>> CreateUpsellItem(
        int tenantId,
        [FromBody] CreateUpsellItemRequest request)
    {
        try
        {
            var item = new UpsellItem
            {
                TenantId = tenantId,
                Title = request.Title,
                Description = request.Description ?? string.Empty,
                PriceCents = request.PriceCents,
                Unit = request.Unit ?? "item",
                Categories = request.Categories?.ToArray() ?? Array.Empty<string>(),
                IsActive = request.IsActive,
                LeadTimeMinutes = request.LeadTimeMinutes ?? 60,
                UpdatedAt = DateTime.UtcNow
            };

            _context.UpsellItems.Add(item);
            await _context.SaveChangesAsync();

            // Create audit log
            await CreateAuditLog(tenantId, "CREATE", "UpsellItem", item.Id, null, new
            {
                item.Title,
                item.PriceCents,
                item.Unit,
                item.Categories,
                item.IsActive,
                item.LeadTimeMinutes
            });

            _logger.LogInformation("Created upsell item {ItemId}", item.Id);

            return CreatedAtAction(nameof(GetUpsellItems), new { tenantId }, item);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating upsell item");
            return StatusCode(500, new { error = "An error occurred while creating the upsell item" });
        }
    }

    // PUT: api/admin/upsell-items/{tenantId}/{itemId}
    [HttpPut("upsell-items/{tenantId}/{itemId}")]
    public async Task<ActionResult<UpsellItem>> UpdateUpsellItem(
        int tenantId,
        int itemId,
        [FromBody] UpdateUpsellItemRequest request)
    {
        try
        {
            var item = await _context.UpsellItems
                .IgnoreQueryFilters()
                .Where(u => u.TenantId == tenantId && u.Id == itemId)
                .FirstOrDefaultAsync();

            if (item == null)
            {
                return NotFound(new { error = "Upsell item not found" });
            }

            // Capture before state for audit log
            var beforeState = new
            {
                item.Title,
                item.PriceCents,
                item.Unit,
                Categories = item.Categories.ToArray(),
                item.IsActive,
                item.LeadTimeMinutes
            };

            item.Title = request.Title;
            item.Description = request.Description ?? string.Empty;
            item.PriceCents = request.PriceCents;
            item.Unit = request.Unit ?? "item";
            item.Categories = request.Categories?.ToArray() ?? Array.Empty<string>();
            item.IsActive = request.IsActive;
            item.LeadTimeMinutes = request.LeadTimeMinutes ?? 60;
            item.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // Create audit log
            await CreateAuditLog(tenantId, "UPDATE", "UpsellItem", itemId, beforeState, new
            {
                item.Title,
                item.PriceCents,
                item.Unit,
                item.Categories,
                item.IsActive,
                item.LeadTimeMinutes
            });

            _logger.LogInformation("Updated upsell item {ItemId}", itemId);

            return Ok(item);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating upsell item {ItemId}", itemId);
            return StatusCode(500, new { error = "An error occurred while updating the upsell item" });
        }
    }

    // DELETE: api/admin/upsell-items/{tenantId}/{itemId}
    [HttpDelete("upsell-items/{tenantId}/{itemId}")]
    public async Task<ActionResult> DeleteUpsellItem(int tenantId, int itemId)
    {
        try
        {
            var item = await _context.UpsellItems
                .IgnoreQueryFilters()
                .Where(u => u.TenantId == tenantId && u.Id == itemId)
                .FirstOrDefaultAsync();

            if (item == null)
            {
                return NotFound(new { error = "Upsell item not found" });
            }

            // Capture before state for audit log
            var beforeState = new
            {
                item.Title,
                item.PriceCents,
                item.Unit,
                Categories = item.Categories.ToArray(),
                item.IsActive,
                item.LeadTimeMinutes
            };

            _context.UpsellItems.Remove(item);
            await _context.SaveChangesAsync();

            // Create audit log
            await CreateAuditLog(tenantId, "DELETE", "UpsellItem", itemId, beforeState, null);

            _logger.LogInformation("Deleted upsell item {ItemId}", itemId);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting upsell item {ItemId}", itemId);
            return StatusCode(500, new { error = "An error occurred while deleting the upsell item" });
        }
    }

    // GET: api/admin/upsell-analytics/{tenantId}
    [HttpGet("upsell-analytics/{tenantId}")]
    public async Task<ActionResult<UpsellAnalyticsDTO>> GetUpsellAnalytics(
        int tenantId,
        [FromQuery] int days = 30)
    {
        try
        {
            // TODO: Implement analytics calculation from conversation messages and upsell tracking
            // For now, return mock data
            var analytics = new UpsellAnalyticsDTO
            {
                TotalRevenue = 0,
                ConversionRate = 0,
                TotalSuggestions = 0,
                TotalAccepted = 0,
                TopPerformers = new List<UpsellPerformerDTO>()
            };

            return Ok(analytics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting upsell analytics for tenant {TenantId}", tenantId);
            return StatusCode(500, new { error = "An error occurred while retrieving upsell analytics" });
        }
    }

    #endregion

    #region Audit Log & Stats

    // GET: api/admin/audit-log/{tenantId}
    [HttpGet("audit-log/{tenantId}")]
    public async Task<ActionResult<List<AuditLogEntryDTO>>> GetAuditLog(
        int tenantId,
        [FromQuery] string? searchTerm = null,
        [FromQuery] int? userId = null,
        [FromQuery] string? action = null,
        [FromQuery] string? entityType = null,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        [FromQuery] string? sortBy = "timestamp",
        [FromQuery] string? sortDirection = "desc")
    {
        try
        {
            var query = _context.AuditLogs
                .Where(a => a.TenantId == tenantId)
                .AsQueryable();

            // Apply filters
            if (!string.IsNullOrEmpty(searchTerm))
            {
                var term = searchTerm.ToLower();
                query = query.Where(a => a.Entity.ToLower().Contains(term) ||
                                        a.Action.ToLower().Contains(term));
            }

            if (userId.HasValue)
            {
                query = query.Where(a => a.ActorUserId == userId.Value);
            }

            if (!string.IsNullOrEmpty(action))
            {
                query = query.Where(a => a.Action == action);
            }

            if (!string.IsNullOrEmpty(entityType))
            {
                query = query.Where(a => a.Entity == entityType);
            }

            if (dateFrom.HasValue)
            {
                query = query.Where(a => a.CreatedAt >= dateFrom.Value);
            }

            if (dateTo.HasValue)
            {
                query = query.Where(a => a.CreatedAt <= dateTo.Value);
            }

            // Apply sorting
            query = sortBy?.ToLower() switch
            {
                "action" => sortDirection == "desc" ? query.OrderByDescending(a => a.Action) : query.OrderBy(a => a.Action),
                "entity" => sortDirection == "desc" ? query.OrderByDescending(a => a.Entity) : query.OrderBy(a => a.Entity),
                _ => sortDirection == "desc" ? query.OrderByDescending(a => a.CreatedAt) : query.OrderBy(a => a.CreatedAt)
            };

            // Get audit logs with user information (first load raw data)
            var rawAuditLogs = await query
                .Select(a => new
                {
                    a.Id,
                    a.TenantId,
                    a.ActorUserId,
                    UserName = a.ActorUser != null ? a.ActorUser.UserName : null,
                    UserEmail = a.ActorUser != null ? a.ActorUser.Email : null,
                    a.Action,
                    Entity = a.Entity,
                    a.EntityId,
                    a.DiffJson,
                    a.CreatedAt
                })
                .Take(100) // Limit to 100 most recent entries
                .ToListAsync();

            // Parse JSON in memory (not in SQL query)
            var auditLogs = rawAuditLogs.Select(a => new AuditLogEntryDTO
            {
                Id = a.Id,
                TenantId = a.TenantId,
                UserId = a.ActorUserId ?? 0,
                UserName = a.UserName ?? "System",
                UserEmail = a.UserEmail ?? "system@hostr.com",
                Action = a.Action,
                EntityType = a.Entity,
                EntityId = a.EntityId ?? 0,
                EntityName = null,
                ChangesBefore = ParseJsonProperty(a.DiffJson, "before"),
                ChangesAfter = ParseJsonProperty(a.DiffJson, "after"),
                IpAddress = null,
                UserAgent = null,
                Timestamp = a.CreatedAt
            }).ToList();

            return Ok(auditLogs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting audit log for tenant {TenantId}", tenantId);
            return StatusCode(500, new { error = "An error occurred while retrieving the audit log" });
        }
    }

    // GET: api/admin/business-rules/stats/{tenantId}
    [HttpGet("business-rules/stats/{tenantId}")]
    public async Task<ActionResult<BusinessRulesStatsDTO>> GetBusinessRulesStats(int tenantId)
    {
        try
        {
            var totalServiceRules = await _context.ServiceBusinessRules
                .Where(r => r.TenantId == tenantId)
                .CountAsync();

            var activeServiceRules = await _context.ServiceBusinessRules
                .Where(r => r.TenantId == tenantId && r.IsActive)
                .CountAsync();

            var totalRequestItemRules = await _context.RequestItemRules
                .Where(r => r.TenantId == tenantId)
                .CountAsync();

            var activeRequestItemRules = await _context.RequestItemRules
                .Where(r => r.TenantId == tenantId && r.IsActive)
                .CountAsync();

            var totalServices = await _context.Services
                .Where(s => s.TenantId == tenantId)
                .CountAsync();

            var servicesWithRules = await _context.Services
                .Where(s => s.TenantId == tenantId &&
                           _context.ServiceBusinessRules.Any(r => r.ServiceId == s.Id))
                .CountAsync();

            var totalRequestItems = await _context.RequestItems
                .Where(i => i.TenantId == tenantId)
                .CountAsync();

            var requestItemsWithRules = await _context.RequestItems
                .Where(i => i.TenantId == tenantId &&
                           _context.RequestItemRules.Any(r => r.RequestItemId == i.Id))
                .CountAsync();

            var totalUpsellItems = await _context.UpsellItems
                .Where(u => u.TenantId == tenantId)
                .CountAsync();

            var activeUpsellItems = await _context.UpsellItems
                .Where(u => u.TenantId == tenantId && u.IsActive)
                .CountAsync();

            var stats = new BusinessRulesStatsDTO
            {
                TotalRules = totalServiceRules + totalRequestItemRules,
                ActiveRules = activeServiceRules + activeRequestItemRules,
                DraftRules = 0, // TODO: Implement draft status
                InactiveRules = (totalServiceRules - activeServiceRules) + (totalRequestItemRules - activeRequestItemRules),
                TotalServices = totalServices,
                ServicesWithRules = servicesWithRules,
                TotalRequestItems = totalRequestItems,
                RequestItemsWithRules = requestItemsWithRules,
                TotalUpsellItems = totalUpsellItems,
                ActiveUpsellItems = activeUpsellItems
            };

            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting business rules stats for tenant {TenantId}", tenantId);
            return StatusCode(500, new { error = "An error occurred while retrieving business rules statistics" });
        }
    }

    #endregion
}

// Helper DTOs
public class ToggleRequest
{
    public bool IsActive { get; set; }
}
