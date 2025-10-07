using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Hostr.Api.Data;
using Hostr.Api.Models;

namespace Hostr.Api.Services;

public interface ITenantCacheService
{
    Task<Tenant> GetTenantAsync(int tenantId);
    Task<List<Service>> GetTenantServicesAsync(int tenantId);
    Task<List<MenuCategory>> GetTenantMenuCategoriesAsync(int tenantId);
    Task<List<RequestItem>> GetTenantRequestItemsAsync(int tenantId);
    Task InvalidateTenantCacheAsync(int tenantId);
    Task<string> GetTenantTimezoneAsync(int tenantId);
}

public class TenantCacheService : ITenantCacheService
{
    private readonly HostrDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly ILogger<TenantCacheService> _logger;
    private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(15);

    public TenantCacheService(HostrDbContext context, IMemoryCache cache, ILogger<TenantCacheService> logger)
    {
        _context = context;
        _cache = cache;
        _logger = logger;
    }

    public async Task<Tenant> GetTenantAsync(int tenantId)
    {
        var cacheKey = $"tenant_{tenantId}";

        if (_cache.TryGetValue(cacheKey, out Tenant? cachedTenant) && cachedTenant != null)
        {
            return cachedTenant;
        }

        try
        {
            var tenant = await _context.Tenants
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == tenantId);

            if (tenant != null)
            {
                _cache.Set(cacheKey, tenant, _cacheExpiration);
                _logger.LogDebug("Cached tenant {TenantId} for {Minutes} minutes", tenantId, _cacheExpiration.TotalMinutes);
            }

            return tenant ?? throw new ArgumentException($"Tenant {tenantId} not found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting tenant {TenantId}", tenantId);
            throw;
        }
    }

    public async Task<List<Service>> GetTenantServicesAsync(int tenantId)
    {
        var cacheKey = $"tenant_services_{tenantId}";

        if (_cache.TryGetValue(cacheKey, out List<Service>? cachedServices) && cachedServices != null)
        {
            return cachedServices;
        }

        try
        {
            var services = await _context.Services
                .AsNoTracking()
                .Where(s => s.TenantId == tenantId)
                .ToListAsync();

            _cache.Set(cacheKey, services, _cacheExpiration);
            _logger.LogDebug("Cached {Count} services for tenant {TenantId}", services.Count, tenantId);

            return services;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting services for tenant {TenantId}", tenantId);
            return new List<Service>();
        }
    }

    public async Task<List<MenuCategory>> GetTenantMenuCategoriesAsync(int tenantId)
    {
        var cacheKey = $"tenant_menu_categories_{tenantId}";

        if (_cache.TryGetValue(cacheKey, out List<MenuCategory>? cachedCategories) && cachedCategories != null)
        {
            return cachedCategories;
        }

        try
        {
            var categories = await _context.MenuCategories
                .AsNoTracking()
                .Where(mc => mc.TenantId == tenantId)
                .OrderBy(mc => mc.DisplayOrder)
                .ToListAsync();

            _cache.Set(cacheKey, categories, _cacheExpiration);
            _logger.LogDebug("Cached {Count} menu categories for tenant {TenantId}", categories.Count, tenantId);

            return categories;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting menu categories for tenant {TenantId}", tenantId);
            return new List<MenuCategory>();
        }
    }

    public async Task<List<RequestItem>> GetTenantRequestItemsAsync(int tenantId)
    {
        var cacheKey = $"tenant_request_items_{tenantId}";

        if (_cache.TryGetValue(cacheKey, out List<RequestItem>? cachedItems) && cachedItems != null)
        {
            return cachedItems;
        }

        try
        {
            var requestItems = await _context.RequestItems
                .AsNoTracking()
                .Where(ri => ri.TenantId == tenantId)
                .ToListAsync();

            _cache.Set(cacheKey, requestItems, _cacheExpiration);
            _logger.LogDebug("Cached {Count} request items for tenant {TenantId}", requestItems.Count, tenantId);

            return requestItems;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting request items for tenant {TenantId}", tenantId);
            return new List<RequestItem>();
        }
    }

    public async Task<string> GetTenantTimezoneAsync(int tenantId)
    {
        var cacheKey = $"tenant_timezone_{tenantId}";

        if (_cache.TryGetValue(cacheKey, out string? cachedTimezone) && cachedTimezone != null)
        {
            return cachedTimezone;
        }

        try
        {
            var timezone = await _context.Tenants
                .AsNoTracking()
                .Where(t => t.Id == tenantId)
                .Select(t => t.Timezone)
                .FirstOrDefaultAsync();

            if (!string.IsNullOrEmpty(timezone))
            {
                _cache.Set(cacheKey, timezone, _cacheExpiration);
                _logger.LogDebug("Cached timezone {Timezone} for tenant {TenantId}", timezone, tenantId);
            }

            return timezone ?? "UTC";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting timezone for tenant {TenantId}", tenantId);
            return "UTC";
        }
    }

    public async Task InvalidateTenantCacheAsync(int tenantId)
    {
        try
        {
            var cacheKeys = new[]
            {
                $"tenant_{tenantId}",
                $"tenant_services_{tenantId}",
                $"tenant_menu_categories_{tenantId}",
                $"tenant_request_items_{tenantId}",
                $"tenant_timezone_{tenantId}"
            };

            foreach (var key in cacheKeys)
            {
                _cache.Remove(key);
            }

            _logger.LogDebug("Invalidated cache for tenant {TenantId}", tenantId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invalidating cache for tenant {TenantId}", tenantId);
        }

        await Task.CompletedTask;
    }
}