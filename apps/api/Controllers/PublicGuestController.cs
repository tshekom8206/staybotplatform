using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Hostr.Api.Data;
using Hostr.Api.Models;
using System.ComponentModel.DataAnnotations;

namespace Hostr.Api.Controllers;

/// <summary>
/// Public endpoints for the Guest Portal PWA.
/// No authentication required - accessed via tenant slug subdomain.
/// </summary>
[ApiController]
[Route("api/public/{slug}")]
public class PublicGuestController : ControllerBase
{
    private readonly HostrDbContext _context;
    private readonly ILogger<PublicGuestController> _logger;

    public PublicGuestController(
        HostrDbContext context,
        ILogger<PublicGuestController> logger)
    {
        _context = context;
        _logger = logger;
    }

    private async Task<Tenant?> GetTenantBySlugAsync(string slug)
    {
        return await _context.Tenants
            .FirstOrDefaultAsync(t => t.Slug.ToLower() == slug.ToLower() && t.Status == "Active");
    }

    #region Hotel Information

    /// <summary>
    /// Get hotel info for branding and display
    /// </summary>
    [HttpGet("info")]
    public async Task<IActionResult> GetHotelInfo(string slug)
    {
        try
        {
            var tenant = await GetTenantBySlugAsync(slug);
            if (tenant == null)
            {
                return NotFound(new { error = "Hotel not found" });
            }

            // Get hotel contact info
            var hotelInfo = await _context.HotelInfos
                .Where(h => h.TenantId == tenant.Id)
                .Select(h => new
                {
                    h.LogoUrl,
                    h.Phone,
                    h.Email,
                    h.Website,
                    h.Street,
                    h.City,
                    h.State,
                    h.Country,
                    h.CheckInTime,
                    h.CheckOutTime,
                    h.FacebookUrl,
                    h.InstagramUrl,
                    h.TwitterUrl
                })
                .FirstOrDefaultAsync();

            // Build address from components
            var addressParts = new List<string>();
            if (!string.IsNullOrEmpty(hotelInfo?.Street)) addressParts.Add(hotelInfo.Street);
            if (!string.IsNullOrEmpty(hotelInfo?.City)) addressParts.Add(hotelInfo.City);
            if (!string.IsNullOrEmpty(hotelInfo?.State)) addressParts.Add(hotelInfo.State);
            if (!string.IsNullOrEmpty(hotelInfo?.Country)) addressParts.Add(hotelInfo.Country);
            var address = addressParts.Any() ? string.Join(", ", addressParts) : null;

            return Ok(new
            {
                id = tenant.Id,
                name = tenant.Name,
                slug = tenant.Slug,
                logoUrl = tenant.LogoUrl ?? hotelInfo?.LogoUrl,
                backgroundImageUrl = tenant.BackgroundImageUrl,
                themePrimary = tenant.ThemePrimary ?? "#1976d2",
                phone = tenant.Phone ?? hotelInfo?.Phone,
                whatsappNumber = tenant.WhatsAppNumber ?? tenant.Phone ?? hotelInfo?.Phone,
                email = hotelInfo?.Email,
                address = address,
                checkInTime = hotelInfo?.CheckInTime,
                checkOutTime = hotelInfo?.CheckOutTime,
                socialLinks = new
                {
                    facebook = hotelInfo?.FacebookUrl,
                    instagram = hotelInfo?.InstagramUrl,
                    twitter = hotelInfo?.TwitterUrl,
                    website = hotelInfo?.Website
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting hotel info for slug {Slug}", slug);
            return StatusCode(500, new { error = "Error retrieving hotel information" });
        }
    }

    /// <summary>
    /// Get "Our Guest Promise" content
    /// </summary>
    [HttpGet("guest-promise")]
    public async Task<IActionResult> GetGuestPromise(string slug)
    {
        try
        {
            var tenant = await GetTenantBySlugAsync(slug);
            if (tenant == null)
            {
                return NotFound(new { error = "Hotel not found" });
            }

            var promiseContent = await _context.BusinessInfo
                .Where(b => b.TenantId == tenant.Id && b.Category == "guest_promise")
                .Select(b => new
                {
                    b.Title,
                    b.Content
                })
                .FirstOrDefaultAsync();

            if (promiseContent == null)
            {
                // Return default guest promise
                return Ok(new
                {
                    title = "Our Guest Promise",
                    content = "{\"promises\": [\"Your comfort is our priority\", \"24/7 concierge at your fingertips\", \"Personalized service, every stay\", \"We listen, we act, we care\"]}"
                });
            }

            return Ok(promiseContent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting guest promise for slug {Slug}", slug);
            return StatusCode(500, new { error = "Error retrieving guest promise" });
        }
    }

    #endregion

    #region Food & Drinks Menu

    /// <summary>
    /// Get menu categories with items for guest portal
    /// </summary>
    [HttpGet("menu")]
    public async Task<IActionResult> GetMenu(string slug, [FromQuery] string? mealType = null)
    {
        try
        {
            var tenant = await GetTenantBySlugAsync(slug);
            if (tenant == null)
            {
                return NotFound(new { error = "Hotel not found" });
            }

            var baseQuery = _context.MenuCategories
                .Where(c => c.TenantId == tenant.Id && c.IsActive);

            if (!string.IsNullOrEmpty(mealType))
            {
                baseQuery = baseQuery.Where(c => c.MealType == mealType || c.MealType == "all");
            }

            var categories = await baseQuery
                .Include(c => c.MenuItems.Where(i => i.IsAvailable))
                .OrderBy(c => c.DisplayOrder)
                .ThenBy(c => c.Name)
                .Select(c => new
                {
                    c.Id,
                    c.Name,
                    c.Description,
                    c.MealType,
                    icon = GetCategoryIcon(c.Name),
                    items = c.MenuItems
                        .OrderBy(i => i.Name)
                        .Select(i => new
                        {
                            i.Id,
                            i.Name,
                            i.Description,
                            price = $"{i.Currency} {i.PriceCents / 100.0:F2}",
                            i.PriceCents,
                            i.Currency,
                            i.Allergens,
                            i.IsVegetarian,
                            i.IsVegan,
                            i.IsGlutenFree,
                            i.IsSpicy,
                            i.IsSpecial,
                            i.Tags
                        })
                        .ToList()
                })
                .ToListAsync();

            return Ok(new { categories });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting menu for slug {Slug}", slug);
            return StatusCode(500, new { error = "Error retrieving menu" });
        }
    }

    /// <summary>
    /// Get menu items by category
    /// </summary>
    [HttpGet("menu/category/{categoryId:int}")]
    public async Task<IActionResult> GetMenuByCategory(string slug, int categoryId)
    {
        try
        {
            var tenant = await GetTenantBySlugAsync(slug);
            if (tenant == null)
            {
                return NotFound(new { error = "Hotel not found" });
            }

            var category = await _context.MenuCategories
                .Where(c => c.Id == categoryId && c.TenantId == tenant.Id && c.IsActive)
                .Include(c => c.MenuItems.Where(i => i.IsAvailable))
                .Select(c => new
                {
                    c.Id,
                    c.Name,
                    c.Description,
                    c.MealType,
                    items = c.MenuItems
                        .OrderBy(i => i.Name)
                        .Select(i => new
                        {
                            i.Id,
                            i.Name,
                            i.Description,
                            price = $"{i.Currency} {i.PriceCents / 100.0:F2}",
                            i.PriceCents,
                            i.Currency,
                            i.Allergens,
                            i.IsVegetarian,
                            i.IsVegan,
                            i.IsGlutenFree,
                            i.IsSpicy,
                            i.IsSpecial
                        })
                        .ToList()
                })
                .FirstOrDefaultAsync();

            if (category == null)
            {
                return NotFound(new { error = "Category not found" });
            }

            return Ok(category);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting menu category {CategoryId} for slug {Slug}", categoryId, slug);
            return StatusCode(500, new { error = "Error retrieving menu category" });
        }
    }

    private static string GetCategoryIcon(string categoryName)
    {
        return categoryName.ToLower() switch
        {
            "breakfast" => "bi-egg-fried",
            "lunch" => "bi-cup-hot",
            "dinner" => "bi-moon-stars",
            "bar" or "drinks" or "beverages" => "bi-cup-straw",
            "desserts" or "dessert" => "bi-cake2",
            "snacks" => "bi-cookie",
            "pizza" => "bi-pizza",
            "coffee" => "bi-cup-hot-fill",
            _ => "bi-egg-fried"
        };
    }

    #endregion

    #region Services/Amenities

    /// <summary>
    /// Get hotel services/amenities
    /// </summary>
    [HttpGet("services")]
    public async Task<IActionResult> GetServices(string slug, [FromQuery] string? category = null)
    {
        try
        {
            var tenant = await GetTenantBySlugAsync(slug);
            if (tenant == null)
            {
                return NotFound(new { error = "Hotel not found" });
            }

            var query = _context.Services
                .Where(s => s.TenantId == tenant.Id && s.IsAvailable);

            if (!string.IsNullOrEmpty(category))
            {
                query = query.Where(s => s.Category == category);
            }

            var services = await query
                .OrderBy(s => s.Category)
                .ThenBy(s => s.Name)
                .Select(s => new
                {
                    s.Id,
                    s.Name,
                    s.Description,
                    s.Category,
                    s.IsChargeable,
                    price = s.IsChargeable ? $"{s.Currency} {s.Price:F2}" : "Complimentary",
                    priceAmount = s.Price,
                    s.Currency,
                    s.PricingUnit,
                    s.AvailableHours,
                    s.ContactInfo,
                    requiresBooking = s.RequiresAdvanceBooking,
                    advanceBookingHours = s.AdvanceBookingHours
                })
                .ToListAsync();

            // Group by category
            var grouped = services
                .GroupBy(s => s.Category)
                .Select(g => new
                {
                    category = g.Key,
                    icon = GetServiceCategoryIcon(g.Key ?? ""),
                    services = g.ToList()
                })
                .ToList();

            return Ok(new { categories = grouped });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting services for slug {Slug}", slug);
            return StatusCode(500, new { error = "Error retrieving services" });
        }
    }

    private static string GetServiceCategoryIcon(string category)
    {
        return category.ToLower() switch
        {
            "spa" or "wellness" => "bi-flower1",
            "pool" or "swimming" => "bi-water",
            "gym" or "fitness" => "bi-activity",
            "restaurant" or "dining" => "bi-cup-hot",
            "laundry" => "bi-bucket",
            "transport" or "shuttle" => "bi-car-front",
            "business" or "conference" => "bi-briefcase",
            "kids" or "family" => "bi-balloon",
            _ => "bi-star"
        };
    }

    #endregion

    #region Maintenance Requests

    /// <summary>
    /// Get available maintenance issue categories
    /// </summary>
    [HttpGet("maintenance-categories")]
    public async Task<IActionResult> GetMaintenanceCategories(string slug)
    {
        try
        {
            var tenant = await GetTenantBySlugAsync(slug);
            if (tenant == null)
            {
                return NotFound(new { error = "Hotel not found" });
            }

            // Define common maintenance categories
            var categories = new[]
            {
                new { id = "plumbing", name = "Plumbing", icon = "bi-droplet", description = "Leaks, blocked drains, toilet issues" },
                new { id = "electrical", name = "Electrical", icon = "bi-lightning", description = "Lights, outlets, switches" },
                new { id = "aircon", name = "Air Conditioning", icon = "bi-thermometer-half", description = "AC not working, too hot/cold" },
                new { id = "tv", name = "TV/Entertainment", icon = "bi-tv", description = "TV, remote, channels" },
                new { id = "wifi", name = "WiFi/Internet", icon = "bi-wifi", description = "Connection issues, slow speed" },
                new { id = "cleaning", name = "Cleaning", icon = "bi-brush", description = "Room needs cleaning" },
                new { id = "furniture", name = "Furniture", icon = "bi-house-door", description = "Broken furniture, fixtures" },
                new { id = "appliances", name = "Appliances", icon = "bi-gear", description = "Kettle, fridge, safe" },
                new { id = "other", name = "Other", icon = "bi-three-dots", description = "Other maintenance issues" }
            };

            return Ok(new { categories });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting maintenance categories for slug {Slug}", slug);
            return StatusCode(500, new { error = "Error retrieving maintenance categories" });
        }
    }

    /// <summary>
    /// Submit a maintenance request from guest portal
    /// </summary>
    [HttpPost("maintenance")]
    public async Task<IActionResult> SubmitMaintenanceRequest(string slug, [FromBody] GuestMaintenanceRequest request)
    {
        try
        {
            var tenant = await GetTenantBySlugAsync(slug);
            if (tenant == null)
            {
                return NotFound(new { error = "Hotel not found" });
            }

            if (string.IsNullOrEmpty(request.RoomNumber))
            {
                return BadRequest(new { error = "Room number is required" });
            }

            if (request.Issues == null || !request.Issues.Any())
            {
                return BadRequest(new { error = "At least one issue must be selected" });
            }

            // Determine priority based on issue categories
            var priority = DetermineMaintenancePriority(request.Issues);
            var department = "Maintenance";

            // Create staff task
            var task = new StaffTask
            {
                TenantId = tenant.Id,
                Title = $"Guest Maintenance Request - Room {request.RoomNumber}",
                Description = $"Issues reported: {string.Join(", ", request.Issues)}\n\nAdditional details: {request.Description ?? "None provided"}",
                TaskType = "maintenance",
                Department = department,
                Priority = priority,
                Status = "Open",
                RoomNumber = request.RoomNumber,
                Notes = $"Submitted via Guest Portal at {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.StaffTasks.Add(task);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Guest maintenance request created: Task {TaskId} for Room {RoomNumber} at Tenant {TenantId}",
                task.Id, request.RoomNumber, tenant.Id);

            return Ok(new
            {
                success = true,
                message = "Your maintenance request has been submitted. Our team will attend to it shortly.",
                taskId = task.Id,
                estimatedResponse = priority == "Urgent" ? "15-30 minutes" : "1-2 hours"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting maintenance request for slug {Slug}", slug);
            return StatusCode(500, new { error = "Error submitting maintenance request" });
        }
    }

    private static string DetermineMaintenancePriority(string[] issues)
    {
        var urgentKeywords = new[] { "electrical", "plumbing", "leak", "flood", "fire", "smoke", "gas", "safety" };
        var highKeywords = new[] { "aircon", "ac", "no water", "no power" };

        foreach (var issue in issues)
        {
            if (urgentKeywords.Any(k => issue.ToLower().Contains(k)))
                return "Urgent";
        }

        foreach (var issue in issues)
        {
            if (highKeywords.Any(k => issue.ToLower().Contains(k)))
                return "High";
        }

        return "Normal";
    }

    #endregion

    #region Lost & Found

    /// <summary>
    /// Get list of found items that guests can inquire about
    /// </summary>
    [HttpGet("lost-found")]
    public async Task<IActionResult> GetFoundItems(string slug, [FromQuery] string? category = null)
    {
        try
        {
            var tenant = await GetTenantBySlugAsync(slug);
            if (tenant == null)
            {
                return NotFound(new { error = "Hotel not found" });
            }

            var query = _context.FoundItems
                .Where(f => f.TenantId == tenant.Id &&
                       (f.Status == "AVAILABLE" || f.Status == "InStorage" || f.Status == "IN_STORAGE"));

            if (!string.IsNullOrEmpty(category))
            {
                query = query.Where(f => f.Category == category);
            }

            var items = await query
                .OrderByDescending(f => f.FoundAt)
                .Select(f => new
                {
                    f.Id,
                    f.ItemName,
                    f.Category,
                    description = f.Description != null ? (f.Description.Length > 100 ? f.Description.Substring(0, 100) + "..." : f.Description) : null,
                    f.Color,
                    f.Brand,
                    f.LocationFound,
                    foundDate = f.FoundAt.ToString("dd MMM yyyy")
                })
                .Take(50)
                .ToListAsync();

            return Ok(new { items });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting found items for slug {Slug}", slug);
            return StatusCode(500, new { error = "Error retrieving found items" });
        }
    }

    /// <summary>
    /// Report a lost item from guest portal
    /// </summary>
    [HttpPost("lost-found")]
    public async Task<IActionResult> ReportLostItem(string slug, [FromBody] GuestLostItemRequest request)
    {
        try
        {
            var tenant = await GetTenantBySlugAsync(slug);
            if (tenant == null)
            {
                return NotFound(new { error = "Hotel not found" });
            }

            if (string.IsNullOrEmpty(request.ItemName))
            {
                return BadRequest(new { error = "Item name is required" });
            }

            var lostItem = new LostItem
            {
                TenantId = tenant.Id,
                ItemName = request.ItemName,
                Category = request.Category ?? "Other",
                Description = request.Description,
                Color = request.Color,
                Brand = request.Brand,
                LocationLost = request.LocationLost,
                ReportedAt = DateTime.UtcNow,
                ReporterName = request.GuestName,
                ReporterPhone = request.Phone,
                RoomNumber = request.RoomNumber,
                Status = "Open",
                SpecialInstructions = "Reported via Guest Portal"
            };

            _context.LostItems.Add(lostItem);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Lost item reported via Guest Portal: {ItemName} by {GuestName} at Tenant {TenantId}",
                request.ItemName, request.GuestName, tenant.Id);

            return Ok(new
            {
                success = true,
                message = "Your lost item report has been submitted. We will contact you if we find a matching item.",
                reportId = lostItem.Id
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reporting lost item for slug {Slug}", slug);
            return StatusCode(500, new { error = "Error submitting lost item report" });
        }
    }

    #endregion

    #region Ratings

    /// <summary>
    /// Submit a rating from guest portal
    /// </summary>
    [HttpPost("rating")]
    public async Task<IActionResult> SubmitRating(string slug, [FromBody] GuestRatingRequest request)
    {
        try
        {
            var tenant = await GetTenantBySlugAsync(slug);
            if (tenant == null)
            {
                return NotFound(new { error = "Hotel not found" });
            }

            if (request.Rating < 1 || request.Rating > 5)
            {
                return BadRequest(new { error = "Rating must be between 1 and 5" });
            }

            var rating = new Rating
            {
                TenantId = tenant.Id,
                Score = request.Rating,
                Comment = $"{(request.GuestName != null ? $"Guest: {request.GuestName}\n" : "")}{(request.RoomNumber != null ? $"Room: {request.RoomNumber}\n" : "")}{request.Comment ?? ""}",
                GuestPhone = request.Phone ?? "guest-portal",
                Source = "GuestPortal",
                Status = "received",
                ReceivedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };

            _context.Ratings.Add(rating);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Rating submitted via Guest Portal: {Score}/5 for Tenant {TenantId}",
                request.Rating, tenant.Id);

            return Ok(new
            {
                success = true,
                message = "Thank you for your feedback!"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting rating for slug {Slug}", slug);
            return StatusCode(500, new { error = "Error submitting rating" });
        }
    }

    #endregion

    #region Languages

    /// <summary>
    /// Get supported languages for the guest portal
    /// </summary>
    [HttpGet("languages")]
    public async Task<IActionResult> GetLanguages(string slug)
    {
        try
        {
            var tenant = await GetTenantBySlugAsync(slug);
            if (tenant == null)
            {
                return NotFound(new { error = "Hotel not found" });
            }

            // Get globally available languages from reference table
            var languages = await _context.SupportedLanguages
                .Where(l => l.IsActive)
                .OrderBy(l => l.SortOrder)
                .Select(l => new
                {
                    code = l.Code,
                    name = l.Name,
                    nativeName = l.Name, // Could add a NativeName column to the model later
                    flag = GetLanguageFlag(l.Code)
                })
                .ToListAsync();

            if (languages.Any())
            {
                return Ok(new { languages });
            }

            // Default languages if none configured
            var defaultLanguages = new[]
            {
                new { code = "en", name = "English", nativeName = "English", flag = "GB" },
                new { code = "af", name = "Afrikaans", nativeName = "Afrikaans", flag = "ZA" },
                new { code = "zu", name = "Zulu", nativeName = "isiZulu", flag = "ZA" },
                new { code = "fr", name = "French", nativeName = "Français", flag = "FR" },
                new { code = "de", name = "German", nativeName = "Deutsch", flag = "DE" },
                new { code = "zh", name = "Chinese", nativeName = "中文", flag = "CN" }
            };

            return Ok(new { languages = defaultLanguages });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting languages for slug {Slug}", slug);
            return StatusCode(500, new { error = "Error retrieving languages" });
        }
    }

    private static string GetLanguageFlag(string languageCode)
    {
        return languageCode.ToLower() switch
        {
            "en" => "GB",
            "af" => "ZA",
            "zu" => "ZA",
            "fr" => "FR",
            "de" => "DE",
            "zh" => "CN",
            "pt" => "PT",
            "es" => "ES",
            "it" => "IT",
            "nl" => "NL",
            _ => "UN"
        };
    }

    #endregion
}

#region Request DTOs

public class GuestMaintenanceRequest
{
    [Required]
    public string RoomNumber { get; set; } = string.Empty;

    [Required]
    public string[] Issues { get; set; } = Array.Empty<string>();

    public string? Description { get; set; }
}

public class GuestLostItemRequest
{
    [Required, MaxLength(100)]
    public string ItemName { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? Category { get; set; }

    public string? Description { get; set; }

    [MaxLength(30)]
    public string? Color { get; set; }

    [MaxLength(50)]
    public string? Brand { get; set; }

    [MaxLength(100)]
    public string? LocationLost { get; set; }

    [MaxLength(100)]
    public string? GuestName { get; set; }

    [MaxLength(20)]
    public string? Phone { get; set; }

    [MaxLength(20)]
    public string? RoomNumber { get; set; }
}

public class GuestRatingRequest
{
    [Required, Range(1, 5)]
    public int Rating { get; set; }

    [MaxLength(1000)]
    public string? Comment { get; set; }

    [MaxLength(20)]
    public string? RoomNumber { get; set; }

    [MaxLength(100)]
    public string? GuestName { get; set; }

    [MaxLength(20)]
    public string? Phone { get; set; }
}

#endregion
