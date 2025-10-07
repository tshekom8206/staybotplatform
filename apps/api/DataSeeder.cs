using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Hostr.Api.Data;
using Hostr.Api.Models;
using System.Text.Json;

namespace Hostr.Api;

public class DataSeeder
{
    private readonly HostrDbContext _context;

    public DataSeeder(HostrDbContext context)
    {
        _context = context;
    }

    public async Task SeedDemoDataAsync()
    {
        const int tenantId = 1;

        Console.WriteLine("Starting demonstration data seeding...");

        // Check if tenant exists
        var tenant = await _context.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId);
        if (tenant == null)
        {
            Console.WriteLine($"Error: Tenant with ID {tenantId} not found. Please ensure tenant exists first.");
            return;
        }

        Console.WriteLine($"Seeding data for tenant: {tenant.Name} (ID: {tenantId})");

        await SeedWiFiCredentialsAsync(tenantId);
        await SeedFAQsAsync(tenantId);

        await _context.SaveChangesAsync();
        Console.WriteLine("Demonstration data seeded successfully!");
    }

    private async Task SeedWiFiCredentialsAsync(int tenantId)
    {
        Console.WriteLine("Adding WiFi credentials...");

        // Check if WiFi credentials already exist
        var existingWiFi = await _context.BusinessInfo
            .FirstOrDefaultAsync(bi => bi.TenantId == tenantId && bi.Category == "wifi_credentials");

        if (existingWiFi != null)
        {
            Console.WriteLine("WiFi credentials already exist, skipping...");
            return;
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
            UpdatedAt = DateTime.UtcNow
        };

        _context.BusinessInfo.Add(wifiCredentials);
        Console.WriteLine("✓ WiFi credentials added");
    }

    private async Task SeedFAQsAsync(int tenantId)
    {
        Console.WriteLine("Adding essential FAQ entries...");

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
                .FirstOrDefaultAsync(f => f.TenantId == tenantId && f.Question == question);

            if (existingFaq != null)
            {
                Console.WriteLine($"FAQ already exists: {question.Substring(0, Math.Min(50, question.Length))}...");
                continue;
            }

            var faq = new FAQ
            {
                TenantId = tenantId,
                Question = question,
                Answer = answer,
                Language = "en",
                Tags = tags,
                UpdatedAt = DateTime.UtcNow
            };

            _context.FAQs.Add(faq);
            addedCount++;
        }

        Console.WriteLine($"✓ {addedCount} FAQ entries added");
    }
}

public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("Hostr Demo Data Seeder");
        Console.WriteLine("=====================");

        // Build configuration
        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables();

        var configuration = builder.Build();

        // Build services
        var services = new ServiceCollection();
        services.AddDbContext<HostrDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));
        
        services.AddHttpContextAccessor();
        services.AddScoped<DataSeeder>();

        var serviceProvider = services.BuildServiceProvider();

        try
        {
            using var scope = serviceProvider.CreateScope();
            var seeder = scope.ServiceProvider.GetRequiredService<DataSeeder>();
            
            await seeder.SeedDemoDataAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error seeding data: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            Environment.Exit(1);
        }

        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
    }
}