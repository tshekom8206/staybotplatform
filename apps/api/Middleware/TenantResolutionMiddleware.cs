using Hostr.Api.Services;

namespace Hostr.Api.Middleware;

public class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantResolutionMiddleware> _logger;

    public TenantResolutionMiddleware(RequestDelegate next, ILogger<TenantResolutionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ITenantService tenantService)
    {
        var tenantContext = await ResolveTenantAsync(context, tenantService);

        if (tenantContext != null)
        {
            context.Items["TenantId"] = tenantContext.TenantId;
            context.Items["TenantSlug"] = tenantContext.TenantSlug;
            context.Items["TenantName"] = tenantContext.TenantName;
            context.Items["TenantPlan"] = tenantContext.Plan;
            context.Items["TenantThemePrimary"] = tenantContext.ThemePrimary;
            context.Items["TenantTimezone"] = tenantContext.Timezone;

            _logger.LogInformation("Tenant resolved: {TenantSlug} (ID: {TenantId})",
                tenantContext.TenantSlug, tenantContext.TenantId);
        }
        else if (RequiresTenant(context))
        {
            _logger.LogWarning("Tenant not found for request: {Host}{Path}",
                context.Request.Host, context.Request.Path);

            context.Response.StatusCode = 404;
            await context.Response.WriteAsync("Tenant not found");
            return;
        }

        await _next(context);
    }

    private async Task<TenantContext?> ResolveTenantAsync(HttpContext context, ITenantService tenantService)
    {
        // Skip tenant resolution for certain paths
        if (IsSystemPath(context.Request.Path))
        {
            return null;
        }

        // Method 1: Resolve from JWT token (for authenticated requests)
        var tenantFromJwt = await ResolveTenantFromJwtAsync(context, tenantService);
        if (tenantFromJwt != null)
        {
            return tenantFromJwt;
        }

        // Method 2: Resolve from query parameter (for auth endpoints)
        var tenantSlugFromQuery = context.Request.Query["tenantSlug"].FirstOrDefault();
        if (!string.IsNullOrEmpty(tenantSlugFromQuery))
        {
            return await tenantService.GetTenantBySlugAsync(tenantSlugFromQuery);
        }

        // Method 3: Resolve from subdomain
        var host = context.Request.Host.Host;
        if (host.Contains(".hostr.co.za"))
        {
            var subdomain = host.Split('.')[0];
            if (!string.IsNullOrEmpty(subdomain) && subdomain != "www" && subdomain != "api")
            {
                return await tenantService.GetTenantBySlugAsync(subdomain);
            }
        }

        // Method 4: Resolve from localhost with port (for development)
        if (host == "localhost" || host.StartsWith("127.0.0.1"))
        {
            // For development, try to get tenant from header
            var tenantHeader = context.Request.Headers["X-Tenant"].FirstOrDefault();
            if (!string.IsNullOrEmpty(tenantHeader))
            {
                return await tenantService.GetTenantBySlugAsync(tenantHeader);
            }
        }

        // Method 5: For WhatsApp webhooks, resolve from phone number ID
        if (context.Request.Path.StartsWithSegments("/webhook"))
        {
            // This will be handled in the webhook controller
            return null;
        }

        return null;
    }

    private static bool IsSystemPath(string path)
    {
        var systemPaths = new[]
        {
            "/health",
            "/swagger",
            "/webhook", // Will be handled separately
            "/api/webhook", // API webhooks handled separately
            "/hubs", // SignalR hubs handle tenant logic internally
            "/api/admin", // Super admin endpoints
            "/api/auth/login", // Login endpoint
            "/api/auth/accept-invite", // Invite acceptance
            "/api/auth/forgot-password", // Password reset request
            "/api/auth/verify-otp", // OTP verification
            "/api/auth/reset-password", // Password reset
            "/api/dataseed", // Data seeding endpoint (temporary for demo)
            "/api/test", // Test endpoints for debugging
            "/api/public", // Public Guest Portal endpoints (handle tenant via slug parameter)
            "/api/ga4/test", // GA4 diagnostic endpoint
            "/g" // Redirect service for WhatsApp template buttons
        };

        return systemPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase));
    }

    private static bool RequiresTenant(HttpContext context)
    {
        // Paths that don't require a tenant
        var noTenantPaths = new[]
        {
            "/health",
            "/swagger",
            "/webhook", // Webhooks handle tenant resolution internally
            "/api/webhook", // API webhooks handle tenant resolution internally
            "/hubs", // SignalR hubs handle tenant logic internally
            "/api/admin",
            "/api/auth/login", // Login endpoint
            "/api/auth/accept-invite",
            "/api/auth/forgot-password", // Password reset request
            "/api/auth/verify-otp", // OTP verification
            "/api/auth/reset-password", // Password reset
            "/api/tenant/onboard", // Tenant onboarding endpoint
            "/api/tenant/validate-slug", // Slug validation endpoint
            "/api/tenant/validate-email", // Email validation endpoint
            "/manifest.webmanifest",
            "/api/dataseed", // Data seeding endpoint (temporary for demo)
            "/api/test", // Test endpoints for debugging
            "/api/public", // Public Guest Portal endpoints (handle tenant via slug parameter)
            "/api/ga4/test", // GA4 diagnostic endpoint
            "/g" // Redirect service for WhatsApp template buttons
        };

        return !noTenantPaths.Any(p => context.Request.Path.StartsWithSegments(p, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<TenantContext?> ResolveTenantFromJwtAsync(HttpContext context, ITenantService tenantService)
    {
        try
        {
            var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            {
                return null;
            }

            var token = authHeader.Substring("Bearer ".Length).Trim();
            if (string.IsNullOrEmpty(token))
            {
                return null;
            }

            // Parse JWT token to extract claims
            var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
            var jsonToken = handler.ReadJwtToken(token);

            var tenantIdClaim = jsonToken.Claims.FirstOrDefault(c => c.Type == "tid")?.Value;
            var tenantSlugClaim = jsonToken.Claims.FirstOrDefault(c => c.Type == "tenant_slug")?.Value;

            // First try to resolve by tenant ID (most reliable)
            if (!string.IsNullOrEmpty(tenantIdClaim) && int.TryParse(tenantIdClaim, out var tenantId))
            {
                return await tenantService.GetTenantByIdAsync(tenantId);
            }

            // Fall back to slug if ID is not available
            if (!string.IsNullOrEmpty(tenantSlugClaim))
            {
                return await tenantService.GetTenantBySlugAsync(tenantSlugClaim);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to resolve tenant from JWT token");
        }

        return null;
    }
}
