using Microsoft.EntityFrameworkCore;
using Hostr.Api.Data;
using Hostr.Api.Models;

namespace Hostr.Api.Services;

public interface ITenantDepartmentService
{
    Task<string> ResolveDepartmentAsync(int tenantId, string serviceCategory, string itemName, bool requiresRoomDelivery = false);
    Task<bool> ValidateRoomDeliveryAsync(int tenantId, string department, GuestStatus guestStatus);
    Task InitializeDefaultDepartmentsAsync(int tenantId, HotelSize hotelSize = HotelSize.Medium);
}

public class TenantDepartmentService : ITenantDepartmentService
{
    private readonly HostrDbContext _context;
    private readonly ILogger<TenantDepartmentService> _logger;

    public TenantDepartmentService(HostrDbContext context, ILogger<TenantDepartmentService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<string> ResolveDepartmentAsync(int tenantId, string serviceCategory, string itemName, bool requiresRoomDelivery = false)
    {
        try
        {
            // Step 1: Try to find tenant-specific mapping
            var mapping = await _context.ServiceDepartmentMappings
                .FirstOrDefaultAsync(m => m.TenantId == tenantId &&
                                         m.ServiceCategory == serviceCategory &&
                                         m.IsActive);

            if (mapping != null)
            {
                _logger.LogInformation("Found tenant-specific mapping for {ServiceCategory} → {Department} (tenant {TenantId})",
                    serviceCategory, mapping.TargetDepartment, tenantId);
                return mapping.TargetDepartment;
            }

            // Step 2: Use intelligent fallback based on service category and item name
            var fallbackDepartment = GetIntelligentFallback(serviceCategory, itemName, requiresRoomDelivery);

            // Step 3: Verify the fallback department exists for this tenant
            var departmentExists = await _context.TenantDepartments
                .AnyAsync(d => d.TenantId == tenantId &&
                              d.DepartmentName == fallbackDepartment &&
                              d.IsActive);

            if (departmentExists)
            {
                _logger.LogInformation("Using intelligent fallback {Department} for {ServiceCategory} (tenant {TenantId})",
                    fallbackDepartment, serviceCategory, tenantId);
                return fallbackDepartment;
            }

            // Step 4: Find any available department as last resort
            var availableDepartment = await _context.TenantDepartments
                .Where(d => d.TenantId == tenantId && d.IsActive)
                .OrderBy(d => d.Priority)
                .Select(d => d.DepartmentName)
                .FirstOrDefaultAsync();

            if (availableDepartment != null)
            {
                _logger.LogWarning("No specific mapping found, using first available department {Department} for {ServiceCategory} (tenant {TenantId})",
                    availableDepartment, serviceCategory, tenantId);
                return availableDepartment;
            }

            // Step 5: Ultimate fallback
            _logger.LogError("No departments configured for tenant {TenantId}, using FrontDesk as ultimate fallback", tenantId);
            return "FrontDesk";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving department for {ServiceCategory} (tenant {TenantId})", serviceCategory, tenantId);
            return "FrontDesk"; // Safe fallback
        }
    }

    public async Task<bool> ValidateRoomDeliveryAsync(int tenantId, string department, GuestStatus guestStatus)
    {
        try
        {
            // Check if this department requires room delivery validation
            var requiresValidation = await _context.ServiceDepartmentMappings
                .Where(m => m.TenantId == tenantId && m.TargetDepartment == department)
                .AnyAsync(m => m.RequiresRoomDelivery);

            if (!requiresValidation)
            {
                return true; // No room validation needed
            }

            // Validate guest status and room number
            var isValid = guestStatus.Type == GuestType.Active &&
                         !string.IsNullOrEmpty(guestStatus.RoomNumber) &&
                         guestStatus.RoomNumber != "Unknown";

            if (!isValid)
            {
                _logger.LogWarning("Room delivery validation failed for {Department}: Guest type={GuestType}, Room={RoomNumber}",
                    department, guestStatus.Type, guestStatus.RoomNumber ?? "null");
            }

            return isValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating room delivery for {Department} (tenant {TenantId})", department, tenantId);
            return false; // Fail safe
        }
    }

    public async Task InitializeDefaultDepartmentsAsync(int tenantId, HotelSize hotelSize = HotelSize.Medium)
    {
        try
        {
            // Check if departments already exist
            var existingCount = await _context.TenantDepartments
                .CountAsync(d => d.TenantId == tenantId);

            if (existingCount > 0)
            {
                _logger.LogInformation("Departments already exist for tenant {TenantId}, skipping initialization", tenantId);
                return;
            }

            // Create default departments based on hotel size
            var departments = DepartmentDefaults.DefaultDepartments[hotelSize];
            var tenantDepartments = new List<TenantDepartment>();

            for (int i = 0; i < departments.Count; i++)
            {
                tenantDepartments.Add(new TenantDepartment
                {
                    TenantId = tenantId,
                    DepartmentName = departments[i],
                    Description = GetDepartmentDescription(departments[i]),
                    IsActive = true,
                    Priority = i,
                    WorkingHours = departments[i] == "FrontDesk" ? "24/7" : "8:00-17:00"
                });
            }

            _context.TenantDepartments.AddRange(tenantDepartments);
            await _context.SaveChangesAsync();

            // Create default service mappings
            await CreateDefaultServiceMappingsAsync(tenantId, departments);

            _logger.LogInformation("Initialized {DepartmentCount} default departments for tenant {TenantId} (size: {HotelSize})",
                departments.Count, tenantId, hotelSize);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing default departments for tenant {TenantId}", tenantId);
            throw;
        }
    }

    private async Task CreateDefaultServiceMappingsAsync(int tenantId, List<string> availableDepartments)
    {
        var mappings = new List<ServiceDepartmentMapping>();

        foreach (var (serviceCategory, defaultDepartment) in DepartmentDefaults.DefaultServiceMappings)
        {
            // Use default department if available, otherwise fallback to FrontDesk
            var targetDepartment = availableDepartments.Contains(defaultDepartment)
                ? defaultDepartment
                : "FrontDesk";

            mappings.Add(new ServiceDepartmentMapping
            {
                TenantId = tenantId,
                ServiceCategory = serviceCategory,
                TargetDepartment = targetDepartment,
                RequiresRoomDelivery = IsRoomDeliveryService(serviceCategory),
                Priority = GetServicePriority(serviceCategory),
                IsActive = true
            });
        }

        _context.ServiceDepartmentMappings.AddRange(mappings);
        await _context.SaveChangesAsync();
    }

    private static string GetIntelligentFallback(string serviceCategory, string itemName, bool requiresRoomDelivery)
    {
        var categoryLower = serviceCategory.ToLower();
        var itemLower = itemName.ToLower();

        // Revenue-generating services → FrontDesk
        if (categoryLower.Contains("tour") || categoryLower.Contains("transportation") ||
            categoryLower.Contains("accommodation") || categoryLower.Contains("business") ||
            itemLower.Contains("tour") || itemLower.Contains("transfer"))
        {
            return "FrontDesk";
        }

        // Food & Beverage → FoodService (fallback to FrontDesk)
        if (categoryLower.Contains("dining") || categoryLower.Contains("food") || categoryLower.Contains("beverage") ||
            itemLower.Contains("food") || itemLower.Contains("breakfast") || itemLower.Contains("room service"))
        {
            return "FoodService";
        }

        // Physical delivery items → Housekeeping
        if (categoryLower.Contains("electronics") || categoryLower.Contains("amenities") ||
            categoryLower.Contains("laundry") || requiresRoomDelivery ||
            itemLower.Contains("charger") || itemLower.Contains("towel") || itemLower.Contains("amenity"))
        {
            return "Housekeeping";
        }

        // Maintenance items → Maintenance
        if (categoryLower.Contains("maintenance") || categoryLower.Contains("repair") ||
            itemLower.Contains("fix") || itemLower.Contains("repair") || itemLower.Contains("maintenance"))
        {
            return "Maintenance";
        }

        // Information and coordination → Concierge (fallback to FrontDesk)
        if (categoryLower.Contains("concierge") || categoryLower.Contains("information") ||
            categoryLower.Contains("wellness") || categoryLower.Contains("spa"))
        {
            return "Concierge";
        }

        // Default fallback
        return "FrontDesk";
    }

    private static string GetDepartmentDescription(string departmentName)
    {
        return departmentName switch
        {
            "FrontDesk" => "Guest services, check-in/out, sales, and general inquiries",
            "Housekeeping" => "Room cleaning, laundry, and in-room item delivery",
            "Maintenance" => "Facility maintenance, repairs, and technical support",
            "Concierge" => "Guest assistance, information, and coordination services",
            "FoodService" => "Restaurant, room service, and food & beverage operations",
            "Security" => "Safety, security, and access control",
            "IT" => "Technology support and infrastructure",
            "Events" => "Event planning and coordination",
            "Recreation" => "Activities, entertainment, and recreational facilities",
            "Spa" => "Wellness, spa services, and health facilities",
            _ => $"{departmentName} department services"
        };
    }

    private static bool IsRoomDeliveryService(string serviceCategory)
    {
        var category = serviceCategory.ToLower();
        return category.Contains("electronics") ||
               category.Contains("amenities") ||
               category.Contains("laundry") ||
               category.Contains("food") ||
               category.Contains("dining");
    }

    private static string GetServicePriority(string serviceCategory)
    {
        var category = serviceCategory.ToLower();

        if (category.Contains("tour") || category.Contains("transportation"))
            return "High"; // Revenue generating

        if (category.Contains("maintenance") || category.Contains("security"))
            return "High"; // Safety critical

        if (category.Contains("food") || category.Contains("dining"))
            return "Medium"; // Time sensitive

        return "Normal";
    }
}