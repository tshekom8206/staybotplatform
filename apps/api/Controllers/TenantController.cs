using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Hostr.Contracts.DTOs.Common;
using Hostr.Api.Services;
using Hostr.Api.Models;
using Hostr.Api.Data;

namespace Hostr.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TenantController : ControllerBase
{
    private readonly ITenantService _tenantService;
    private readonly ITenantOnboardingService _onboardingService;
    private readonly HostrDbContext _context;

    public TenantController(ITenantService tenantService, ITenantOnboardingService onboardingService, HostrDbContext context)
    {
        _tenantService = tenantService;
        _onboardingService = onboardingService;
        _context = context;
    }

    [HttpGet]
    public IActionResult GetTenant()
    {
        var tenantId = int.Parse(HttpContext.Items["TenantId"]?.ToString() ?? "0");
        var tenantSlug = HttpContext.Items["TenantSlug"]?.ToString() ?? "";
        var tenantName = HttpContext.Items["TenantName"]?.ToString() ?? "";
        var tenantPlan = HttpContext.Items["TenantPlan"]?.ToString() ?? "";
        var themePrimary = HttpContext.Items["TenantThemePrimary"]?.ToString() ?? "";
        var timezone = HttpContext.Items["TenantTimezone"]?.ToString() ?? "";

        var tenant = new TenantDto
        {
            Id = tenantId,
            Slug = tenantSlug,
            Name = tenantName,
            Plan = tenantPlan,
            ThemePrimary = themePrimary,
            Timezone = timezone,
            Features = _tenantService.GetFeatures(tenantPlan)
        };

        return Ok(new ApiResponse<TenantDto> 
        { 
            Success = true, 
            Data = tenant 
        });
    }

    [HttpPost("onboard")]
    [AllowAnonymous]
    public async Task<IActionResult> OnboardTenant([FromBody] TenantOnboardingRequest request)
    {
        var result = await _onboardingService.OnboardTenantAsync(request);
        
        if (result.Success)
        {
            return Ok(new ApiResponse<TenantOnboardingResponse> 
            { 
                Success = true, 
                Data = result 
            });
        }
        
        return BadRequest(new ApiResponse<TenantOnboardingResponse> 
        { 
            Success = false, 
            Error = result.ErrorMessage,
            Data = result 
        });
    }

    [HttpGet("validate-slug/{slug}")]
    [AllowAnonymous]
    public async Task<IActionResult> ValidateSlugAvailability(string slug)
    {
        var isAvailable = await _onboardingService.ValidateSlugAvailabilityAsync(slug);
        return Ok(new ApiResponse<object> 
        { 
            Success = true, 
            Data = new { isAvailable, slug } 
        });
    }

    [HttpGet("validate-email/{email}")]
    [AllowAnonymous]
    public async Task<IActionResult> ValidateEmailAvailability(string email)
    {
        var isAvailable = await _onboardingService.ValidateEmailAvailabilityAsync(email);
        return Ok(new ApiResponse<object>
        {
            Success = true,
            Data = new { isAvailable, email }
        });
    }

