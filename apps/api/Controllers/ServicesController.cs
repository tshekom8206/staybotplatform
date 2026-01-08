using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Hostr.Api.Data;
using Hostr.Api.Models;
using Hostr.Api.Middleware;

namespace Hostr.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ServicesController : ControllerBase
{
    private readonly HostrDbContext _context;

    public ServicesController(HostrDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Get all services for the current tenant
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetServices()
    {
        var tenantId = int.Parse(HttpContext.Items["TenantId"]?.ToString() ?? "0");
        if (tenantId == 0)
        {
            return BadRequest("Tenant context is required");
        }

        var services = await _context.Services
            .Where(s => s.TenantId == tenantId)
            .OrderBy(s => s.Priority)
            .ThenBy(s => s.Name)
            .Select(s => new
            {
                s.Id,
                s.Name,
                s.Description,
                s.Category,
                s.Icon,
                s.IsAvailable,
                s.IsChargeable,
                s.Price,
                s.Currency,
                s.PricingUnit,
                s.AvailableHours,
                s.ContactMethod,
                s.ContactInfo,
                s.Priority,
                s.SpecialInstructions,
                s.ImageUrl,
                s.RequiresAdvanceBooking,
                s.AdvanceBookingHours,
                // Featured/Upselling fields
                s.IsFeatured,
                s.FeaturedImageUrl,
                s.TimeSlots,
                s.DisplayOrder,
                s.CreatedAt,
                s.UpdatedAt
            })
            .ToListAsync();

        return Ok(new { services });
    }

    /// <summary>
    /// Get a specific service by ID
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetService(int id)
    {
        var tenantId = int.Parse(HttpContext.Items["TenantId"]?.ToString() ?? "0");
        if (tenantId == 0)
        {
            return BadRequest("Tenant context is required");
        }

        var service = await _context.Services
            .Where(s => s.Id == id && s.TenantId == tenantId)
            .Select(s => new
            {
                s.Id,
                s.Name,
                s.Description,
                s.Category,
                s.Icon,
                s.IsAvailable,
                s.IsChargeable,
                s.Price,
                s.Currency,
                s.PricingUnit,
                s.AvailableHours,
                s.ContactMethod,
                s.ContactInfo,
                s.Priority,
                s.SpecialInstructions,
                s.ImageUrl,
                s.RequiresAdvanceBooking,
                s.AdvanceBookingHours,
                // Featured/Upselling fields
                s.IsFeatured,
                s.FeaturedImageUrl,
                s.TimeSlots,
                s.DisplayOrder,
                s.CreatedAt,
                s.UpdatedAt
            })
            .FirstOrDefaultAsync();

        if (service == null)
        {
            return NotFound("Service not found");
        }

        return Ok(new { service });
    }

    /// <summary>
    /// Create a new service
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateService([FromBody] CreateServiceRequest request)
    {
        var tenantId = int.Parse(HttpContext.Items["TenantId"]?.ToString() ?? "0");
        if (tenantId == 0)
        {
            return BadRequest("Tenant context is required");
        }

        var service = new Service
        {
            TenantId = tenantId,
            Name = request.Name,
            Description = request.Description,
            Category = request.Category,
            Icon = request.Icon,
            IsAvailable = request.IsAvailable,
            IsChargeable = request.IsChargeable,
            Price = request.Price,
            Currency = request.Currency,
            PricingUnit = request.PricingUnit,
            AvailableHours = request.AvailableHours,
            ContactMethod = request.ContactMethod,
            ContactInfo = request.ContactInfo,
            Priority = request.Priority,
            SpecialInstructions = request.SpecialInstructions,
            ImageUrl = request.ImageUrl,
            RequiresAdvanceBooking = request.RequiresAdvanceBooking,
            AdvanceBookingHours = request.AdvanceBookingHours,
            // Featured/Upselling fields
            IsFeatured = request.IsFeatured,
            FeaturedImageUrl = request.FeaturedImageUrl,
            TimeSlots = request.TimeSlots,
            DisplayOrder = request.DisplayOrder,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Services.Add(service);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetService), new { id = service.Id }, new { service });
    }

