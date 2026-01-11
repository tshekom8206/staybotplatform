using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Hostr.Api.Data;
using Hostr.Api.DTOs;
using Hostr.Api.Hubs;
using Hostr.Api.Models;
using Hostr.Api.Services;
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
    private readonly IHubContext<StaffTaskHub> _staffTaskHub;
    private readonly IRoomValidationService _roomValidationService;
    private readonly IProactiveMessageService _proactiveMessageService;
    private readonly ISmsService _smsService;
    private readonly IConfiguration _configuration;

    public PublicGuestController(
        HostrDbContext context,
        ILogger<PublicGuestController> logger,
        IHubContext<StaffTaskHub> staffTaskHub,
        IRoomValidationService roomValidationService,
        IProactiveMessageService proactiveMessageService,
        ISmsService smsService,
        IConfiguration configuration)
    {
        _context = context;
        _logger = logger;
        _staffTaskHub = staffTaskHub;
        _roomValidationService = roomValidationService;
        _proactiveMessageService = proactiveMessageService;
        _smsService = smsService;
        _configuration = configuration;
    }

    private async Task<Tenant?> GetTenantBySlugAsync(string slug)
    {
        return await _context.Tenants
            .FirstOrDefaultAsync(t => t.Slug.ToLower() == slug.ToLower() && t.Status == "Active");
    }

    #region Hotel Information

    /// <summary>
    /// Get PWA manifest with tenant branding
    /// </summary>
    [HttpGet("manifest.webmanifest")]
    [Produces("application/manifest+json")]
    public async Task<IActionResult> GetManifest(string slug)
    {
        try
        {
            var tenant = await GetTenantBySlugAsync(slug);
            if (tenant == null)
            {
                return NotFound(new { error = "Hotel not found" });
            }

            // Get hotel info for logo
            var hotelInfo = await _context.HotelInfos
                .Where(h => h.TenantId == tenant.Id)
                .Select(h => new { h.LogoUrl })
                .FirstOrDefaultAsync();

            var logoUrl = tenant.LogoUrl ?? hotelInfo?.LogoUrl ?? "/favicon.ico";
            var themePrimary = tenant.ThemePrimary ?? "#1976d2";
            var shortName = tenant.Name.Length > 12 ? tenant.Name.Substring(0, 12) : tenant.Name;

            // Build the start URL based on tenant subdomain
            var startUrl = $"https://{slug}.staybot.co.za/";

            var manifest = new
            {
                name = tenant.Name,
                short_name = shortName,
                description = $"Guest services for {tenant.Name}",
                start_url = startUrl,
                scope = "/",
                display = "standalone",
                orientation = "portrait-primary",
                theme_color = themePrimary,
                background_color = "#FFFFFF",
                categories = new[] { "travel", "hospitality", "lifestyle" },
                icons = new[]
                {
                    new { src = logoUrl, sizes = "72x72", type = "image/png", purpose = "any" },
                    new { src = logoUrl, sizes = "96x96", type = "image/png", purpose = "any" },
                    new { src = logoUrl, sizes = "128x128", type = "image/png", purpose = "any" },
                    new { src = logoUrl, sizes = "144x144", type = "image/png", purpose = "any" },
                    new { src = logoUrl, sizes = "152x152", type = "image/png", purpose = "any" },
                    new { src = logoUrl, sizes = "192x192", type = "image/png", purpose = "any maskable" },
                    new { src = logoUrl, sizes = "384x384", type = "image/png", purpose = "any" },
                    new { src = logoUrl, sizes = "512x512", type = "image/png", purpose = "any maskable" }
                },
                screenshots = new object[] { },
                related_applications = new object[] { },
                prefer_related_applications = false
            };

            return Ok(manifest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting manifest for slug {Slug}", slug);
            return StatusCode(500, new { error = "Error generating manifest" });
        }
    }

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
                    h.Latitude,
                    h.Longitude,
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
                city = hotelInfo?.City,
                latitude = hotelInfo?.Latitude,
                longitude = hotelInfo?.Longitude,
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

    /// <summary>
    /// Get WiFi credentials for the hotel
    /// </summary>
    [HttpGet("wifi")]
    public async Task<IActionResult> GetWifiCredentials(string slug)
    {
        try
        {
            var tenant = await GetTenantBySlugAsync(slug);
            if (tenant == null)
            {
                return NotFound(new { error = "Hotel not found" });
            }

            // Get WiFi credentials from HotelInfo
            var hotelInfo = await _context.HotelInfos
                .Where(h => h.TenantId == tenant.Id)
                .Select(h => new { h.WifiNetwork, h.WifiPassword })
                .FirstOrDefaultAsync();

            return Ok(new
            {
                network = hotelInfo?.WifiNetwork,
                password = hotelInfo?.WifiPassword
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting WiFi credentials for slug {Slug}", slug);
            return StatusCode(500, new { error = "Error retrieving WiFi credentials" });
        }
    }

    /// <summary>
    /// Get house rules (policies) for the hotel
    /// </summary>
    [HttpGet("house-rules")]
    public async Task<IActionResult> GetHouseRules(string slug)
    {
        try
        {
            var tenant = await GetTenantBySlugAsync(slug);
            if (tenant == null)
            {
                return NotFound(new { error = "Hotel not found" });
            }

            var hotelInfo = await _context.HotelInfos
                .Where(h => h.TenantId == tenant.Id)
                .Select(h => new
                {
                    smoking = h.SmokingPolicy,
                    pets = h.PetPolicy,
                    children = h.ChildPolicy,
                    cancellation = h.CancellationPolicy,
                    checkInTime = h.CheckInTime,
                    checkOutTime = h.CheckOutTime
                })
                .FirstOrDefaultAsync();

            if (hotelInfo == null)
            {
                return Ok(new
                {
                    smoking = (string?)null,
                    pets = (string?)null,
                    children = (string?)null,
                    cancellation = (string?)null,
                    checkInTime = (string?)null,
                    checkOutTime = (string?)null
                });
            }

            return Ok(hotelInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting house rules for slug {Slug}", slug);
            return StatusCode(500, new { error = "Error retrieving house rules" });
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

    /// <summary>
    /// Get featured services for "Enhance Your Stay" carousel
    /// </summary>
    [HttpGet("services/featured")]
    public async Task<IActionResult> GetFeaturedServices(string slug)
    {
        try
        {
            var tenant = await GetTenantBySlugAsync(slug);
            if (tenant == null)
            {
                return NotFound(new { error = "Hotel not found" });
            }

            var services = await _context.Services
                .Where(s => s.TenantId == tenant.Id && s.IsAvailable && s.IsFeatured)
                .OrderBy(s => s.DisplayOrder)
                .ThenBy(s => s.Priority)
                .Take(6) // Max 6 featured items for carousel
                .Select(s => new
                {
                    s.Id,
                    s.Name,
                    s.Description,
                    s.Category,
                    s.Icon,
                    imageUrl = s.FeaturedImageUrl ?? s.ImageUrl,
                    s.IsChargeable,
                    price = s.IsChargeable ? $"{s.Currency} {s.Price:F2}" : null,
                    priceAmount = s.Price,
                    s.Currency,
                    s.PricingUnit,
                    s.AvailableHours,
                    requiresBooking = s.RequiresAdvanceBooking,
                    advanceBookingHours = s.AdvanceBookingHours,
                    s.TimeSlots
                })
                .ToListAsync();

            return Ok(new { services });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting featured services for slug {Slug}", slug);
            return StatusCode(500, new { error = "Error retrieving featured services" });
        }
    }

    /// <summary>
    /// Get contextual service recommendations based on time of day
    /// </summary>
    [HttpGet("services/contextual")]
    public async Task<IActionResult> GetContextualServices(string slug, [FromQuery] string? timeSlot = null)
    {
        try
        {
            var tenant = await GetTenantBySlugAsync(slug);
            if (tenant == null)
            {
                return NotFound(new { error = "Hotel not found" });
            }

            // Determine time slot based on current hour if not provided
            if (string.IsNullOrEmpty(timeSlot))
            {
                var hour = DateTime.Now.Hour;
                timeSlot = hour switch
                {
                    >= 6 and < 10 => "morning",
                    >= 10 and < 14 => "midday",
                    >= 14 and < 18 => "afternoon",
                    >= 18 and < 22 => "evening",
                    _ => "night"
                };
            }

            var query = _context.Services
                .Where(s => s.TenantId == tenant.Id && s.IsAvailable && s.IsChargeable);

            // Filter by time slot if service has TimeSlots configured
            var services = await query
                .Where(s => s.TimeSlots == null || s.TimeSlots.Contains(timeSlot))
                .OrderBy(s => s.DisplayOrder)
                .ThenBy(s => s.Priority)
                .Take(4)
                .Select(s => new
                {
                    s.Id,
                    s.Name,
                    s.Description,
                    s.Category,
                    s.Icon,
                    imageUrl = s.FeaturedImageUrl ?? s.ImageUrl,
                    price = $"{s.Currency} {s.Price:F2}",
                    priceAmount = s.Price,
                    s.Currency,
                    s.PricingUnit,
                    s.AvailableHours,
                    s.TimeSlots
                })
                .ToListAsync();

            return Ok(new { timeSlot, services });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting contextual services for slug {Slug}", slug);
            return StatusCode(500, new { error = "Error retrieving contextual services" });
        }
    }

    /// <summary>
    /// Get weather-based service upsells based on current weather conditions
    /// </summary>
    [HttpGet("weather-upsells")]
    public async Task<IActionResult> GetWeatherUpsells(
        string slug,
        [FromQuery] int temperature,
        [FromQuery] int weatherCode)
    {
        try
        {
            var tenant = await GetTenantBySlugAsync(slug);
            if (tenant == null)
            {
                return NotFound(new { error = "Hotel not found" });
            }

            // Find matching weather upsell rules
            var matchingRules = await _context.WeatherUpsellRules
                .Where(r => r.TenantId == tenant.Id && r.IsActive)
                .OrderByDescending(r => r.Priority)
                .ToListAsync();

            // Filter rules that match the current conditions
            var applicableRule = matchingRules.FirstOrDefault(r =>
            {
                // Check temperature range
                if (r.MinTemperature.HasValue && temperature < r.MinTemperature.Value)
                    return false;
                if (r.MaxTemperature.HasValue && temperature > r.MaxTemperature.Value)
                    return false;

                // Check weather codes if specified
                if (!string.IsNullOrEmpty(r.WeatherCodes))
                {
                    try
                    {
                        var codes = System.Text.Json.JsonSerializer.Deserialize<int[]>(r.WeatherCodes);
                        if (codes != null && codes.Length > 0 && !codes.Contains(weatherCode))
                            return false;
                    }
                    catch
                    {
                        // If weather codes parsing fails, don't filter by it
                    }
                }

                return true;
            });

            if (applicableRule == null)
            {
                return Ok(new
                {
                    bannerText = (string?)null,
                    bannerIcon = (string?)null,
                    services = Array.Empty<object>()
                });
            }

            // Parse service IDs from the rule
            var serviceIds = new List<int>();
            try
            {
                serviceIds = System.Text.Json.JsonSerializer.Deserialize<List<int>>(applicableRule.ServiceIds) ?? new List<int>();
            }
            catch
            {
                _logger.LogWarning("Failed to parse ServiceIds for WeatherUpsellRule {RuleId}", applicableRule.Id);
            }

            // Get the services
            var services = await _context.Services
                .Where(s => s.TenantId == tenant.Id && s.IsAvailable && serviceIds.Contains(s.Id))
                .OrderBy(s => s.DisplayOrder)
                .Select(s => new
                {
                    s.Id,
                    s.Name,
                    s.Description,
                    s.Category,
                    s.Icon,
                    imageUrl = s.FeaturedImageUrl ?? s.ImageUrl,
                    s.IsChargeable,
                    price = s.IsChargeable ? $"{s.Currency} {s.Price:F2}" : null,
                    priceAmount = s.Price,
                    s.Currency,
                    s.PricingUnit,
                    s.AvailableHours,
                    requiresBooking = s.RequiresAdvanceBooking,
                    advanceBookingHours = s.AdvanceBookingHours
                })
                .ToListAsync();

            return Ok(new
            {
                bannerText = applicableRule.BannerText,
                bannerIcon = applicableRule.BannerIcon,
                weatherCondition = applicableRule.WeatherCondition,
                services
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting weather upsells for slug {Slug}", slug);
            return StatusCode(500, new { error = "Error retrieving weather-based recommendations" });
        }
    }

    /// <summary>
    /// Submit a service request from guest portal
    /// </summary>
    [HttpPost("services/request")]
    public async Task<IActionResult> SubmitServiceRequest(string slug, [FromBody] GuestServiceRequest request)
    {
        try
        {
            var tenant = await GetTenantBySlugAsync(slug);
            if (tenant == null)
            {
                return NotFound(new { error = "Hotel not found" });
            }

            if (request.ServiceId <= 0)
            {
                return BadRequest(new { error = "Service ID is required" });
            }

            if (string.IsNullOrEmpty(request.RoomNumber))
            {
                return BadRequest(new { error = "Room number is required" });
            }

            // Get the service details
            var service = await _context.Services
                .Where(s => s.Id == request.ServiceId && s.TenantId == tenant.Id && s.IsAvailable)
                .FirstOrDefaultAsync();

            if (service == null)
            {
                return NotFound(new { error = "Service not found" });
            }

            // Determine the appropriate department (valid: Housekeeping, Maintenance, FrontDesk, Concierge, FoodService, General)
            var department = service.Category?.ToLower() switch
            {
                "spa" or "wellness" => "Concierge",
                "restaurant" or "dining" or "room service" or "food" => "FoodService",
                "transport" or "shuttle" => "Concierge",
                "housekeeping" or "laundry" => "Housekeeping",
                "maintenance" => "Maintenance",
                _ => "Concierge"
            };

            // Create staff task for the service request
            var task = new StaffTask
            {
                TenantId = tenant.Id,
                Title = $"Service Request: {service.Name} - Room {request.RoomNumber}",
                Description = BuildServiceRequestDescription(service, request),
                TaskType = "concierge", // Valid: deliver_item, collect_item, maintenance, frontdesk, concierge, general
                Department = department,
                Priority = "Normal",
                Status = "Open",
                RoomNumber = request.RoomNumber,
                Notes = $"Submitted via Guest Portal at {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.StaffTasks.Add(task);
            await _context.SaveChangesAsync();

            // Track upsell conversion if source is provided (indicates upsell feature usage)
            if (!string.IsNullOrEmpty(request.Source))
            {
                var upsellMetric = new PortalUpsellMetric
                {
                    TenantId = tenant.Id,
                    ServiceId = service.Id,
                    ServiceName = service.Name,
                    ServicePrice = service.Price ?? 0,
                    ServiceCategory = service.Category,
                    Source = request.Source,
                    RoomNumber = request.RoomNumber,
                    EventType = "conversion",
                    Revenue = service.IsChargeable ? (service.Price ?? 0) : 0,
                    StaffTaskId = task.Id,
                    CreatedAt = DateTime.UtcNow
                };
                _context.PortalUpsellMetrics.Add(upsellMetric);
                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "Portal upsell tracked: Service {ServiceName} from source {Source} in Room {RoomNumber}",
                    service.Name, request.Source, request.RoomNumber);
            }

            _logger.LogInformation(
                "Service request created: Task {TaskId} for {ServiceName} in Room {RoomNumber} at Tenant {TenantId}",
                task.Id, service.Name, request.RoomNumber, tenant.Id);

            return Ok(new
            {
                success = true,
                message = "Your service request has been submitted. Our team will contact you shortly to confirm.",
                taskId = task.Id,
                serviceName = service.Name,
                estimatedResponse = service.RequiresAdvanceBooking ? "We will contact you to confirm availability" : "Within 30 minutes"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting service request for slug {Slug}", slug);
            return StatusCode(500, new { error = "Error submitting service request" });
        }
    }

    private static string BuildServiceRequestDescription(Service service, GuestServiceRequest request)
    {
        var lines = new List<string>
        {
            $"Service: {service.Name}",
            $"Category: {service.Category}"
        };

        if (service.IsChargeable && service.Price.HasValue)
        {
            lines.Add($"Price: {service.Currency} {service.Price:F2}{(string.IsNullOrEmpty(service.PricingUnit) ? "" : $" ({service.PricingUnit})")}");
        }

        lines.Add($"Preferred Time: {request.PreferredTime ?? "As soon as possible"}");

        if (!string.IsNullOrEmpty(request.SpecialRequests))
        {
            lines.Add($"Special Requests: {request.SpecialRequests}");
        }

        if (!string.IsNullOrEmpty(request.GuestName))
        {
            lines.Add($"Guest Name: {request.GuestName}");
        }

        if (!string.IsNullOrEmpty(request.Phone))
        {
            lines.Add($"Contact Phone: {request.Phone}");
        }

        return string.Join("\n", lines);
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
    /// Get list of lost item reports from other guests
    /// </summary>
    [HttpGet("lost-reports")]
    public async Task<IActionResult> GetLostReports(string slug, [FromQuery] string? category = null)
    {
        try
        {
            var tenant = await GetTenantBySlugAsync(slug);
            if (tenant == null)
            {
                return NotFound(new { error = "Hotel not found" });
            }

            var query = _context.LostItems
                .Where(l => l.TenantId == tenant.Id);

            if (!string.IsNullOrEmpty(category))
            {
                query = query.Where(l => l.Category == category);
            }

            var items = await query
                .OrderByDescending(l => l.ReportedAt)
                .Select(l => new
                {
                    l.Id,
                    l.ItemName,
                    category = l.Category ?? "Other",
                    description = l.Description != null ? (l.Description.Length > 100 ? l.Description.Substring(0, 100) + "..." : l.Description) : null,
                    l.Color,
                    l.Brand,
                    l.LocationLost,
                    reportedDate = l.ReportedAt.ToString("dd MMM yyyy"),
                    status = l.Status == "Matched" || l.Status == "Found" || l.Status == "Claimed" || l.Status == "Returned" ? "found" : "searching"
                })
                .Take(50)
                .ToListAsync();

            return Ok(new { items });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting lost reports for slug {Slug}", slug);
            return StatusCode(500, new { error = "Error retrieving lost reports" });
        }
    }

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

            // Store in GuestRatings table for analytics reports
            var guestRating = new GuestRating
            {
                TenantId = tenant.Id,
                Rating = request.Rating,
                Comment = request.Comment,
                GuestName = request.GuestName,
                GuestPhone = request.Phone,
                RoomNumber = request.RoomNumber,
                RatingType = "Stay",
                CollectionMethod = "Manual", // Guest Portal submission
                CreatedAt = DateTime.UtcNow
            };

            _context.GuestRatings.Add(guestRating);
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

    #region Housekeeping Preferences

    /// <summary>
    /// Get housekeeping preferences for a room
    /// </summary>
    [HttpGet("housekeeping-preferences")]
    public async Task<IActionResult> GetHousekeepingPreferences(string slug, [FromQuery] string? roomNumber = null)
    {
        try
        {
            var tenant = await GetTenantBySlugAsync(slug);
            if (tenant == null)
            {
                return NotFound(new { error = "Hotel not found" });
            }

            // Return empty list for now - in the future we could query actual preferences
            // For now, preferences are stored as staff tasks
            return Ok(new List<object>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting housekeeping preferences for slug {Slug}", slug);
            return StatusCode(500, new { error = "Error retrieving preferences" });
        }
    }

    /// <summary>
    /// Submit housekeeping preferences from guest portal
    /// </summary>
    [HttpPost("housekeeping-preferences")]
    public async Task<IActionResult> SubmitHousekeepingPreference(string slug, [FromBody] GuestHousekeepingPreferenceRequest request)
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

            if (string.IsNullOrEmpty(request.PreferenceType))
            {
                return BadRequest(new { error = "Preference type is required" });
            }

            // Build description based on preference type
            var description = BuildPreferenceDescription(request);
            var title = GetPreferenceTitle(request.PreferenceType, request.RoomNumber);

            // Create staff task for housekeeping team
            var task = new StaffTask
            {
                TenantId = tenant.Id,
                Title = title,
                Description = description,
                TaskType = "general",
                Department = "Housekeeping",
                Priority = "Normal",
                Status = "Open",
                RoomNumber = request.RoomNumber,
                Notes = $"Guest preference submitted via Guest Portal at {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.StaffTasks.Add(task);
            await _context.SaveChangesAsync();

            // Send SignalR notification to housekeeping staff
            await _staffTaskHub.Clients.Group($"Tenant_{tenant.Id}")
                .SendAsync("PreferenceCreated", new
                {
                    taskId = task.Id,
                    roomNumber = request.RoomNumber,
                    preferenceType = request.PreferenceType,
                    title = title,
                    description = description,
                    department = "Housekeeping",
                    createdAt = task.CreatedAt
                });

            _logger.LogInformation(
                "Housekeeping preference created: Task {TaskId} for Room {RoomNumber} at Tenant {TenantId}",
                task.Id, request.RoomNumber, tenant.Id);

            return Ok(new
            {
                id = task.Id,
                preferenceType = request.PreferenceType,
                preferenceValue = request.PreferenceValue,
                status = "Active",
                createdAt = DateTime.UtcNow,
                updatedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting housekeeping preference for slug {Slug}", slug);
            return StatusCode(500, new { error = "Error submitting preference" });
        }
    }

    private static string GetPreferenceTitle(string preferenceType, string roomNumber)
    {
        return preferenceType switch
        {
            "aircon_after_cleaning" => $"Room {roomNumber}: Aircon Preference Set",
            "linen_change_frequency" => $"Room {roomNumber}: Linen Change Preference",
            "towel_change_frequency" => $"Room {roomNumber}: Towel Change Preference",
            "dnd_schedule" => $"Room {roomNumber}: Do Not Disturb Request",
            _ => $"Room {roomNumber}: Housekeeping Preference"
        };
    }

    private static string BuildPreferenceDescription(GuestHousekeepingPreferenceRequest request)
    {
        var lines = new List<string> { $"Room: {request.RoomNumber}" };

        switch (request.PreferenceType)
        {
            case "aircon_after_cleaning":
                var airconEnabled = request.PreferenceValue?.TryGetProperty("enabled", out var enabled) == true && enabled.GetBoolean();
                lines.Add($"Preference: Keep aircon {(airconEnabled ? "ON" : "OFF")} after cleaning");
                break;

            case "linen_change_frequency":
                var dailyLinen = request.PreferenceValue?.TryGetProperty("daily", out var linenDaily) == true && linenDaily.GetBoolean();
                lines.Add($"Preference: Linen change {(dailyLinen ? "daily" : "every 2-3 days")}");
                break;

            case "towel_change_frequency":
                var dailyTowel = request.PreferenceValue?.TryGetProperty("daily", out var towelDaily) == true && towelDaily.GetBoolean();
                lines.Add($"Preference: Towel change {(dailyTowel ? "daily" : "every 2-3 days")}");
                break;

            case "dnd_schedule":
                var until = request.PreferenceValue?.TryGetProperty("until", out var untilTime) == true ? untilTime.GetString() : "14:00";
                lines.Add($"Do Not Disturb until: {until}");
                break;

            default:
                lines.Add($"Preference Type: {request.PreferenceType}");
                break;
        }

        if (!string.IsNullOrEmpty(request.Notes))
        {
            lines.Add($"Notes: {request.Notes}");
        }

        return string.Join("\n", lines);
    }

    #endregion

    #region Request Items

    /// <summary>
    /// Get available request items from the database
    /// </summary>
    [HttpGet("request-items")]
    public async Task<IActionResult> GetAvailableRequestItems(string slug, [FromQuery] string? category = null, [FromQuery] string? department = null)
    {
        try
        {
            var tenant = await GetTenantBySlugAsync(slug);
            if (tenant == null)
            {
                return NotFound(new { error = "Hotel not found" });
            }

            var query = _context.RequestItems
                .Where(r => r.TenantId == tenant.Id && r.IsAvailable);

            // Filter by category if specified (e.g., "RoomAmenities", "Housekeeping")
            if (!string.IsNullOrEmpty(category))
            {
                query = query.Where(r => r.Category == category);
            }

            // Filter by department if specified (e.g., "Housekeeping")
            if (!string.IsNullOrEmpty(department))
            {
                query = query.Where(r => r.Department == department);
            }

            var items = await query
                .OrderBy(r => r.DisplayOrder)
                .ThenBy(r => r.Name)
                .Select(r => new
                {
                    r.Id,
                    r.Name,
                    r.Category,
                    r.Description,
                    r.RequiresQuantity,
                    r.DefaultQuantityLimit,
                    r.EstimatedTime,
                    icon = GetRequestItemIcon(r.Name)
                })
                .ToListAsync();

            return Ok(new { items });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting request items for slug {Slug}", slug);
            return StatusCode(500, new { error = "Error retrieving request items" });
        }
    }

    private static string GetRequestItemIcon(string itemName)
    {
        var name = itemName.ToLower();
        return name switch
        {
            var n when n.Contains("iron") => "bi-lightning",
            var n when n.Contains("hair dryer") || n.Contains("dryer") => "bi-wind",
            var n when n.Contains("hanger") => "bi-handbag",
            var n when n.Contains("bathrobe") || n.Contains("robe") => "bi-person-standing",
            var n when n.Contains("slipper") => "bi-box",
            var n when n.Contains("towel") => "bi-droplet",
            var n when n.Contains("blanket") => "bi-cloud",
            var n when n.Contains("pillow") => "bi-moon",
            var n when n.Contains("laundry") => "bi-basket",
            var n when n.Contains("coffee") || n.Contains("tea") => "bi-cup-hot",
            var n when n.Contains("ice") => "bi-snow",
            var n when n.Contains("mini bar") => "bi-cup-straw",
            _ => "bi-box-seam"
        };
    }

    /// <summary>
    /// Submit an item request from guest portal (creates a StaffTask)
    /// </summary>
    [HttpPost("item-request")]
    public async Task<IActionResult> SubmitItemRequest(string slug, [FromBody] GuestItemRequestDto request)
    {
        try
        {
            var tenant = await GetTenantBySlugAsync(slug);
            if (tenant == null)
            {
                return NotFound(new { error = "Hotel not found" });
            }

            if (request.RequestItemId <= 0)
            {
                return BadRequest(new { error = "Request item ID is required" });
            }

            // Get the RequestItem details
            var requestItem = await _context.RequestItems
                .FirstOrDefaultAsync(r => r.Id == request.RequestItemId && r.TenantId == tenant.Id);

            if (requestItem == null || !requestItem.IsAvailable)
            {
                return BadRequest(new { error = "Item not available" });
            }

            // Validate quantity if required
            var quantity = request.Quantity > 0 ? request.Quantity : 1;
            if (requestItem.RequiresQuantity && quantity > requestItem.DefaultQuantityLimit)
            {
                quantity = requestItem.DefaultQuantityLimit;
            }

            // Create StaffTask for the item request
            var task = new StaffTask
            {
                TenantId = tenant.Id,
                RequestItemId = requestItem.Id,
                Title = $"{requestItem.Name} - Room {request.RoomNumber}",
                Description = string.IsNullOrEmpty(request.Notes)
                    ? $"Guest requested: {requestItem.Name}{(quantity > 1 ? $" (Qty: {quantity})" : "")}"
                    : $"Guest requested: {requestItem.Name}{(quantity > 1 ? $" (Qty: {quantity})" : "")}\n\nNotes: {request.Notes}",
                TaskType = "deliver_item",
                Department = requestItem.Department ?? "Housekeeping",
                RoomNumber = request.RoomNumber,
                Quantity = quantity,
                Priority = requestItem.IsUrgent ? "High" : "Normal",
                Status = "Open",
                Notes = $"Submitted via Guest Portal at {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.StaffTasks.Add(task);
            await _context.SaveChangesAsync();

            // Send SignalR notification to staff
            await _staffTaskHub.Clients.Group($"Tenant_{tenant.Id}")
                .SendAsync("TaskCreated", new
                {
                    taskId = task.Id,
                    title = task.Title,
                    roomNumber = task.RoomNumber,
                    department = task.Department,
                    itemName = requestItem.Name,
                    quantity = quantity,
                    createdAt = task.CreatedAt
                });

            _logger.LogInformation(
                "Item request created: Task {TaskId} for {ItemName} in Room {RoomNumber} at Tenant {TenantId}",
                task.Id, requestItem.Name, request.RoomNumber, tenant.Id);

            return Ok(new
            {
                success = true,
                message = $"Your request for {requestItem.Name} has been sent to housekeeping",
                taskId = task.Id,
                itemName = requestItem.Name,
                estimatedTime = requestItem.EstimatedTime ?? 15
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting item request for slug {Slug}", slug);
            return StatusCode(500, new { error = "Error submitting item request" });
        }
    }

    [HttpPost("custom-request")]
    public async Task<IActionResult> SubmitCustomRequest(string slug, [FromBody] CustomRequestDto request)
    {
        try
        {
            var tenant = await GetTenantBySlugAsync(slug);
            if (tenant == null)
            {
                return NotFound(new { error = "Hotel not found" });
            }

            // Validate required fields
            if (string.IsNullOrWhiteSpace(request.Description))
            {
                return BadRequest(new { error = "Request description is required" });
            }

            if (string.IsNullOrWhiteSpace(request.RoomNumber))
            {
                return BadRequest(new { error = "Room number is required" });
            }

            // Create StaffTask for custom request
            var task = new StaffTask
            {
                TenantId = tenant.Id,
                Title = $"Custom Request - Room {request.RoomNumber}",
                Description = $"Guest requested:\n{request.Description}"
                            + (string.IsNullOrEmpty(request.Timing) ? "" : $"\n\nTiming: {GetTimingDescription(request.Timing)}"),
                TaskType = "custom_request",
                Department = request.Department ?? "Concierge", // Default to concierge
                RoomNumber = request.RoomNumber,
                Priority = "Normal",
                Status = "Open",
                Notes = $"Submitted via Guest Portal at {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.StaffTasks.Add(task);
            await _context.SaveChangesAsync();

            // Track metric
            var metric = new PortalUpsellMetric
            {
                TenantId = tenant.Id,
                ServiceType = "Custom Request",
                RoomNumber = request.RoomNumber,
                ActionTaken = "requested",
                Timestamp = DateTime.UtcNow,
                Source = request.Source ?? "prepare_page"
            };

            _context.PortalUpsellMetrics.Add(metric);
            await _context.SaveChangesAsync();

            // Send SignalR notification to staff
            await _staffTaskHub.Clients.Group($"Tenant_{tenant.Id}")
                .SendAsync("TaskCreated", new
                {
                    taskId = task.Id,
                    title = task.Title,
                    roomNumber = task.RoomNumber,
                    department = task.Department,
                    taskType = task.TaskType,
                    createdAt = task.CreatedAt
                });

            _logger.LogInformation(
                "Custom request created: Task {TaskId} in Room {RoomNumber} at Tenant {TenantId}",
                task.Id, request.RoomNumber, tenant.Id);

            return Ok(new
            {
                success = true,
                message = "Your custom request has been submitted successfully",
                taskId = task.Id
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting custom request for slug {Slug}", slug);
            return StatusCode(500, new { error = "Error submitting custom request" });
        }
    }

    private string GetTimingDescription(string timing)
    {
        return timing switch
        {
            "before-arrival" => "Before arrival",
            "check-in" => "Upon check-in",
            "later" => "Later during stay",
            "asap" => "As soon as possible",
            _ => "No specific time"
        };
    }

    #endregion

    #region Guest Journey - Pre-Arrival & Feedback

    /// <summary>
    /// Get booking info for pre-arrival prepare page
    /// Used when guest clicks prepare link in WhatsApp message
    /// </summary>
    [HttpGet("booking/{bookingId:int}")]
    public async Task<IActionResult> GetBookingInfo(string slug, int bookingId)
    {
        try
        {
            var tenant = await GetTenantBySlugAsync(slug);
            if (tenant == null)
            {
                return NotFound(new { error = "Hotel not found" });
            }

            var bookingData = await _context.Bookings
                .Where(b => b.Id == bookingId && b.TenantId == tenant.Id)
                .Select(b => new
                {
                    b.Id,
                    b.GuestName,
                    b.RoomNumber,
                    b.CheckinDate,
                    b.CheckoutDate,
                    b.Status
                })
                .FirstOrDefaultAsync();

            if (bookingData == null)
            {
                return NotFound(new { error = "Booking not found" });
            }

            // Compute first name in memory (not in SQL)
            var guestFirstName = bookingData.GuestName.Contains(' ')
                ? bookingData.GuestName.Split(' ')[0]
                : bookingData.GuestName;

            return Ok(new
            {
                bookingData.Id,
                guestFirstName,
                bookingData.GuestName,
                bookingData.RoomNumber,
                bookingData.CheckinDate,
                bookingData.CheckoutDate,
                bookingData.Status,
                hasCheckedIn = bookingData.Status == "CheckedIn" || bookingData.Status == "InHouse"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting booking {BookingId} for slug {Slug}", bookingId, slug);
            return StatusCode(500, new { error = "Error retrieving booking information" });
        }
    }

    /// <summary>
    /// Get available items and services for pre-arrival upsells (prepare page)
    /// This combines RequestItems and Services marked for pre-arrival
    /// </summary>
    [HttpGet("prepare-items")]
    public async Task<IActionResult> GetPrepareItems(string slug)
    {
        try
        {
            var tenant = await GetTenantBySlugAsync(slug);
            if (tenant == null)
            {
                return NotFound(new { error = "Hotel not found" });
            }

            // Get RequestItems that are available for pre-arrival (Category = "PreArrival" or "Upsell")
            var requestItems = await _context.RequestItems
                .Where(r => r.TenantId == tenant.Id && r.IsAvailable &&
                           (r.Category == "PreArrival" || r.Category == "Upsell" || r.Category == "RoomAmenities"))
                .OrderBy(r => r.DisplayOrder)
                .ThenBy(r => r.Name)
                .Select(r => new
                {
                    id = r.Id,
                    type = "item",
                    name = r.Name,
                    description = r.Description,
                    category = r.Category,
                    price = (decimal?)null, // Most request items are free
                    isChargeable = false,
                    icon = GetRequestItemIcon(r.Name),
                    estimatedTime = r.EstimatedTime ?? 15
                })
                .ToListAsync();

            // Get Services available for pre-arrival (chargeable services that can be pre-booked)
            var services = await _context.Services
                .Where(s => s.TenantId == tenant.Id && s.IsAvailable && s.IsChargeable)
                .OrderBy(s => s.DisplayOrder)
                .ThenBy(s => s.Name)
                .Select(s => new
                {
                    id = s.Id,
                    type = "service",
                    name = s.Name,
                    description = s.Description,
                    category = s.Category,
                    price = s.Price,
                    isChargeable = s.IsChargeable,
                    icon = s.Icon ?? GetServiceCategoryIcon(s.Category ?? ""),
                    imageUrl = s.FeaturedImageUrl ?? s.ImageUrl,
                    currency = s.Currency,
                    pricingUnit = s.PricingUnit,
                    requiresBooking = s.RequiresAdvanceBooking
                })
                .ToListAsync();

            return Ok(new
            {
                items = requestItems,
                services = services
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting prepare items for slug {Slug}", slug);
            return StatusCode(500, new { error = "Error retrieving prepare items" });
        }
    }

    /// <summary>
    /// Get feedback categories for the feedback page (used in Welcome Settled journey)
    /// </summary>
    [HttpGet("feedback-categories")]
    public async Task<IActionResult> GetFeedbackCategories(string slug)
    {
        try
        {
            var tenant = await GetTenantBySlugAsync(slug);
            if (tenant == null)
            {
                return NotFound(new { error = "Hotel not found" });
            }

            // Get departments from database for issue categories
            // Fall back to default categories if none configured
            var defaultCategories = new[]
            {
                new { id = "room_issue", name = "Room Issue", icon = "bi-house-door", description = "Problem with the room or amenities" },
                new { id = "noise", name = "Noise Complaint", icon = "bi-volume-up", description = "Noise disturbance" },
                new { id = "cleanliness", name = "Cleanliness", icon = "bi-brush", description = "Room needs cleaning" },
                new { id = "service", name = "Service Issue", icon = "bi-person-badge", description = "Issue with staff or service" },
                new { id = "other", name = "Other", icon = "bi-three-dots", description = "Other feedback" }
            };

            return Ok(new { categories = defaultCategories });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting feedback categories for slug {Slug}", slug);
            return StatusCode(500, new { error = "Error retrieving feedback categories" });
        }
    }

    /// <summary>
    /// Submit quick feedback from the Welcome Settled journey
    /// Creates a rating and optionally a staff task if there's an issue
    /// </summary>
    [HttpPost("feedback")]
    public async Task<IActionResult> SubmitFeedback(string slug, [FromBody] GuestQuickFeedbackRequest request)
    {
        try
        {
            var tenant = await GetTenantBySlugAsync(slug);
            if (tenant == null)
            {
                return NotFound(new { error = "Hotel not found" });
            }

            // Validate rating
            if (request.Rating < 1 || request.Rating > 5)
            {
                return BadRequest(new { error = "Rating must be between 1 and 5" });
            }

            // Store the rating
            var guestRating = new GuestRating
            {
                TenantId = tenant.Id,
                Rating = request.Rating,
                Comment = request.Comment,
                RoomNumber = request.RoomNumber,
                RatingType = "WelcomeSettled", // Mark as from Welcome Settled journey
                CollectionMethod = "Journey", // Guest Journey automated collection
                CreatedAt = DateTime.UtcNow
            };

            _context.GuestRatings.Add(guestRating);

            // If there's an issue category, create a staff task
            if (!string.IsNullOrEmpty(request.IssueCategory))
            {
                var priority = request.Rating <= 2 ? "High" : "Normal";
                var department = request.IssueCategory switch
                {
                    "room_issue" => "Maintenance",
                    "cleanliness" => "Housekeeping",
                    "noise" => "FrontDesk",
                    "service" => "FrontDesk",
                    _ => "General"
                };

                var task = new StaffTask
                {
                    TenantId = tenant.Id,
                    Title = $"Guest Feedback Issue - Room {request.RoomNumber}",
                    Description = $"Issue Category: {request.IssueCategory}\nRating: {request.Rating}/5\n\n{request.Comment ?? "No additional details provided."}",
                    TaskType = "general",
                    Department = department,
                    Priority = priority,
                    Status = "Open",
                    RoomNumber = request.RoomNumber,
                    Notes = $"From Welcome Settled journey at {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.StaffTasks.Add(task);

                // Send SignalR notification to staff
                await _staffTaskHub.Clients.Group($"Tenant_{tenant.Id}")
                    .SendAsync("TaskCreated", new
                    {
                        taskId = task.Id,
                        title = task.Title,
                        roomNumber = task.RoomNumber,
                        department = task.Department,
                        priority = task.Priority,
                        createdAt = task.CreatedAt
                    });
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Guest feedback submitted via Welcome Settled: {Rating}/5 for Room {RoomNumber} at Tenant {TenantId}",
                request.Rating, request.RoomNumber, tenant.Id);

            var responseMessage = request.Rating >= 4
                ? "Thank you for your positive feedback!"
                : "Thank you for letting us know. Our team will address this shortly.";

            return Ok(new
            {
                success = true,
                message = responseMessage
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting feedback for slug {Slug}", slug);
            return StatusCode(500, new { error = "Error submitting feedback" });
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

    #region Push Notifications

    /// <summary>
    /// Subscribe guest to push notifications
    /// </summary>
    [HttpPost("push/subscribe")]
    public async Task<IActionResult> SubscribeToPush(string slug, [FromBody] GuestPushSubscribeRequest request)
    {
        try
        {
            var tenant = await GetTenantBySlugAsync(slug);
            if (tenant == null)
            {
                return NotFound(new { error = "Hotel not found" });
            }

            // Validate and resolve room number
            var validation = await _roomValidationService.ValidateAndResolveRoom(
                tenant.Id,
                request.Phone,
                request.RoomNumber);

            if (!validation.IsValid)
            {
                return BadRequest(new
                {
                    success = false,
                    error = validation.ErrorMessage,
                    requiresRoomNumber = string.IsNullOrWhiteSpace(request.RoomNumber)
                });
            }

            // Check if subscription already exists
            var existingSubscription = await _context.PushSubscriptions
                .FirstOrDefaultAsync(s =>
                    s.Endpoint == request.Endpoint &&
                    s.TenantId == tenant.Id &&
                    s.IsGuest);

            if (existingSubscription != null)
            {
                // Update existing subscription
                existingSubscription.P256dhKey = request.Keys?.P256dh ?? string.Empty;
                existingSubscription.AuthKey = request.Keys?.Auth ?? string.Empty;
                existingSubscription.DeviceInfo = request.DeviceInfo;
                existingSubscription.GuestPhone = request.Phone;
                existingSubscription.RoomNumber = validation.RoomNumber;
                existingSubscription.BookingId = validation.BookingId;
                existingSubscription.IsVerified = validation.IsVerified;
                existingSubscription.LastUsedAt = DateTime.UtcNow;
                existingSubscription.IsActive = true;

                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "Updated guest push subscription for tenant {TenantId}, room {RoomNumber}, verified: {IsVerified}",
                    tenant.Id, validation.RoomNumber, validation.IsVerified);

                return Ok(new
                {
                    success = true,
                    message = "Subscription updated successfully",
                    roomNumber = validation.RoomNumber,
                    isVerified = validation.IsVerified,
                    guestName = validation.GuestName
                });
            }

            // Create new subscription
            var subscription = new PushSubscription
            {
                TenantId = tenant.Id,
                Endpoint = request.Endpoint,
                P256dhKey = request.Keys?.P256dh ?? string.Empty,
                AuthKey = request.Keys?.Auth ?? string.Empty,
                DeviceInfo = request.DeviceInfo,
                GuestPhone = request.Phone,
                RoomNumber = validation.RoomNumber,
                BookingId = validation.BookingId,
                IsVerified = validation.IsVerified,
                IsGuest = true,
                CreatedAt = DateTime.UtcNow,
                LastUsedAt = DateTime.UtcNow,
                IsActive = true
            };

            _context.PushSubscriptions.Add(subscription);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Created guest push subscription for tenant {TenantId}, room {RoomNumber}, verified: {IsVerified}",
                tenant.Id, validation.RoomNumber, validation.IsVerified);

            return Ok(new
            {
                success = true,
                message = "Subscribed to notifications successfully",
                roomNumber = validation.RoomNumber,
                isVerified = validation.IsVerified,
                guestName = validation.GuestName
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error subscribing guest to push notifications for slug {Slug}", slug);
            return StatusCode(500, new { error = "Failed to subscribe to notifications" });
        }
    }

    /// <summary>
    /// Unsubscribe guest from push notifications
    /// </summary>
    [HttpPost("push/unsubscribe")]
    public async Task<IActionResult> UnsubscribeFromPush(string slug, [FromBody] GuestPushUnsubscribeRequest request)
    {
        try
        {
            var tenant = await GetTenantBySlugAsync(slug);
            if (tenant == null)
            {
                return NotFound(new { error = "Hotel not found" });
            }

            var subscription = await _context.PushSubscriptions
                .FirstOrDefaultAsync(s =>
                    s.Endpoint == request.Endpoint &&
                    s.TenantId == tenant.Id &&
                    s.IsGuest);

            if (subscription == null)
            {
                return NotFound(new { error = "Subscription not found" });
            }

            subscription.IsActive = false;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Guest unsubscribed from push notifications for tenant {TenantId}", tenant.Id);

            return Ok(new { success = true, message = "Unsubscribed successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unsubscribing guest from push notifications for slug {Slug}", slug);
            return StatusCode(500, new { error = "Failed to unsubscribe" });
        }
    }

    /// <summary>
    /// Validate a room number for this property
    /// </summary>
    [HttpGet("rooms/validate/{roomNumber}")]
    public async Task<IActionResult> ValidateRoom(string slug, string roomNumber)
    {
        try
        {
            var tenant = await GetTenantBySlugAsync(slug);
            if (tenant == null)
            {
                return NotFound(new { error = "Hotel not found" });
            }

            var isValid = await _roomValidationService.IsValidRoom(tenant.Id, roomNumber);
            var validRooms = await _roomValidationService.GetValidRooms(tenant.Id);

            // If no valid rooms configured, accept any room
            if (!validRooms.Any())
            {
                return Ok(new {
                    valid = true,
                    roomNumber = roomNumber.Trim(),
                    message = "Room accepted"
                });
            }

            if (isValid)
            {
                return Ok(new {
                    valid = true,
                    roomNumber = roomNumber.Trim(),
                    message = "Valid room number"
                });
            }

            return Ok(new {
                valid = false,
                error = $"Room {roomNumber} is not a valid room at this property"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating room {Room} for slug {Slug}", roomNumber, slug);
            return StatusCode(500, new { error = "Failed to validate room" });
        }
    }

    /// <summary>
    /// Test endpoint: Schedule proactive messages for a booking (for development/testing)
    /// </summary>
    [HttpPost("test/schedule-booking/{bookingId}")]
    public async Task<IActionResult> TestScheduleBooking(string slug, int bookingId)
    {
        try
        {
            var tenant = await GetTenantBySlugAsync(slug);
            if (tenant == null)
            {
                return NotFound(new { error = "Hotel not found" });
            }

            var booking = await _context.Bookings
                .FirstOrDefaultAsync(b => b.Id == bookingId && b.TenantId == tenant.Id);

            if (booking == null)
            {
                return NotFound(new { error = "Booking not found" });
            }

            await _proactiveMessageService.ScheduleMessagesForBookingAsync(tenant.Id, booking);

            // Return the scheduled messages
            var scheduledMessages = await _context.ScheduledMessages
                .Where(m => m.BookingId == bookingId)
                .OrderBy(m => m.ScheduledFor)
                .Select(m => new
                {
                    m.Id,
                    MessageType = m.MessageType.ToString(),
                    m.ScheduledFor,
                    Status = m.Status.ToString(),
                    m.Content
                })
                .ToListAsync();

            return Ok(new
            {
                message = $"Scheduled {scheduledMessages.Count} messages for booking {bookingId}",
                booking = new
                {
                    booking.Id,
                    booking.GuestName,
                    booking.RoomNumber,
                    booking.CheckinDate,
                    booking.CheckoutDate
                },
                scheduledMessages
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scheduling messages for booking {BookingId}", bookingId);
            return StatusCode(500, new { error = "Failed to schedule messages", details = ex.Message });
        }
    }

    /// <summary>
    /// Test endpoint: Simulate guest check-in to trigger WelcomeSettled message (for development/testing)
    /// </summary>
    [HttpPost("test/checkin/{bookingId}")]
    public async Task<IActionResult> TestCheckin(string slug, int bookingId)
    {
        try
        {
            var tenant = await GetTenantBySlugAsync(slug);
            if (tenant == null)
            {
                return NotFound(new { error = "Hotel not found" });
            }

            var booking = await _context.Bookings
                .FirstOrDefaultAsync(b => b.Id == bookingId && b.TenantId == tenant.Id);

            if (booking == null)
            {
                return NotFound(new { error = "Booking not found" });
            }

            var previousStatus = booking.Status;

            // Update booking status to CheckedIn
            booking.Status = "CheckedIn";
            booking.CheckInDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // Schedule WelcomeSettled message (3 hours after check-in by default)
            await _proactiveMessageService.ScheduleWelcomeSettledAsync(booking);

            // Get all scheduled messages for this booking
            var scheduledMessages = await _context.ScheduledMessages
                .Where(m => m.BookingId == bookingId)
                .OrderBy(m => m.ScheduledFor)
                .Select(m => new
                {
                    m.Id,
                    MessageType = m.MessageType.ToString(),
                    m.ScheduledFor,
                    Status = m.Status.ToString(),
                    m.Content
                })
                .ToListAsync();

            return Ok(new
            {
                message = $"Guest checked in! WelcomeSettled message scheduled.",
                booking = new
                {
                    booking.Id,
                    booking.GuestName,
                    booking.RoomNumber,
                    PreviousStatus = previousStatus,
                    NewStatus = booking.Status,
                    ActualCheckinTime = booking.CheckInDate
                },
                scheduledMessages
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error simulating check-in for booking {BookingId}", bookingId);
            return StatusCode(500, new { error = "Failed to simulate check-in", details = ex.Message });
        }
    }

    /// <summary>
    /// Test endpoint: Send a test SMS via ClickaTell (for development/testing)
    /// </summary>
    [HttpPost("test/sms-message")]
    public async Task<IActionResult> TestSmsMessage(
        string slug,
        [FromQuery] string phone,
        [FromQuery] string message)
    {
        try
        {
            var tenant = await GetTenantBySlugAsync(slug);
            if (tenant == null)
            {
                return NotFound(new { error = "Hotel not found" });
            }

            _logger.LogInformation("Sending test SMS to {Phone} for tenant {TenantId}", phone, tenant.Id);

            // Check API key configuration
            var apiKey = _configuration["ClickaTell:ApiKey"];
            var hasApiKey = !string.IsNullOrEmpty(apiKey);
            _logger.LogInformation("API Key configured: {HasApiKey}, Length: {Length}", hasApiKey, apiKey?.Length ?? 0);

            var sent = await _smsService.SendMessageAsync(phone, message);

            return Ok(new
            {
                success = sent,
                phone,
                message,
                apiKeyConfigured = hasApiKey,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending test SMS to {Phone}", phone);
            return StatusCode(500, new { error = "Failed to send SMS", details = ex.Message, stackTrace = ex.StackTrace });
        }
    }

    /// <summary>
    /// Test endpoint: Direct ClickaTell API call (for debugging)
    /// </summary>
    [HttpPost("test/sms-direct")]
    public async Task<IActionResult> TestSmsDirect(
        string slug,
        [FromQuery] string phone,
        [FromQuery] string message)
    {
        try
        {
            var apiKey = _configuration["ClickaTell:ApiKey"];
            _logger.LogInformation("Direct ClickaTell test - API Key configured: {HasKey}", !string.IsNullOrEmpty(apiKey));

            using var httpClient = new HttpClient();
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://platform.clickatell.com/messages");

            var requestBody = new { content = message, to = new[] { phone } };
            var json = System.Text.Json.JsonSerializer.Serialize(requestBody);

            request.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            request.Headers.TryAddWithoutValidation("Authorization", apiKey);
            request.Headers.TryAddWithoutValidation("User-Agent", "StayBot/1.0");
            request.Headers.TryAddWithoutValidation("Accept", "application/json");

            _logger.LogInformation("Sending direct HTTP POST to ClickaTell");

            var response = await httpClient.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("Direct ClickaTell response: Status={Status}, Body={Body}",
                response.StatusCode, responseBody);

            return Ok(new
            {
                success = response.IsSuccessStatusCode,
                statusCode = (int)response.StatusCode,
                responseBody,
                phone,
                message,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in direct ClickaTell test");
            return StatusCode(500, new { error = "Failed to send direct SMS", details = ex.Message, stackTrace = ex.StackTrace });
        }
    }

    /// <summary>
    /// Test endpoint: Manually trigger processing of due scheduled messages (for development/testing)
    /// </summary>
    [HttpPost("test/process-messages")]
    public async Task<IActionResult> TestProcessMessages(string slug)
    {
        try
        {
            var tenant = await GetTenantBySlugAsync(slug);
            if (tenant == null)
            {
                return NotFound(new { error = "Hotel not found" });
            }

            _logger.LogInformation("Manually triggering message processing for tenant {TenantId}", tenant.Id);

            // Process all due messages
            await _proactiveMessageService.ProcessDueMessagesAsync();

            // Get recently sent messages for this tenant
            var recentMessages = await _context.ScheduledMessages
                .Where(m => m.TenantId == tenant.Id &&
                           m.SentAt.HasValue &&
                           m.SentAt.Value > DateTime.UtcNow.AddMinutes(-5))
                .OrderByDescending(m => m.SentAt)
                .Select(m => new
                {
                    m.Id,
                    MessageType = m.MessageType.ToString(),
                    m.Phone,
                    m.ScheduledFor,
                    m.SentAt,
                    Status = m.Status.ToString(),
                    m.Content
                })
                .ToListAsync();

            return Ok(new
            {
                message = "Processed due messages",
                sentCount = recentMessages.Count,
                sentMessages = recentMessages
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing messages");
            return StatusCode(500, new { error = "Failed to process messages", details = ex.Message });
        }
    }

    #endregion
}

#region Request DTOs

public class GuestPushSubscribeRequest
{
    [Required]
    public string Endpoint { get; set; } = string.Empty;

    public GuestPushKeys? Keys { get; set; }

    public string? DeviceInfo { get; set; }

    /// <summary>
    /// Phone number for booking lookup (optional - can use room number instead).
    /// </summary>
    public string? Phone { get; set; }

    /// <summary>
    /// Room number is optional - will be auto-filled from booking if found.
    /// Required if no booking found for the phone number.
    /// </summary>
    public string? RoomNumber { get; set; }
}

public class GuestPushKeys
{
    public string P256dh { get; set; } = string.Empty;
    public string Auth { get; set; } = string.Empty;
}

public class GuestPushUnsubscribeRequest
{
    [Required]
    public string Endpoint { get; set; } = string.Empty;
}

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

public class GuestServiceRequest
{
    [Required]
    public int ServiceId { get; set; }

    [Required, MaxLength(20)]
    public string RoomNumber { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? PreferredTime { get; set; } // "asap", "this_afternoon", "this_evening", "tomorrow"

    [MaxLength(500)]
    public string? SpecialRequests { get; set; }

    [MaxLength(100)]
    public string? GuestName { get; set; }

    [MaxLength(20)]
    public string? Phone { get; set; }

    /// <summary>
    /// Source of the request for upsell tracking: weather_hot, weather_warm, weather_cold, weather_rainy, featured_carousel, service_menu
    /// </summary>
    [MaxLength(100)]
    public string? Source { get; set; }
}

public class GuestHousekeepingPreferenceRequest
{
    [Required, MaxLength(20)]
    public string RoomNumber { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string PreferenceType { get; set; } = string.Empty; // aircon_after_cleaning, linen_change_frequency, towel_change_frequency, dnd_schedule

    public System.Text.Json.JsonElement? PreferenceValue { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}

public class GuestItemRequestDto
{
    [Required]
    public int RequestItemId { get; set; }

    [MaxLength(20)]
    public string? RoomNumber { get; set; }

    public int Quantity { get; set; } = 1;

    [MaxLength(500)]
    public string? Notes { get; set; }
}

/// <summary>
/// Request DTO for quick feedback from Welcome Settled journey
/// </summary>
public class GuestQuickFeedbackRequest
{
    [Required, Range(1, 5)]
    public int Rating { get; set; }

    [MaxLength(1000)]
    public string? Comment { get; set; }

    [MaxLength(20)]
    public string? RoomNumber { get; set; }

    /// <summary>
    /// Issue category if guest has a problem (room_issue, noise, cleanliness, service, other)
    /// </summary>
    [MaxLength(50)]
    public string? IssueCategory { get; set; }
}

#endregion
