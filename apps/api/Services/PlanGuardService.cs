using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Hostr.Api.Services;

public interface IPlanGuardService
{
    bool IsPremium(string plan);
    bool IsStandard(string plan);
    bool HasFeature(string plan, string feature);
    IActionResult? CheckPremiumAccess(HttpContext context);
}

public class PlanGuardService : IPlanGuardService
{
    public bool IsPremium(string plan)
    {
        return plan.Equals("Premium", StringComparison.OrdinalIgnoreCase);
    }

    public bool IsStandard(string plan)
    {
        return plan.Equals("Standard", StringComparison.OrdinalIgnoreCase) || IsPremium(plan);
    }

    public bool HasFeature(string plan, string feature)
    {
        var features = GetPlanFeatures(plan);
        return features.GetValueOrDefault(feature, false);
    }

    public IActionResult? CheckPremiumAccess(HttpContext context)
    {
        var tenantPlan = context.Items["TenantPlan"]?.ToString() ?? "Basic";
        
        if (!IsPremium(tenantPlan))
        {
            return new ObjectResult(new 
            { 
                code = "plan_required", 
                message = "This feature requires a Premium plan subscription.",
                plan = "Premium",
                current_plan = tenantPlan 
            })
            {
                StatusCode = 403
            };
        }

        return null;
    }

    private Dictionary<string, bool> GetPlanFeatures(string plan)
    {
        return plan.ToLowerInvariant() switch
        {
            "premium" => new Dictionary<string, bool>
            {
                { "stock_management", true },
                { "collection_workflows", true },
                { "advanced_analytics", true },
                { "priority_support", true },
                { "custom_branding", true },
                { "unlimited_faqs", true },
                { "webhook_endpoints", true }
            },
            "standard" => new Dictionary<string, bool>
            {
                { "stock_management", false },
                { "collection_workflows", false },
                { "advanced_analytics", true },
                { "priority_support", false },
                { "custom_branding", true },
                { "unlimited_faqs", true },
                { "webhook_endpoints", false }
            },
            _ => new Dictionary<string, bool>
            {
                { "stock_management", false },
                { "collection_workflows", false },
                { "advanced_analytics", false },
                { "priority_support", false },
                { "custom_branding", false },
                { "unlimited_faqs", false },
                { "webhook_endpoints", false }
            }
        };
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequiresPremiumAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var planGuard = context.HttpContext.RequestServices.GetRequiredService<IPlanGuardService>();
        var result = planGuard.CheckPremiumAccess(context.HttpContext);
        
        if (result != null)
        {
            context.Result = result;
        }
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequiresFeatureAttribute : ActionFilterAttribute
{
    private readonly string _feature;

    public RequiresFeatureAttribute(string feature)
    {
        _feature = feature;
    }

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var planGuard = context.HttpContext.RequestServices.GetRequiredService<IPlanGuardService>();
        var tenantPlan = context.HttpContext.Items["TenantPlan"]?.ToString() ?? "Basic";
        
        if (!planGuard.HasFeature(tenantPlan, _feature))
        {
            context.Result = new ObjectResult(new 
            { 
                code = "feature_not_available", 
                message = $"The '{_feature}' feature is not available in your current plan.",
                feature = _feature,
                current_plan = tenantPlan 
            })
            {
                StatusCode = 403
            };
        }
    }
}