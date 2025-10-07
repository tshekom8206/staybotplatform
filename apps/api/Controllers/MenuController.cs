using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Hostr.Api.Data;
using Hostr.Api.Models;
using System.ComponentModel.DataAnnotations;

namespace Hostr.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MenuController : ControllerBase
{
    private readonly HostrDbContext _context;

    public MenuController(HostrDbContext context)
    {
        _context = context;
    }

    #region Menu Categories

    /// <summary>
    /// Get all menu categories with their items for the current tenant
    /// </summary>
    [HttpGet("categories")]
    public async Task<IActionResult> GetMenuCategories([FromQuery] string? mealType = null)
    {
        var tenantId = int.Parse(HttpContext.Items["TenantId"]?.ToString() ?? "0");
        if (tenantId == 0)
        {
            return BadRequest("Tenant context is required");
        }

        var baseQuery = _context.MenuCategories
            .Where(c => c.TenantId == tenantId && c.IsActive);

        if (!string.IsNullOrEmpty(mealType))
        {
            baseQuery = baseQuery.Where(c => c.MealType == mealType || c.MealType == "all");
        }

        var query = baseQuery.Include(c => c.MenuItems.Where(i => i.IsAvailable));

        var categories = await query
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.Name)
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.Description,
                c.MealType,
                c.DisplayOrder,
                c.IsActive,
                c.UpdatedAt,
                ItemCount = c.MenuItems.Count,
                MenuItems = c.MenuItems.Select(i => new
                {
                    i.Id,
                    i.Name,
                    i.Description,
                    PriceFormatted = $"{i.Currency} {i.PriceCents / 100.0:F2}",
                    i.PriceCents,
                    i.Currency,
                    i.Allergens,
                    i.MealType,
                    i.IsVegetarian,
                    i.IsVegan,
                    i.IsGlutenFree,
                    i.IsSpicy,
                    i.IsAvailable,
                    i.IsSpecial,
                    i.Tags,
                    i.UpdatedAt
                }).OrderBy(i => i.Name).ToList()
            })
            .ToListAsync();

        return Ok(new { categories });
    }

    /// <summary>
    /// Get a specific menu category with its items
    /// </summary>
    [HttpGet("categories/{id:int}")]
    public async Task<IActionResult> GetMenuCategory(int id)
    {
        var tenantId = int.Parse(HttpContext.Items["TenantId"]?.ToString() ?? "0");
        if (tenantId == 0)
        {
            return BadRequest("Tenant context is required");
        }

        var category = await _context.MenuCategories
            .Where(c => c.Id == id && c.TenantId == tenantId)
            .Include(c => c.MenuItems.Where(i => i.IsAvailable))
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.Description,
                c.MealType,
                c.DisplayOrder,
                c.IsActive,
                c.UpdatedAt,
                ItemCount = c.MenuItems.Count,
                MenuItems = c.MenuItems.Select(i => new
                {
                    i.Id,
                    i.Name,
                    i.Description,
                    PriceFormatted = $"{i.Currency} {i.PriceCents / 100.0:F2}",
                    i.PriceCents,
                    i.Currency,
                    i.Allergens,
                    i.MealType,
                    i.IsVegetarian,
                    i.IsVegan,
                    i.IsGlutenFree,
                    i.IsSpicy,
                    i.IsAvailable,
                    i.IsSpecial,
                    i.Tags,
                    i.UpdatedAt
                }).OrderBy(i => i.Name).ToList()
            })
            .FirstOrDefaultAsync();

        if (category == null)
        {
            return NotFound("Menu category not found");
        }

        return Ok(new { category });
    }

    /// <summary>
    /// Create a new menu category
    /// </summary>
    [HttpPost("categories")]
    public async Task<IActionResult> CreateMenuCategory([FromBody] CreateMenuCategoryRequest request)
    {
        var tenantId = int.Parse(HttpContext.Items["TenantId"]?.ToString() ?? "0");
        if (tenantId == 0)
        {
            return BadRequest("Tenant context is required");
        }

        var category = new MenuCategory
        {
            TenantId = tenantId,
            Name = request.Name,
            Description = request.Description,
            MealType = request.MealType ?? "all",
            DisplayOrder = request.DisplayOrder,
            IsActive = request.IsActive ?? true,
            UpdatedAt = DateTime.UtcNow
        };

        _context.MenuCategories.Add(category);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetMenuCategory), new { id = category.Id }, new { category });
    }

    /// <summary>
    /// Update an existing menu category
    /// </summary>
    [HttpPut("categories/{id:int}")]
    public async Task<IActionResult> UpdateMenuCategory(int id, [FromBody] UpdateMenuCategoryRequest request)
    {
        var tenantId = int.Parse(HttpContext.Items["TenantId"]?.ToString() ?? "0");
        if (tenantId == 0)
        {
            return BadRequest("Tenant context is required");
        }

        var category = await _context.MenuCategories
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId);

        if (category == null)
        {
            return NotFound("Menu category not found");
        }

        category.Name = request.Name;
        category.Description = request.Description;
        category.MealType = request.MealType ?? "all";
        category.DisplayOrder = request.DisplayOrder;
        category.IsActive = request.IsActive ?? true;
        category.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new { category });
    }

    /// <summary>
    /// Delete a menu category (soft delete by setting IsActive to false)
    /// </summary>
    [HttpDelete("categories/{id:int}")]
    public async Task<IActionResult> DeleteMenuCategory(int id)
    {
        var tenantId = int.Parse(HttpContext.Items["TenantId"]?.ToString() ?? "0");
        if (tenantId == 0)
        {
            return BadRequest("Tenant context is required");
        }

        var category = await _context.MenuCategories
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId);

        if (category == null)
        {
            return NotFound("Menu category not found");
        }

        // Check if category has menu items
        var hasItems = await _context.MenuItems
            .AnyAsync(i => i.MenuCategoryId == id && i.TenantId == tenantId);

        if (hasItems)
        {
            // Soft delete - set IsActive to false
            category.IsActive = false;
            category.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return Ok(new { message = "Category deactivated successfully" });
        }
        else
        {
            // Hard delete if no items
            _context.MenuCategories.Remove(category);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }

    #endregion

    #region Menu Items

    /// <summary>
    /// Get all menu items with optional filtering
    /// </summary>
    [HttpGet("items")]
    public async Task<IActionResult> GetMenuItems(
        [FromQuery] int? categoryId = null,
        [FromQuery] string? mealType = null,
        [FromQuery] bool? isVegetarian = null,
        [FromQuery] bool? isVegan = null,
        [FromQuery] bool? isGlutenFree = null,
        [FromQuery] bool? isSpecial = null,
        [FromQuery] bool? isAvailable = null,
        [FromQuery] string? search = null)
    {
        var tenantId = int.Parse(HttpContext.Items["TenantId"]?.ToString() ?? "0");
        if (tenantId == 0)
        {
            return BadRequest("Tenant context is required");
        }

        var baseQuery = _context.MenuItems
            .Where(i => i.TenantId == tenantId);

        // Apply filters
        if (categoryId.HasValue)
        {
            baseQuery = baseQuery.Where(i => i.MenuCategoryId == categoryId.Value);
        }

        if (!string.IsNullOrEmpty(mealType))
        {
            baseQuery = baseQuery.Where(i => i.MealType == mealType || i.MealType == "all");
        }

        if (isVegetarian.HasValue)
        {
            baseQuery = baseQuery.Where(i => i.IsVegetarian == isVegetarian.Value);
        }

        if (isVegan.HasValue)
        {
            baseQuery = baseQuery.Where(i => i.IsVegan == isVegan.Value);
        }

        if (isGlutenFree.HasValue)
        {
            baseQuery = baseQuery.Where(i => i.IsGlutenFree == isGlutenFree.Value);
        }

        if (isSpecial.HasValue)
        {
            baseQuery = baseQuery.Where(i => i.IsSpecial == isSpecial.Value);
        }

        if (isAvailable.HasValue)
        {
            baseQuery = baseQuery.Where(i => i.IsAvailable == isAvailable.Value);
        }

        var query = baseQuery.Include(i => i.MenuCategory);

        var items = await query
            .Select(i => new
            {
                i.Id,
                i.MenuCategoryId,
                CategoryName = i.MenuCategory.Name,
                i.Name,
                i.Description,
                PriceFormatted = $"{i.Currency} {i.PriceCents / 100.0:F2}",
                i.PriceCents,
                i.Currency,
                i.Allergens,
                i.MealType,
                i.IsVegetarian,
                i.IsVegan,
                i.IsGlutenFree,
                i.IsSpicy,
                i.IsAvailable,
                i.IsSpecial,
                i.Tags,
                i.UpdatedAt
            })
            .ToListAsync();

        // Apply search filter after loading (for Tags array search)
        if (!string.IsNullOrEmpty(search))
        {
            var searchLower = search.ToLower();
            items = items.Where(i =>
                i.Name.ToLower().Contains(searchLower) ||
                i.Description.ToLower().Contains(searchLower) ||
                (i.Allergens != null && i.Allergens.ToLower().Contains(searchLower)) ||
                (i.Tags != null && i.Tags.Any(t => t.ToLower().Contains(searchLower)))
            ).ToList();
        }

        return Ok(new { items = items.OrderBy(i => i.CategoryName).ThenBy(i => i.Name) });
    }

    /// <summary>
    /// Get a specific menu item by ID
    /// </summary>
    [HttpGet("items/{id:int}")]
    public async Task<IActionResult> GetMenuItem(int id)
    {
        var tenantId = int.Parse(HttpContext.Items["TenantId"]?.ToString() ?? "0");
        if (tenantId == 0)
        {
            return BadRequest("Tenant context is required");
        }

        var item = await _context.MenuItems
            .Where(i => i.Id == id && i.TenantId == tenantId)
            .Include(i => i.MenuCategory)
            .Select(i => new
            {
                i.Id,
                i.MenuCategoryId,
                CategoryName = i.MenuCategory.Name,
                i.Name,
                i.Description,
                PriceFormatted = $"{i.Currency} {i.PriceCents / 100.0:F2}",
                i.PriceCents,
                i.Currency,
                i.Allergens,
                i.MealType,
                i.IsVegetarian,
                i.IsVegan,
                i.IsGlutenFree,
                i.IsSpicy,
                i.IsAvailable,
                i.IsSpecial,
                i.Tags,
                i.UpdatedAt
            })
            .FirstOrDefaultAsync();

        if (item == null)
        {
            return NotFound("Menu item not found");
        }

        return Ok(new { item });
    }

    /// <summary>
    /// Create a new menu item
    /// </summary>
    [HttpPost("items")]
    public async Task<IActionResult> CreateMenuItem([FromBody] CreateMenuItemRequest request)
    {
        var tenantId = int.Parse(HttpContext.Items["TenantId"]?.ToString() ?? "0");
        if (tenantId == 0)
        {
            return BadRequest("Tenant context is required");
        }

        // Verify category exists and belongs to tenant
        var categoryExists = await _context.MenuCategories
            .AnyAsync(c => c.Id == request.MenuCategoryId && c.TenantId == tenantId);

        if (!categoryExists)
        {
            return BadRequest("Invalid menu category");
        }

        var item = new MenuItem
        {
            TenantId = tenantId,
            MenuCategoryId = request.MenuCategoryId,
            Name = request.Name,
            Description = request.Description,
            PriceCents = request.PriceCents,
            Currency = request.Currency ?? "ZAR",
            Allergens = request.Allergens,
            MealType = request.MealType ?? "all",
            IsVegetarian = request.IsVegetarian ?? false,
            IsVegan = request.IsVegan ?? false,
            IsGlutenFree = request.IsGlutenFree ?? false,
            IsSpicy = request.IsSpicy ?? false,
            IsAvailable = request.IsAvailable ?? true,
            IsSpecial = request.IsSpecial ?? false,
            Tags = request.Tags ?? Array.Empty<string>(),
            UpdatedAt = DateTime.UtcNow
        };

        _context.MenuItems.Add(item);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetMenuItem), new { id = item.Id }, new { item });
    }

    /// <summary>
    /// Update an existing menu item
    /// </summary>
    [HttpPut("items/{id:int}")]
    public async Task<IActionResult> UpdateMenuItem(int id, [FromBody] UpdateMenuItemRequest request)
    {
        var tenantId = int.Parse(HttpContext.Items["TenantId"]?.ToString() ?? "0");
        if (tenantId == 0)
        {
            return BadRequest("Tenant context is required");
        }

        var item = await _context.MenuItems
            .FirstOrDefaultAsync(i => i.Id == id && i.TenantId == tenantId);

        if (item == null)
        {
            return NotFound("Menu item not found");
        }

        // Verify category exists and belongs to tenant if changing category
        if (request.MenuCategoryId != item.MenuCategoryId)
        {
            var categoryExists = await _context.MenuCategories
                .AnyAsync(c => c.Id == request.MenuCategoryId && c.TenantId == tenantId);

            if (!categoryExists)
            {
                return BadRequest("Invalid menu category");
            }
        }

        item.MenuCategoryId = request.MenuCategoryId;
        item.Name = request.Name;
        item.Description = request.Description;
        item.PriceCents = request.PriceCents;
        item.Currency = request.Currency ?? "ZAR";
        item.Allergens = request.Allergens;
        item.MealType = request.MealType ?? "all";
        item.IsVegetarian = request.IsVegetarian ?? false;
        item.IsVegan = request.IsVegan ?? false;
        item.IsGlutenFree = request.IsGlutenFree ?? false;
        item.IsSpicy = request.IsSpicy ?? false;
        item.IsAvailable = request.IsAvailable ?? true;
        item.IsSpecial = request.IsSpecial ?? false;
        item.Tags = request.Tags ?? Array.Empty<string>();
        item.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new { item });
    }

    /// <summary>
    /// Delete a menu item
    /// </summary>
    [HttpDelete("items/{id:int}")]
    public async Task<IActionResult> DeleteMenuItem(int id)
    {
        var tenantId = int.Parse(HttpContext.Items["TenantId"]?.ToString() ?? "0");
        if (tenantId == 0)
        {
            return BadRequest("Tenant context is required");
        }

        var item = await _context.MenuItems
            .FirstOrDefaultAsync(i => i.Id == id && i.TenantId == tenantId);

        if (item == null)
        {
            return NotFound("Menu item not found");
        }

        _context.MenuItems.Remove(item);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    #endregion

    #region Menu Specials

    /// <summary>
    /// Get all menu specials for the current tenant
    /// </summary>
    [HttpGet("specials")]
    public async Task<IActionResult> GetMenuSpecials([FromQuery] string? mealType = null, [FromQuery] bool? activeOnly = true)
    {
        var tenantId = int.Parse(HttpContext.Items["TenantId"]?.ToString() ?? "0");
        if (tenantId == 0)
        {
            return BadRequest("Tenant context is required");
        }

        var baseQuery = _context.MenuSpecials
            .Where(s => s.TenantId == tenantId);

        if (activeOnly == true)
        {
            baseQuery = baseQuery.Where(s => s.IsActive);
        }

        if (!string.IsNullOrEmpty(mealType))
        {
            baseQuery = baseQuery.Where(s => s.MealType == mealType || s.MealType == "all");
        }

        var query = baseQuery.Include(s => s.MenuItem);

        var specials = await query
            .Select(s => new
            {
                s.Id,
                s.MenuItemId,
                MenuItemName = s.MenuItem != null ? s.MenuItem.Name : null,
                s.Title,
                s.Description,
                SpecialPriceFormatted = s.SpecialPriceCents.HasValue ? $"ZAR {s.SpecialPriceCents.Value / 100.0:F2}" : null,
                s.SpecialPriceCents,
                s.SpecialType,
                s.DayOfWeek,
                s.ValidFrom,
                s.ValidTo,
                s.MealType,
                s.IsActive,
                s.UpdatedAt
            })
            .OrderBy(s => s.ValidFrom)
            .ThenBy(s => s.Title)
            .ToListAsync();

        return Ok(new { specials });
    }

    /// <summary>
    /// Create a new menu special
    /// </summary>
    [HttpPost("specials")]
    public async Task<IActionResult> CreateMenuSpecial([FromBody] CreateMenuSpecialRequest request)
    {
        var tenantId = int.Parse(HttpContext.Items["TenantId"]?.ToString() ?? "0");
        if (tenantId == 0)
        {
            return BadRequest("Tenant context is required");
        }

        // Verify menu item exists and belongs to tenant if specified
        if (request.MenuItemId.HasValue)
        {
            var itemExists = await _context.MenuItems
                .AnyAsync(i => i.Id == request.MenuItemId.Value && i.TenantId == tenantId);

            if (!itemExists)
            {
                return BadRequest("Invalid menu item");
            }
        }

        var special = new MenuSpecial
        {
            TenantId = tenantId,
            MenuItemId = request.MenuItemId,
            Title = request.Title,
            Description = request.Description,
            SpecialPriceCents = request.SpecialPriceCents,
            SpecialType = request.SpecialType ?? "daily",
            DayOfWeek = request.DayOfWeek,
            ValidFrom = request.ValidFrom,
            ValidTo = request.ValidTo,
            MealType = request.MealType ?? "all",
            IsActive = request.IsActive ?? true,
            UpdatedAt = DateTime.UtcNow
        };

        _context.MenuSpecials.Add(special);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetMenuSpecials), new { }, new { special });
    }

    /// <summary>
    /// Update an existing menu special
    /// </summary>
    [HttpPut("specials/{id:int}")]
    public async Task<IActionResult> UpdateMenuSpecial(int id, [FromBody] UpdateMenuSpecialRequest request)
    {
        var tenantId = int.Parse(HttpContext.Items["TenantId"]?.ToString() ?? "0");
        if (tenantId == 0)
        {
            return BadRequest("Tenant context is required");
        }

        var special = await _context.MenuSpecials
            .FirstOrDefaultAsync(s => s.Id == id && s.TenantId == tenantId);

        if (special == null)
        {
            return NotFound("Menu special not found");
        }

        // Verify menu item exists and belongs to tenant if specified
        if (request.MenuItemId.HasValue)
        {
            var itemExists = await _context.MenuItems
                .AnyAsync(i => i.Id == request.MenuItemId.Value && i.TenantId == tenantId);

            if (!itemExists)
            {
                return BadRequest("Invalid menu item");
            }
        }

        special.MenuItemId = request.MenuItemId;
        special.Title = request.Title;
        special.Description = request.Description;
        special.SpecialPriceCents = request.SpecialPriceCents;
        special.SpecialType = request.SpecialType ?? "daily";
        special.DayOfWeek = request.DayOfWeek;
        special.ValidFrom = request.ValidFrom;
        special.ValidTo = request.ValidTo;
        special.MealType = request.MealType ?? "all";
        special.IsActive = request.IsActive ?? true;
        special.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new { special });
    }

    /// <summary>
    /// Delete a menu special
    /// </summary>
    [HttpDelete("specials/{id:int}")]
    public async Task<IActionResult> DeleteMenuSpecial(int id)
    {
        var tenantId = int.Parse(HttpContext.Items["TenantId"]?.ToString() ?? "0");
        if (tenantId == 0)
        {
            return BadRequest("Tenant context is required");
        }

        var special = await _context.MenuSpecials
            .FirstOrDefaultAsync(s => s.Id == id && s.TenantId == tenantId);

        if (special == null)
        {
            return NotFound("Menu special not found");
        }

        _context.MenuSpecials.Remove(special);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    #endregion

    #region Statistics

    /// <summary>
    /// Get menu statistics for the current tenant
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetMenuStats()
    {
        var tenantId = int.Parse(HttpContext.Items["TenantId"]?.ToString() ?? "0");
        if (tenantId == 0)
        {
            return BadRequest("Tenant context is required");
        }

        var totalCategories = await _context.MenuCategories
            .Where(c => c.TenantId == tenantId && c.IsActive)
            .CountAsync();

        var totalItems = await _context.MenuItems
            .Where(i => i.TenantId == tenantId)
            .CountAsync();

        var availableItems = await _context.MenuItems
            .Where(i => i.TenantId == tenantId && i.IsAvailable)
            .CountAsync();

        var specialItems = await _context.MenuItems
            .Where(i => i.TenantId == tenantId && i.IsSpecial)
            .CountAsync();

        var activeSpecials = await _context.MenuSpecials
            .Where(s => s.TenantId == tenantId && s.IsActive)
            .CountAsync();

        var itemsByMealType = await _context.MenuItems
            .Where(i => i.TenantId == tenantId)
            .GroupBy(i => i.MealType)
            .Select(g => new
            {
                MealType = g.Key,
                Count = g.Count()
            })
            .ToListAsync();

        var itemsByDietaryRestrictions = new
        {
            Vegetarian = await _context.MenuItems.Where(i => i.TenantId == tenantId && i.IsVegetarian).CountAsync(),
            Vegan = await _context.MenuItems.Where(i => i.TenantId == tenantId && i.IsVegan).CountAsync(),
            GlutenFree = await _context.MenuItems.Where(i => i.TenantId == tenantId && i.IsGlutenFree).CountAsync(),
            Spicy = await _context.MenuItems.Where(i => i.TenantId == tenantId && i.IsSpicy).CountAsync()
        };

        var averagePrice = await _context.MenuItems
            .Where(i => i.TenantId == tenantId && i.IsAvailable)
            .AverageAsync(i => (double?)i.PriceCents) ?? 0;

        var priceRange = new
        {
            Min = await _context.MenuItems
                .Where(i => i.TenantId == tenantId && i.IsAvailable)
                .MinAsync(i => (int?)i.PriceCents) ?? 0,
            Max = await _context.MenuItems
                .Where(i => i.TenantId == tenantId && i.IsAvailable)
                .MaxAsync(i => (int?)i.PriceCents) ?? 0
        };

        return Ok(new
        {
            totalCategories = totalCategories,
            totalItems = totalItems,
            totalSpecials = activeSpecials,
            itemsByMealType = itemsByMealType.Select(x => new { mealType = x.MealType, count = x.Count }).ToArray(),
            itemsByDietaryType = new[]
            {
                new { type = "Vegetarian", count = itemsByDietaryRestrictions.Vegetarian },
                new { type = "Vegan", count = itemsByDietaryRestrictions.Vegan },
                new { type = "Gluten-Free", count = itemsByDietaryRestrictions.GlutenFree },
                new { type = "Spicy", count = itemsByDietaryRestrictions.Spicy }
            },
            averagePrice = Math.Round(averagePrice / 100.0, 2),
            minPrice = Math.Round(priceRange.Min / 100.0, 2),
            maxPrice = Math.Round(priceRange.Max / 100.0, 2),
            currency = "ZAR"
        });
    }

    #endregion
}