    /// <summary>
    /// Update an existing service
    /// </summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateService(int id, [FromBody] UpdateServiceRequest request)
    {
        var tenantId = int.Parse(HttpContext.Items["TenantId"]?.ToString() ?? "0");
        if (tenantId == 0)
        {
            return BadRequest("Tenant context is required");
        }

        var service = await _context.Services
            .FirstOrDefaultAsync(s => s.Id == id && s.TenantId == tenantId);

        if (service == null)
        {
            return NotFound("Service not found");
        }

        service.Name = request.Name;
        service.Description = request.Description;
        service.Category = request.Category;
        service.Icon = request.Icon;
        service.IsAvailable = request.IsAvailable;
        service.IsChargeable = request.IsChargeable;
        service.Price = request.Price;
        service.Currency = request.Currency;
        service.PricingUnit = request.PricingUnit;
        service.AvailableHours = request.AvailableHours;
        service.ContactMethod = request.ContactMethod;
        service.ContactInfo = request.ContactInfo;
        service.Priority = request.Priority;
        service.SpecialInstructions = request.SpecialInstructions;
        service.ImageUrl = request.ImageUrl;
        service.RequiresAdvanceBooking = request.RequiresAdvanceBooking;
        service.AdvanceBookingHours = request.AdvanceBookingHours;
        // Featured/Upselling fields
        service.IsFeatured = request.IsFeatured;
        service.FeaturedImageUrl = request.FeaturedImageUrl;
        service.TimeSlots = request.TimeSlots;
        service.DisplayOrder = request.DisplayOrder;
        service.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new { service });
    }

    /// <summary>
    /// Delete a service
    /// </summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteService(int id)
    {
        var tenantId = int.Parse(HttpContext.Items["TenantId"]?.ToString() ?? "0");
        if (tenantId == 0)
        {
            return BadRequest("Tenant context is required");
        }

        var service = await _context.Services
            .FirstOrDefaultAsync(s => s.Id == id && s.TenantId == tenantId);

        if (service == null)
        {
            return NotFound("Service not found");
        }

        _context.Services.Remove(service);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>
    /// Get available service categories
    /// </summary>
    [HttpGet("categories")]
    public async Task<IActionResult> GetServiceCategories()
    {
        // Get existing service categories from the concierge services module
        var categories = await _context.ServiceCategories
            .Where(c => c.IsActive)
            .Select(c => new { value = c.Name, label = c.Name, description = c.Description })
            .ToListAsync();

        return Ok(new { categories });
    }

    /// <summary>
    /// Get available service icons
    /// </summary>
    [HttpGet("icons")]
    public async Task<IActionResult> GetServiceIcons()
    {
        var icons = await _context.ServiceIcons
            .Where(i => i.IsActive)
            .OrderBy(i => i.SortOrder)
            .Select(i => new { name = i.Name, icon = i.Icon, label = i.Label })
            .ToListAsync();

        return Ok(new { icons });
    }
}

public class CreateServiceRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Category { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public bool IsAvailable { get; set; } = true;
    public bool IsChargeable { get; set; } = false;
    public decimal? Price { get; set; }
    public string? Currency { get; set; } = "USD";
    public string? PricingUnit { get; set; }
    public string? AvailableHours { get; set; }
    public string? ContactMethod { get; set; }
    public string? ContactInfo { get; set; }
    public int Priority { get; set; } = 0;
    public string? SpecialInstructions { get; set; }
    public string? ImageUrl { get; set; }
    public bool RequiresAdvanceBooking { get; set; } = false;
    public int? AdvanceBookingHours { get; set; }
    // Featured/Upselling fields
    public bool IsFeatured { get; set; } = false;
    public string? FeaturedImageUrl { get; set; }
    public string? TimeSlots { get; set; }
    public int DisplayOrder { get; set; } = 0;
}

public class UpdateServiceRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Category { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public bool IsAvailable { get; set; } = true;
    public bool IsChargeable { get; set; } = false;
    public decimal? Price { get; set; }
    public string? Currency { get; set; } = "USD";
    public string? PricingUnit { get; set; }
    public string? AvailableHours { get; set; }
    public string? ContactMethod { get; set; }
    public string? ContactInfo { get; set; }
    public int Priority { get; set; } = 0;
    public string? SpecialInstructions { get; set; }
    public string? ImageUrl { get; set; }
    public bool RequiresAdvanceBooking { get; set; } = false;
    public int? AdvanceBookingHours { get; set; }
    // Featured/Upselling fields
    public bool IsFeatured { get; set; } = false;
    public string? FeaturedImageUrl { get; set; }
    public string? TimeSlots { get; set; }
    public int DisplayOrder { get; set; } = 0;
}