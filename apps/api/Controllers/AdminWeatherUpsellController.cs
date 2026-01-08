using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Hostr.Api.Data;
using Hostr.Api.Models;
using System.ComponentModel.DataAnnotations;

namespace Hostr.Api.Controllers;

[ApiController]
[Route("api/admin/weather-upsells")]
public class AdminWeatherUpsellController : ControllerBase
{
    private readonly HostrDbContext _context;
    private readonly ILogger<AdminWeatherUpsellController> _logger;

    public AdminWeatherUpsellController(HostrDbContext context, ILogger<AdminWeatherUpsellController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Get all weather upsell rules for a tenant
    /// </summary>
    [HttpGet("{tenantId}")]
    public async Task<ActionResult<List<WeatherUpsellRuleDTO>>> GetWeatherUpsellRules(int tenantId)
    {
        try
        {
            var rules = await _context.WeatherUpsellRules
                .IgnoreQueryFilters()
                .Where(r => r.TenantId == tenantId)
                .OrderByDescending(r => r.Priority)
                .ThenBy(r => r.WeatherCondition)
                .Select(r => new WeatherUpsellRuleDTO
                {
                    Id = r.Id,
                    TenantId = r.TenantId,
                    WeatherCondition = r.WeatherCondition,
                    MinTemperature = r.MinTemperature,
                    MaxTemperature = r.MaxTemperature,
                    WeatherCodes = r.WeatherCodes,
                    ServiceIds = r.ServiceIds,
                    BannerText = r.BannerText,
                    BannerIcon = r.BannerIcon,
                    Priority = r.Priority,
                    IsActive = r.IsActive,
                    CreatedAt = r.CreatedAt,
                    UpdatedAt = r.UpdatedAt
                })
                .ToListAsync();

            return Ok(rules);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting weather upsell rules for tenant {TenantId}", tenantId);
            return StatusCode(500, new { error = "An error occurred while retrieving weather upsell rules" });
        }
    }

    /// <summary>
    /// Get a specific weather upsell rule
    /// </summary>
    [HttpGet("{tenantId}/{ruleId}")]
    public async Task<ActionResult<WeatherUpsellRuleDTO>> GetWeatherUpsellRule(int tenantId, int ruleId)
    {
        try
        {
            var rule = await _context.WeatherUpsellRules
                .IgnoreQueryFilters()
                .Where(r => r.TenantId == tenantId && r.Id == ruleId)
                .Select(r => new WeatherUpsellRuleDTO
                {
                    Id = r.Id,
                    TenantId = r.TenantId,
                    WeatherCondition = r.WeatherCondition,
                    MinTemperature = r.MinTemperature,
                    MaxTemperature = r.MaxTemperature,
                    WeatherCodes = r.WeatherCodes,
                    ServiceIds = r.ServiceIds,
                    BannerText = r.BannerText,
                    BannerIcon = r.BannerIcon,
                    Priority = r.Priority,
                    IsActive = r.IsActive,
                    CreatedAt = r.CreatedAt,
                    UpdatedAt = r.UpdatedAt
                })
                .FirstOrDefaultAsync();

            if (rule == null)
            {
                return NotFound(new { error = "Weather upsell rule not found" });
            }

            return Ok(rule);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting weather upsell rule {RuleId} for tenant {TenantId}", ruleId, tenantId);
            return StatusCode(500, new { error = "An error occurred while retrieving the weather upsell rule" });
        }
    }

    /// <summary>
    /// Create a new weather upsell rule
    /// </summary>
    [HttpPost("{tenantId}")]
    public async Task<ActionResult<WeatherUpsellRuleDTO>> CreateWeatherUpsellRule(
        int tenantId,
        [FromBody] CreateWeatherUpsellRuleRequest request)
    {
        try
        {
            // Validate weather condition
            if (!WeatherConditionTypes.All.Contains(request.WeatherCondition))
            {
                return BadRequest(new { error = $"Invalid weather condition. Must be one of: {string.Join(", ", WeatherConditionTypes.All)}" });
            }

            // Validate service IDs JSON
            if (!string.IsNullOrEmpty(request.ServiceIds))
            {
                try
                {
                    System.Text.Json.JsonSerializer.Deserialize<int[]>(request.ServiceIds);
                }
                catch
                {
                    return BadRequest(new { error = "ServiceIds must be a valid JSON array of integers" });
                }
            }

            var rule = new WeatherUpsellRule
            {
                TenantId = tenantId,
                WeatherCondition = request.WeatherCondition,
                MinTemperature = request.MinTemperature,
                MaxTemperature = request.MaxTemperature,
                WeatherCodes = request.WeatherCodes,
                ServiceIds = request.ServiceIds ?? "[]",
                BannerText = request.BannerText,
                BannerIcon = request.BannerIcon,
                Priority = request.Priority,
                IsActive = request.IsActive,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.WeatherUpsellRules.Add(rule);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created weather upsell rule {RuleId} for tenant {TenantId}", rule.Id, tenantId);

            var dto = new WeatherUpsellRuleDTO
            {
                Id = rule.Id,
                TenantId = rule.TenantId,
                WeatherCondition = rule.WeatherCondition,
                MinTemperature = rule.MinTemperature,
                MaxTemperature = rule.MaxTemperature,
                WeatherCodes = rule.WeatherCodes,
                ServiceIds = rule.ServiceIds,
                BannerText = rule.BannerText,
                BannerIcon = rule.BannerIcon,
                Priority = rule.Priority,
                IsActive = rule.IsActive,
                CreatedAt = rule.CreatedAt,
                UpdatedAt = rule.UpdatedAt
            };

            return CreatedAtAction(nameof(GetWeatherUpsellRule), new { tenantId, ruleId = rule.Id }, dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating weather upsell rule for tenant {TenantId}", tenantId);
            return StatusCode(500, new { error = "An error occurred while creating the weather upsell rule" });
        }
    }

    /// <summary>
    /// Update an existing weather upsell rule
    /// </summary>
    [HttpPut("{tenantId}/{ruleId}")]
    public async Task<ActionResult<WeatherUpsellRuleDTO>> UpdateWeatherUpsellRule(
        int tenantId,
        int ruleId,
        [FromBody] UpdateWeatherUpsellRuleRequest request)
    {
        try
        {
            var rule = await _context.WeatherUpsellRules
                .IgnoreQueryFilters()
                .Where(r => r.TenantId == tenantId && r.Id == ruleId)
                .FirstOrDefaultAsync();

            if (rule == null)
            {
                return NotFound(new { error = "Weather upsell rule not found" });
            }

            // Validate weather condition
            if (!WeatherConditionTypes.All.Contains(request.WeatherCondition))
            {
                return BadRequest(new { error = $"Invalid weather condition. Must be one of: {string.Join(", ", WeatherConditionTypes.All)}" });
            }

            // Validate service IDs JSON
            if (!string.IsNullOrEmpty(request.ServiceIds))
            {
                try
                {
                    System.Text.Json.JsonSerializer.Deserialize<int[]>(request.ServiceIds);
                }
                catch
                {
                    return BadRequest(new { error = "ServiceIds must be a valid JSON array of integers" });
                }
            }

            rule.WeatherCondition = request.WeatherCondition;
            rule.MinTemperature = request.MinTemperature;
            rule.MaxTemperature = request.MaxTemperature;
            rule.WeatherCodes = request.WeatherCodes;
            rule.ServiceIds = request.ServiceIds ?? "[]";
            rule.BannerText = request.BannerText;
            rule.BannerIcon = request.BannerIcon;
            rule.Priority = request.Priority;
            rule.IsActive = request.IsActive;
            rule.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Updated weather upsell rule {RuleId}", ruleId);

            var dto = new WeatherUpsellRuleDTO
            {
                Id = rule.Id,
                TenantId = rule.TenantId,
                WeatherCondition = rule.WeatherCondition,
                MinTemperature = rule.MinTemperature,
                MaxTemperature = rule.MaxTemperature,
                WeatherCodes = rule.WeatherCodes,
                ServiceIds = rule.ServiceIds,
                BannerText = rule.BannerText,
                BannerIcon = rule.BannerIcon,
                Priority = rule.Priority,
                IsActive = rule.IsActive,
                CreatedAt = rule.CreatedAt,
                UpdatedAt = rule.UpdatedAt
            };

            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating weather upsell rule {RuleId}", ruleId);
            return StatusCode(500, new { error = "An error occurred while updating the weather upsell rule" });
        }
    }

    /// <summary>
    /// Delete a weather upsell rule
    /// </summary>
    [HttpDelete("{tenantId}/{ruleId}")]
    public async Task<ActionResult> DeleteWeatherUpsellRule(int tenantId, int ruleId)
    {
        try
        {
            var rule = await _context.WeatherUpsellRules
                .IgnoreQueryFilters()
                .Where(r => r.TenantId == tenantId && r.Id == ruleId)
                .FirstOrDefaultAsync();

            if (rule == null)
            {
                return NotFound(new { error = "Weather upsell rule not found" });
            }

            _context.WeatherUpsellRules.Remove(rule);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Deleted weather upsell rule {RuleId}", ruleId);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting weather upsell rule {RuleId}", ruleId);
            return StatusCode(500, new { error = "An error occurred while deleting the weather upsell rule" });
        }
    }

    /// <summary>
    /// Toggle the active status of a weather upsell rule
    /// </summary>
    [HttpPatch("{tenantId}/{ruleId}/toggle")]
    public async Task<ActionResult> ToggleWeatherUpsellRule(int tenantId, int ruleId, [FromBody] ToggleWeatherUpsellRequest request)
    {
        try
        {
            var rule = await _context.WeatherUpsellRules
                .IgnoreQueryFilters()
                .Where(r => r.TenantId == tenantId && r.Id == ruleId)
                .FirstOrDefaultAsync();

            if (rule == null)
            {
                return NotFound(new { error = "Weather upsell rule not found" });
            }

            rule.IsActive = request.IsActive;
            rule.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Toggled weather upsell rule {RuleId} to {IsActive}", ruleId, request.IsActive);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling weather upsell rule {RuleId}", ruleId);
            return StatusCode(500, new { error = "An error occurred while toggling the weather upsell rule" });
        }
    }

    /// <summary>
    /// Update priority order for multiple rules
    /// </summary>
    [HttpPatch("{tenantId}/reorder")]
    public async Task<ActionResult> ReorderWeatherUpsellRules(int tenantId, [FromBody] ReorderWeatherUpsellRequest request)
    {
        try
        {
            var rules = await _context.WeatherUpsellRules
                .IgnoreQueryFilters()
                .Where(r => r.TenantId == tenantId && request.RuleIds.Contains(r.Id))
                .ToListAsync();

            // Update priorities based on order in request
            for (int i = 0; i < request.RuleIds.Length; i++)
            {
                var rule = rules.FirstOrDefault(r => r.Id == request.RuleIds[i]);
                if (rule != null)
                {
                    rule.Priority = request.RuleIds.Length - i; // Higher index = lower priority
                    rule.UpdatedAt = DateTime.UtcNow;
                }
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Reordered {Count} weather upsell rules for tenant {TenantId}", request.RuleIds.Length, tenantId);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reordering weather upsell rules for tenant {TenantId}", tenantId);
            return StatusCode(500, new { error = "An error occurred while reordering weather upsell rules" });
        }
    }

    /// <summary>
    /// Get available weather condition types
    /// </summary>
    [HttpGet("conditions")]
    public ActionResult<WeatherConditionInfo[]> GetWeatherConditions()
    {
        var conditions = new[]
        {
            new WeatherConditionInfo { Code = WeatherConditionTypes.Hot, Name = "Hot", Description = "Temperature above 28°C with clear skies", DefaultMinTemp = 28, DefaultMaxTemp = null, DefaultIcon = "sun" },
            new WeatherConditionInfo { Code = WeatherConditionTypes.Warm, Name = "Warm", Description = "Temperature between 20-28°C", DefaultMinTemp = 20, DefaultMaxTemp = 28, DefaultIcon = "sun" },
            new WeatherConditionInfo { Code = WeatherConditionTypes.Mild, Name = "Mild", Description = "Temperature between 15-20°C", DefaultMinTemp = 15, DefaultMaxTemp = 20, DefaultIcon = "cloud-sun" },
            new WeatherConditionInfo { Code = WeatherConditionTypes.Cold, Name = "Cold", Description = "Temperature below 15°C", DefaultMinTemp = null, DefaultMaxTemp = 15, DefaultIcon = "cloud" },
            new WeatherConditionInfo { Code = WeatherConditionTypes.Rainy, Name = "Rainy", Description = "Rain or drizzle conditions", DefaultMinTemp = null, DefaultMaxTemp = null, DefaultIcon = "cloud-rain", WmoCodes = WeatherConditionTypes.RainCodes },
            new WeatherConditionInfo { Code = WeatherConditionTypes.Stormy, Name = "Stormy", Description = "Thunderstorm conditions", DefaultMinTemp = null, DefaultMaxTemp = null, DefaultIcon = "cloud-lightning-rain", WmoCodes = WeatherConditionTypes.StormCodes },
            new WeatherConditionInfo { Code = WeatherConditionTypes.Cloudy, Name = "Cloudy", Description = "Overcast or foggy conditions", DefaultMinTemp = null, DefaultMaxTemp = null, DefaultIcon = "cloud", WmoCodes = WeatherConditionTypes.CloudyCodes }
        };

        return Ok(conditions);
    }

    /// <summary>
    /// Get services available for upselling
    /// </summary>
    [HttpGet("{tenantId}/available-services")]
    public async Task<ActionResult> GetAvailableServices(int tenantId)
    {
        try
        {
            var services = await _context.Services
                .IgnoreQueryFilters()
                .Where(s => s.TenantId == tenantId && s.IsAvailable)
                .OrderBy(s => s.Category)
                .ThenBy(s => s.Name)
                .Select(s => new
                {
                    s.Id,
                    s.Name,
                    s.Description,
                    s.Category,
                    s.Icon,
                    imageUrl = s.FeaturedImageUrl ?? s.ImageUrl,
                    s.IsChargeable,
                    price = s.IsChargeable ? $"{s.Currency} {s.Price:F2}" : "Complimentary",
                    priceAmount = s.Price,
                    s.Currency
                })
                .ToListAsync();

            return Ok(new { services });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available services for tenant {TenantId}", tenantId);
            return StatusCode(500, new { error = "An error occurred while retrieving available services" });
        }
    }
}

#region DTOs

public class WeatherUpsellRuleDTO
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public string WeatherCondition { get; set; } = string.Empty;
    public int? MinTemperature { get; set; }
    public int? MaxTemperature { get; set; }
    public string? WeatherCodes { get; set; }
    public string ServiceIds { get; set; } = "[]";
    public string? BannerText { get; set; }
    public string? BannerIcon { get; set; }
    public int Priority { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreateWeatherUpsellRuleRequest
{
    [Required]
    [MaxLength(50)]
    public string WeatherCondition { get; set; } = string.Empty;

    public int? MinTemperature { get; set; }
    public int? MaxTemperature { get; set; }
    public string? WeatherCodes { get; set; }

    [Required]
    public string ServiceIds { get; set; } = "[]";

    [MaxLength(200)]
    public string? BannerText { get; set; }

    [MaxLength(50)]
    public string? BannerIcon { get; set; }

    public int Priority { get; set; } = 0;
    public bool IsActive { get; set; } = true;
}

public class UpdateWeatherUpsellRuleRequest
{
    [Required]
    [MaxLength(50)]
    public string WeatherCondition { get; set; } = string.Empty;

    public int? MinTemperature { get; set; }
    public int? MaxTemperature { get; set; }
    public string? WeatherCodes { get; set; }

    [Required]
    public string ServiceIds { get; set; } = "[]";

    [MaxLength(200)]
    public string? BannerText { get; set; }

    [MaxLength(50)]
    public string? BannerIcon { get; set; }

    public int Priority { get; set; } = 0;
    public bool IsActive { get; set; } = true;
}

public class ToggleWeatherUpsellRequest
{
    public bool IsActive { get; set; }
}

public class ReorderWeatherUpsellRequest
{
    [Required]
    public int[] RuleIds { get; set; } = Array.Empty<int>();
}

public class WeatherConditionInfo
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int? DefaultMinTemp { get; set; }
    public int? DefaultMaxTemp { get; set; }
    public string DefaultIcon { get; set; } = string.Empty;
    public int[]? WmoCodes { get; set; }
}

#endregion