#region DTOs

public class CreateMenuCategoryRequest
{
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [MaxLength(20)]
    public string? MealType { get; set; } = "all";

    public int DisplayOrder { get; set; } = 0;

    public bool? IsActive { get; set; } = true;
}

public class UpdateMenuCategoryRequest
{
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [MaxLength(20)]
    public string? MealType { get; set; } = "all";

    public int DisplayOrder { get; set; } = 0;

    public bool? IsActive { get; set; } = true;
}

public class CreateMenuItemRequest
{
    [Required]
    public int MenuCategoryId { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string Description { get; set; } = string.Empty;

    [Required]
    public int PriceCents { get; set; }

    [MaxLength(20)]
    public string? Currency { get; set; } = "ZAR";

    [MaxLength(100)]
    public string? Allergens { get; set; }

    [MaxLength(20)]
    public string? MealType { get; set; } = "all";

    public bool? IsVegetarian { get; set; } = false;

    public bool? IsVegan { get; set; } = false;

    public bool? IsGlutenFree { get; set; } = false;

    public bool? IsSpicy { get; set; } = false;

    public bool? IsAvailable { get; set; } = true;

    public bool? IsSpecial { get; set; } = false;

    public string[]? Tags { get; set; }
}

public class UpdateMenuItemRequest
{
    [Required]
    public int MenuCategoryId { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string Description { get; set; } = string.Empty;

    [Required]
    public int PriceCents { get; set; }

    [MaxLength(20)]
    public string? Currency { get; set; } = "ZAR";

    [MaxLength(100)]
    public string? Allergens { get; set; }

    [MaxLength(20)]
    public string? MealType { get; set; } = "all";

    public bool? IsVegetarian { get; set; } = false;

    public bool? IsVegan { get; set; } = false;

    public bool? IsGlutenFree { get; set; } = false;

    public bool? IsSpicy { get; set; } = false;

    public bool? IsAvailable { get; set; } = true;

    public bool? IsSpecial { get; set; } = false;

    public string[]? Tags { get; set; }
}

public class CreateMenuSpecialRequest
{
    public int? MenuItemId { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Description { get; set; } = string.Empty;

    public int? SpecialPriceCents { get; set; }

    [MaxLength(20)]
    public string? SpecialType { get; set; } = "daily";

    public int? DayOfWeek { get; set; }

    public DateOnly? ValidFrom { get; set; }

    public DateOnly? ValidTo { get; set; }

    [MaxLength(20)]
    public string? MealType { get; set; } = "all";

    public bool? IsActive { get; set; } = true;
}

public class UpdateMenuSpecialRequest
{
    public int? MenuItemId { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Description { get; set; } = string.Empty;

    public int? SpecialPriceCents { get; set; }

    [MaxLength(20)]
    public string? SpecialType { get; set; } = "daily";

    public int? DayOfWeek { get; set; }

    public DateOnly? ValidFrom { get; set; }

    public DateOnly? ValidTo { get; set; }

    [MaxLength(20)]
    public string? MealType { get; set; } = "all";

    public bool? IsActive { get; set; } = true;
}

#endregion