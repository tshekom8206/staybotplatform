using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Hostr.Api.Data;
using Hostr.Api.Models;
using System.Text.Json;

namespace Hostr.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[AllowAnonymous] // Temporary for demo purposes
public class DataSeedController : ControllerBase
{
    private readonly HostrDbContext _context;
    private readonly ILogger<DataSeedController> _logger;
    private readonly UserManager<User> _userManager;

    public DataSeedController(HostrDbContext context, ILogger<DataSeedController> logger, UserManager<User> userManager)
    {
        _context = context;
        _logger = logger;
        _userManager = userManager;
    }

    [HttpPost("demo-data")]
    public async Task<IActionResult> SeedDemoData()
    {
        const int tenantId = 1;

        try
        {
            _logger.LogInformation("Starting demonstration data seeding for tenant {TenantId}", tenantId);

            // Check if tenant exists, create if not - bypass global filters for data seeding
            var tenant = await _context.Tenants
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Id == tenantId);
            if (tenant == null)
            {
                _logger.LogInformation("Tenant with ID {TenantId} not found, creating...", tenantId);
                tenant = await CreateTenantAsync(tenantId);
            }
            else
            {
                _logger.LogInformation("Found existing tenant: {TenantName} (ID: {TenantId})", tenant.Name, tenantId);
            }

            // Disable global filters for data seeding operations
            _context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;

            var results = new
            {
                TenantName = tenant.Name,
                TenantId = tenantId,
                WiFiCredentialsAdded = await SeedWiFiCredentialsAsync(tenantId),
                BasicFAQsAdded = await SeedFAQsAsync(tenantId),
                BusinessInfoAdded = 0 // BusinessInfo data added via SQL script
            };

            await _context.SaveChangesAsync();
            _logger.LogInformation("Demonstration data seeded successfully for tenant {TenantId}", tenantId);

            return Ok(new
            {
                Message = "Demonstration data seeded successfully!",
                Details = results
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error seeding demonstration data for tenant {TenantId}", tenantId);
            return StatusCode(500, new { Message = "Error seeding data", Error = ex.Message });
        }
    }

    [HttpPost("essential-faqs")]
    public async Task<IActionResult> SeedEssentialFAQs()
    {
        const int tenantId = 1;

        try
        {
            _logger.LogInformation("Starting essential FAQ seeding for tenant {TenantId}", tenantId);

            // Check if tenant exists, create if not - bypass global filters for data seeding
            var tenant = await _context.Tenants
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Id == tenantId);
            if (tenant == null)
            {
                _logger.LogInformation("Tenant with ID {TenantId} not found, creating...", tenantId);
                tenant = await CreateTenantAsync(tenantId);
            }
            else
            {
                _logger.LogInformation("Found existing tenant: {TenantName} (ID: {TenantId})", tenant.Name, tenantId);
            }

            // Disable global filters for data seeding operations
            _context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;

            var addedCount = await SeedFAQsAsync(tenantId);

            await _context.SaveChangesAsync();
            _logger.LogInformation("Essential FAQs seeded successfully for tenant {TenantId}", tenantId);

            return Ok(new
            {
                Message = "Essential FAQs seeded successfully!",
                TenantName = tenant.Name,
                TenantId = tenantId,
                EssentialFAQsAdded = addedCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error seeding essential FAQs for tenant {TenantId}", tenantId);
            return StatusCode(500, new { Message = "Error seeding essential FAQs", Error = ex.Message });
        }
    }

    [HttpPost("create-test-admin")]
    public async Task<IActionResult> CreateTestAdmin()
    {
        try
        {
            const string testEmail = "test@admin.com";
            const string testPassword = "Password123!";

            _logger.LogInformation("Creating test admin user: {Email}", testEmail);

            // Check if user already exists
            var existingUser = await _userManager.FindByEmailAsync(testEmail);
            if (existingUser != null)
            {
                // Delete existing user to recreate
                await _userManager.DeleteAsync(existingUser);
                _logger.LogInformation("Deleted existing test admin user");
            }

            // Create new test admin user
            var user = new User
            {
                UserName = testEmail,
                Email = testEmail,
                EmailConfirmed = true,
                IsActive = true,
                CreatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc)
            };

            // Create user with password using Identity's proper hashing
            var result = await _userManager.CreateAsync(user, testPassword);

            if (result.Succeeded)
            {
                _logger.LogInformation("Test admin user created successfully: {Email}", testEmail);

                // Add user to tenant 1
                var userTenant = new UserTenant
                {
                    UserId = user.Id,
                    TenantId = 1, // panoramaview tenant
                    Role = "Admin"
                };

                _context.UserTenants.Add(userTenant);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    Message = "Test admin user created successfully!",
                    Email = testEmail,
                    Password = testPassword,
                    Instructions = "You can now login with these credentials in the admin UI",
                    TenantId = 1,
                    Role = "Admin"
                });
            }
            else
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                _logger.LogError("Test admin creation failed: {Errors}", errors);

                return BadRequest(new
                {
                    Message = "Test admin creation failed",
                    Email = testEmail,
                    Errors = result.Errors.Select(e => e.Description)
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating test admin user");
            return StatusCode(500, new { Message = "Error creating test admin user", Error = ex.Message });
        }
    }

    [HttpPost("fix-test-guest")]
    public async Task<IActionResult> FixTestGuestStatus()
    {
        try
        {
            const string testPhone = "+27783776207";
            
            // Update the test guest's booking status to Active so we can test notifications
            var booking = await _context.Bookings
                .IgnoreQueryFilters()
                .Where(b => b.Phone == testPhone)
                .OrderByDescending(b => b.CreatedAt)
                .FirstOrDefaultAsync();

            if (booking == null)
            {
                return NotFound(new { Message = "Test guest booking not found", Phone = testPhone });
            }

            booking.Status = "CheckedIn";
            booking.CheckinDate = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-2)); // Checked in 2 hours ago
            booking.CheckoutDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)); // Checkout in 2 days
            
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Fixed test guest status: {Phone} - Status: {Status}, CheckIn: {CheckIn}, CheckOut: {CheckOut}",
                testPhone, booking.Status, booking.CheckinDate, booking.CheckoutDate);

            return Ok(new
            {
                Message = "Test guest status updated successfully!",
                Phone = testPhone,
                Status = booking.Status,
                CheckinDate = booking.CheckinDate,
                CheckoutDate = booking.CheckoutDate,
                BookingId = booking.Id
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fixing test guest status");
            return StatusCode(500, new { Message = "Error fixing test guest status", Error = ex.Message });
        }
    }

    private async Task<bool> SeedWiFiCredentialsAsync(int tenantId)
    {
        // Check if WiFi credentials already exist
        var existingWiFi = await _context.BusinessInfo
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(bi => bi.TenantId == tenantId && bi.Category == "wifi_credentials");

        if (existingWiFi != null)
        {
            _logger.LogInformation("WiFi credentials already exist for tenant {TenantId}, skipping...", tenantId);
            return false;
        }

        var wifiCredentials = new BusinessInfo
        {
            TenantId = tenantId,
            Category = "wifi_credentials",
            Title = "WiFi Network Information",
            Content = JsonSerializer.Serialize(new
            {
                network = "PanoramaView_Guest",
                password = "Welcome2024!"
            }),
            Tags = new[] { "wifi", "internet", "guest", "network" },
            IsActive = true,
            DisplayOrder = 1,
            UpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc)
        };

        _context.BusinessInfo.Add(wifiCredentials);
        _logger.LogInformation("WiFi credentials added for tenant {TenantId}", tenantId);
        return true;
    }

    private async Task<int> SeedFAQsAsync(int tenantId)
    {
        var faqs = new List<(string question, string answer, string[] tags)>
        {
            (
                "What is the WiFi password?",
                "Our guest WiFi network is 'PanoramaView_Guest' with password 'Welcome2024!'. The network provides high-speed internet throughout the hotel including all rooms, lobby, restaurant, and pool areas.",
                new[] { "wifi", "internet", "password", "network" }
            ),
            (
                "What time is checkout?",
                "Standard checkout time is 11:00 AM. Late checkout until 2:00 PM can be arranged subject to availability - just ask at the front desk!",
                new[] { "checkout", "time", "late checkout", "front desk" }
            ),
            (
                "Do you have room service?",
                "Yes! Room service is available 24/7. You can view our full menu by asking me to 'show menu' or call extension 7 from your room phone.",
                new[] { "room service", "menu", "24/7", "extension", "food" }
            ),
            (
                "Where is the gym?",
                "Our fitness center is located on the 2nd floor and is open 24/7 for guests. Access with your room key. We have cardio equipment, weights, and towels available.",
                new[] { "gym", "fitness", "2nd floor", "24/7", "equipment", "towels" }
            ),
            (
                "What time is breakfast?",
                "Breakfast is served daily from 6:30 AM to 10:30 AM in the Garden Restaurant on the ground floor. We offer both continental and full American breakfast options.",
                new[] { "breakfast", "time", "restaurant", "continental", "american", "ground floor" }
            ),
            (
                "Do you have parking?",
                "Yes, we offer complimentary valet parking for all guests. Just pull up to the main entrance and our staff will take care of your vehicle.",
                new[] { "parking", "valet", "complimentary", "free", "car", "vehicle" }
            ),
            (
                "Can I get extra towels?",
                "Absolutely! I can arrange for housekeeping to bring extra towels to your room. They should arrive within 15-20 minutes. Anything else you need?",
                new[] { "towels", "housekeeping", "extra", "room", "amenities" }
            )
        };

        int addedCount = 0;
        foreach (var (question, answer, tags) in faqs)
        {
            // Check if FAQ already exists
            var existingFaq = await _context.FAQs
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(f => f.TenantId == tenantId && f.Question == question);

            if (existingFaq != null)
            {
                _logger.LogInformation("FAQ already exists for tenant {TenantId}: {Question}", tenantId, question.Substring(0, Math.Min(50, question.Length)));
                continue;
            }

            var faq = new FAQ
            {
                TenantId = tenantId,
                Question = question,
                Answer = answer,
                Language = "en",
                Tags = tags,
                UpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc)
            };

            _context.FAQs.Add(faq);
            addedCount++;
        }

        _logger.LogInformation("{Count} FAQ entries added for tenant {TenantId}", addedCount, tenantId);
        return addedCount;
    }

    private async Task<Tenant> CreateTenantAsync(int tenantId)
    {
        _logger.LogInformation("Creating tenant with ID {TenantId}", tenantId);

        var tenant = new Tenant
        {
            Id = tenantId,
            Name = "Panorama View Hotel",
            Slug = "panorama-view",
            Timezone = "Africa/Johannesburg",
            Plan = "Premium",
            ThemePrimary = "#2563eb",
            Status = "Active",
            RetentionDays = 90,
            CreatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc)
        };

        _context.Tenants.Add(tenant);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Tenant '{TenantName}' created successfully with ID {TenantId}", tenant.Name, tenantId);
        return tenant;
    }
}