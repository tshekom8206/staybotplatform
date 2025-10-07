using Microsoft.EntityFrameworkCore;
using Hostr.Api.Data;

namespace Hostr.Api.Services;

public class TenantContext
{
    public int TenantId { get; set; }
    public string TenantSlug { get; set; } = string.Empty;
    public string TenantName { get; set; } = string.Empty;
    public string Plan { get; set; } = string.Empty;
    public string ThemePrimary { get; set; } = string.Empty;
    public string Timezone { get; set; } = string.Empty;
}

public interface ITenantService
{
    Task<TenantContext?> GetTenantByIdAsync(int tenantId);
    Task<TenantContext?> GetTenantBySlugAsync(string slug);
    Task<TenantContext?> GetTenantByPhoneNumberIdAsync(string phoneNumberId);
    Dictionary<string, bool> GetFeatures(string plan);
}

public class TenantService : ITenantService
{
    private readonly HostrDbContext _context;

    public TenantService(HostrDbContext context)
    {
        _context = context;
    }

    public async Task<TenantContext?> GetTenantByIdAsync(int tenantId)
    {
        var tenant = await _context.Tenants
            .Where(t => t.Id == tenantId && t.Status == "Active")
            .Select(t => new TenantContext
            {
                TenantId = t.Id,
                TenantSlug = t.Slug,
                TenantName = t.Name,
                Plan = t.Plan,
                ThemePrimary = t.ThemePrimary,
                Timezone = t.Timezone
            })
            .FirstOrDefaultAsync();

        return tenant;
    }

    public async Task<TenantContext?> GetTenantBySlugAsync(string slug)
    {
        var tenant = await _context.Tenants
            .Where(t => t.Slug == slug && t.Status == "Active")
            .Select(t => new TenantContext
            {
                TenantId = t.Id,
                TenantSlug = t.Slug,
                TenantName = t.Name,
                Plan = t.Plan,
                ThemePrimary = t.ThemePrimary,
                Timezone = t.Timezone
            })
            .FirstOrDefaultAsync();

        return tenant;
    }

    public async Task<TenantContext?> GetTenantByPhoneNumberIdAsync(string phoneNumberId)
    {
        var tenant = await _context.WhatsAppNumbers
            .Where(w => w.PhoneNumberId == phoneNumberId && w.Status == "Active")
            .Select(w => new TenantContext
            {
                TenantId = w.Tenant.Id,
                TenantSlug = w.Tenant.Slug,
                TenantName = w.Tenant.Name,
                Plan = w.Tenant.Plan,
                ThemePrimary = w.Tenant.ThemePrimary,
                Timezone = w.Tenant.Timezone
            })
            .FirstOrDefaultAsync();

        return tenant;
    }

    public Dictionary<string, bool> GetFeatures(string plan)
    {
        return plan switch
        {
            "Premium" => new Dictionary<string, bool>
            {
                { "stock_management", true },
                { "collection_workflows", true },
                { "advanced_analytics", true },
                { "priority_support", true },
                { "custom_branding", true }
            },
            "Standard" => new Dictionary<string, bool>
            {
                { "stock_management", false },
                { "collection_workflows", false },
                { "advanced_analytics", true },
                { "priority_support", false },
                { "custom_branding", true }
            },
            _ => new Dictionary<string, bool>
            {
                { "stock_management", false },
                { "collection_workflows", false },
                { "advanced_analytics", false },
                { "priority_support", false },
                { "custom_branding", false }
            }
        };
    }
}