    [HttpGet("{tenantId:int}")]
    public async Task<IActionResult> GetTenantWithHotelInfo(int tenantId)
    {
        try
        {
            var tenant = await _context.Tenants
                .Where(t => t.Id == tenantId)
                .Select(t => new
                {
                    Id = t.Id,
                    Name = t.Name,
                    Slug = t.Slug,
                    Plan = t.Plan,
                    ThemePrimary = t.ThemePrimary,
                    Timezone = t.Timezone,
                    Status = t.Status,
                    CreatedAt = t.CreatedAt
                })
                .FirstOrDefaultAsync();

            if (tenant == null)
            {
                return NotFound(new ApiResponse<object>
                {
                    Success = false,
                    Error = "Tenant not found"
                });
            }

            var hotelInfo = await _context.HotelInfos
                .Where(h => h.TenantId == tenantId)
                .FirstOrDefaultAsync();

            // Get available services from Services table instead of HotelInfo.Features
            var availableServices = await _context.Services
                .Where(s => s.TenantId == tenantId && s.IsAvailable)
                .OrderBy(s => s.Priority)
                .ThenBy(s => s.Name)
                .Select(s => s.Name)
                .ToArrayAsync();

            var response = new
            {
                tenant = new
                {
                    name = tenant.Name,
                    description = hotelInfo?.Description ?? "",
                    category = hotelInfo?.Category ?? "",
                    logo = hotelInfo?.LogoUrl,
                    phone = hotelInfo?.Phone ?? "",
                    email = hotelInfo?.Email ?? "",
                    website = hotelInfo?.Website ?? "",
                    address = new
                    {
                        street = hotelInfo?.Street ?? "",
                        city = hotelInfo?.City ?? "",
                        state = hotelInfo?.State ?? "",
                        postalCode = hotelInfo?.PostalCode ?? "",
                        country = hotelInfo?.Country ?? ""
                    },
                    checkInTime = hotelInfo?.CheckInTime ?? "15:00",
                    checkOutTime = hotelInfo?.CheckOutTime ?? "11:00",
                    numberOfRooms = hotelInfo?.NumberOfRooms ?? 0,
                    numberOfFloors = hotelInfo?.NumberOfFloors ?? 1,
                    establishedYear = hotelInfo?.EstablishedYear ?? DateTime.Now.Year,
                    supportedLanguages = !string.IsNullOrEmpty(hotelInfo?.SupportedLanguages)
                        ? JsonSerializer.Deserialize<string[]>(hotelInfo.SupportedLanguages)
                        : new[] { "en" },
                    defaultLanguage = hotelInfo?.DefaultLanguage ?? "en",
                    features = availableServices,
                    socialMedia = new
                    {
                        facebook = hotelInfo?.FacebookUrl,
                        twitter = hotelInfo?.TwitterUrl,
                        instagram = hotelInfo?.InstagramUrl,
                        linkedin = hotelInfo?.LinkedInUrl
                    },
                    policies = new
                    {
                        cancellationPolicy = hotelInfo?.CancellationPolicy ?? "",
                        petPolicy = hotelInfo?.PetPolicy ?? "",
                        smokingPolicy = hotelInfo?.SmokingPolicy ?? "",
                        childPolicy = hotelInfo?.ChildPolicy ?? ""
                    },
                    settings = new
                    {
                        allowOnlineBooking = hotelInfo?.AllowOnlineBooking ?? true,
                        requirePhoneVerification = hotelInfo?.RequirePhoneVerification ?? true,
                        enableNotifications = hotelInfo?.EnableNotifications ?? true,
                        enableChatbot = hotelInfo?.EnableChatbot ?? true,
                        timezone = tenant.Timezone,
                        currency = hotelInfo?.Currency ?? "USD"
                    }
                }
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Error = $"An error occurred: {ex.Message}"
            });
        }
    }

    [HttpPut("{tenantId:int}")]
    public async Task<IActionResult> UpdateTenantHotelInfo(int tenantId, [FromBody] UpdateHotelInfoRequest request)
    {
        try
        {
            var tenant = await _context.Tenants.FindAsync(tenantId);
            if (tenant == null)
            {
                return NotFound(new ApiResponse<object>
                {
                    Success = false,
                    Error = "Tenant not found"
                });
            }

            var hotelInfo = await _context.HotelInfos
                .FirstOrDefaultAsync(h => h.TenantId == tenantId);

            if (hotelInfo == null)
            {
                hotelInfo = new HotelInfo { TenantId = tenantId };
                _context.HotelInfos.Add(hotelInfo);
            }

            // Update tenant name if provided
            if (!string.IsNullOrEmpty(request.Name))
            {
                tenant.Name = request.Name;
            }

            // Update hotel info fields
            if (!string.IsNullOrEmpty(request.Description))
                hotelInfo.Description = request.Description;

            if (!string.IsNullOrEmpty(request.Category))
                hotelInfo.Category = request.Category;

            if (!string.IsNullOrEmpty(request.Logo))
                hotelInfo.LogoUrl = request.Logo;

            if (!string.IsNullOrEmpty(request.Phone))
                hotelInfo.Phone = request.Phone;

            if (!string.IsNullOrEmpty(request.Email))
                hotelInfo.Email = request.Email;

            if (!string.IsNullOrEmpty(request.Website))
                hotelInfo.Website = request.Website;

            // Update address if provided
            if (request.Address != null)
            {
                if (!string.IsNullOrEmpty(request.Address.Street))
                    hotelInfo.Street = request.Address.Street;
                if (!string.IsNullOrEmpty(request.Address.City))
                    hotelInfo.City = request.Address.City;
                if (!string.IsNullOrEmpty(request.Address.State))
                    hotelInfo.State = request.Address.State;
                if (!string.IsNullOrEmpty(request.Address.PostalCode))
                    hotelInfo.PostalCode = request.Address.PostalCode;
                if (!string.IsNullOrEmpty(request.Address.Country))
                    hotelInfo.Country = request.Address.Country;
            }

            if (!string.IsNullOrEmpty(request.CheckInTime))
                hotelInfo.CheckInTime = request.CheckInTime;

            if (!string.IsNullOrEmpty(request.CheckOutTime))
                hotelInfo.CheckOutTime = request.CheckOutTime;

            if (request.NumberOfRooms.HasValue)
                hotelInfo.NumberOfRooms = request.NumberOfRooms.Value;

            if (request.NumberOfFloors.HasValue)
                hotelInfo.NumberOfFloors = request.NumberOfFloors.Value;

            if (request.EstablishedYear.HasValue)
                hotelInfo.EstablishedYear = request.EstablishedYear.Value;

            if (request.SupportedLanguages != null)
                hotelInfo.SupportedLanguages = JsonSerializer.Serialize(request.SupportedLanguages);

            if (!string.IsNullOrEmpty(request.DefaultLanguage))
                hotelInfo.DefaultLanguage = request.DefaultLanguage;

            // Features are now managed through Services table, not stored in HotelInfo

            // Update social media if provided
            if (request.SocialMedia != null)
            {
                if (!string.IsNullOrEmpty(request.SocialMedia.Facebook))
                    hotelInfo.FacebookUrl = request.SocialMedia.Facebook;
                if (!string.IsNullOrEmpty(request.SocialMedia.Twitter))
                    hotelInfo.TwitterUrl = request.SocialMedia.Twitter;
                if (!string.IsNullOrEmpty(request.SocialMedia.Instagram))
                    hotelInfo.InstagramUrl = request.SocialMedia.Instagram;
                if (!string.IsNullOrEmpty(request.SocialMedia.Linkedin))
                    hotelInfo.LinkedInUrl = request.SocialMedia.Linkedin;
            }

            // Update policies if provided
            if (request.Policies != null)
            {
                if (!string.IsNullOrEmpty(request.Policies.CancellationPolicy))
                    hotelInfo.CancellationPolicy = request.Policies.CancellationPolicy;
                if (!string.IsNullOrEmpty(request.Policies.PetPolicy))
                    hotelInfo.PetPolicy = request.Policies.PetPolicy;
                if (!string.IsNullOrEmpty(request.Policies.SmokingPolicy))
                    hotelInfo.SmokingPolicy = request.Policies.SmokingPolicy;
                if (!string.IsNullOrEmpty(request.Policies.ChildPolicy))
                    hotelInfo.ChildPolicy = request.Policies.ChildPolicy;
            }

            // Update settings if provided
            if (request.Settings != null)
            {
                if (request.Settings.AllowOnlineBooking.HasValue)
                    hotelInfo.AllowOnlineBooking = request.Settings.AllowOnlineBooking.Value;
                if (request.Settings.RequirePhoneVerification.HasValue)
                    hotelInfo.RequirePhoneVerification = request.Settings.RequirePhoneVerification.Value;
                if (request.Settings.EnableNotifications.HasValue)
                    hotelInfo.EnableNotifications = request.Settings.EnableNotifications.Value;
                if (request.Settings.EnableChatbot.HasValue)
                    hotelInfo.EnableChatbot = request.Settings.EnableChatbot.Value;
                if (!string.IsNullOrEmpty(request.Settings.Timezone))
                    tenant.Timezone = request.Settings.Timezone;
                if (!string.IsNullOrEmpty(request.Settings.Currency))
                    hotelInfo.Currency = request.Settings.Currency;
            }

            hotelInfo.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // Return updated data
            return await GetTenantWithHotelInfo(tenantId);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Error = $"An error occurred: {ex.Message}"
            });
        }
    }

    [HttpGet("categories")]
    public async Task<IActionResult> GetHotelCategories()
    {
        try
        {
            var categories = await _context.HotelCategories
                .Where(c => c.IsActive)
                .OrderBy(c => c.SortOrder)
                .Select(c => new { value = c.Value, label = c.Label })
                .ToListAsync();

            return Ok(new { categories });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Error = $"An error occurred: {ex.Message}"
            });
        }
    }

    [HttpGet("languages")]
    public async Task<IActionResult> GetAvailableLanguages()
    {
        try
        {
            var languages = await _context.SupportedLanguages
                .Where(l => l.IsActive)
                .OrderBy(l => l.SortOrder)
                .Select(l => new { code = l.Code, name = l.Name })
                .ToListAsync();

            return Ok(new { languages });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Error = $"An error occurred: {ex.Message}"
            });
        }
    }

    [HttpGet("{tenantId:int}/features")]
    public async Task<IActionResult> GetAvailableFeatures(int tenantId)
    {
        try
        {
            // Get available services from Services table for this tenant
            var features = await _context.Services
                .Where(s => s.TenantId == tenantId && s.IsAvailable)
                .OrderBy(s => s.Priority)
                .ThenBy(s => s.Name)
                .Select(s => s.Name)
                .ToListAsync();

            return Ok(new { features });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Error = $"An error occurred: {ex.Message}"
            });
        }
    }

    [HttpGet("timezones")]
    public async Task<IActionResult> GetTimezones()
    {
        try
        {
            var timezones = new[]
            {
                "UTC", "America/New_York", "America/Chicago", "America/Denver",
                "America/Los_Angeles", "Europe/London", "Europe/Paris", "Europe/Berlin",
                "Asia/Tokyo", "Asia/Shanghai", "Australia/Sydney", "Africa/Johannesburg"
            };

            return Ok(new { timezones });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Error = $"An error occurred: {ex.Message}"
            });
        }
    }

    [HttpGet("currencies")]
    public async Task<IActionResult> GetCurrencies()
    {
        try
        {
            var currencies = await _context.SupportedCurrencies
                .Where(c => c.IsActive)
                .OrderBy(c => c.SortOrder)
                .Select(c => new { code = c.Code, name = c.Name })
                .ToListAsync();

            return Ok(new { currencies });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Error = $"An error occurred: {ex.Message}"
            });
        }
    }
}

[ApiController]
[Route("[controller]")]
public class ManifestController : ControllerBase
{
    [HttpGet("manifest.webmanifest")]
    public IActionResult GetManifest()
    {
        var tenantName = HttpContext.Items["TenantName"]?.ToString() ?? "Hostr";
        var themePrimary = HttpContext.Items["TenantThemePrimary"]?.ToString() ?? "#007bff";
        var tenantSlug = HttpContext.Items["TenantSlug"]?.ToString() ?? "";

        var manifest = new
        {
            name = $"{tenantName} - Hotel Concierge",
            short_name = tenantName,
            description = $"Hotel concierge service for {tenantName}",
            start_url = $"https://{tenantSlug}.hostr.co.za/",
            display = "standalone",
            background_color = "#ffffff",
            theme_color = themePrimary,
            icons = new[]
            {
                new
                {
                    src = "/images/icon-192.png",
                    sizes = "192x192",
                    type = "image/png"
                },
                new
                {
                    src = "/images/icon-512.png",
                    sizes = "512x512",
                    type = "image/png"
                }
            }
        };

        return Ok(manifest);
    }
}