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