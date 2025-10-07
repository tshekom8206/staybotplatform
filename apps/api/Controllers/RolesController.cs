using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using Hostr.Api.Data;
using Hostr.Api.Models;
using Hostr.Api.Services;

namespace Hostr.Api.Controllers;

public class RoleDto
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> Permissions { get; set; } = new();
    public int UserCount { get; set; }
    public bool IsBuiltIn { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class PermissionDto
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class PermissionCategoryDto
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public List<PermissionDto> Permissions { get; set; } = new();
}

public class CreateRoleRequest
{
    [Required, MaxLength(50)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    public List<string> Permissions { get; set; } = new();
}

public class UpdateRoleRequest
{
    [Required, MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    public List<string> Permissions { get; set; } = new();
}

public class RoleStatsDto
{
    public int TotalRoles { get; set; }
    public int BuiltInRoles { get; set; }
    public int CustomRoles { get; set; }
    public Dictionary<string, int> UsersByRole { get; set; } = new();
    public List<PermissionUsageDto> MostUsedPermissions { get; set; } = new();
}

public class PermissionUsageDto
{
    public string Permission { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int RoleCount { get; set; }
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RolesController : ControllerBase
{
    private readonly HostrDbContext _context;
    private readonly ILogger<RolesController> _logger;
    private readonly IAuthService _authService;

    // Built-in role definitions with their permissions
    private static readonly Dictionary<string, RoleDto> BuiltInRoles = new()
    {
        ["Owner"] = new RoleDto
        {
            Name = "Owner",
            DisplayName = "Owner",
            Description = "Full system access and billing management",
            IsBuiltIn = true,
            Permissions = new List<string>
            {
                "users.view", "users.create", "users.edit", "users.delete",
                "roles.view", "roles.create", "roles.edit", "roles.delete",
                "guests.view", "guests.edit", "guests.delete",
                "tasks.view", "tasks.create", "tasks.edit", "tasks.delete", "tasks.assign",
                "broadcast.view", "broadcast.send", "broadcast.emergency",
                "configuration.view", "configuration.edit",
                "reports.view", "reports.export",
                "billing.view", "billing.manage",
                "audit.view"
            }
        },
        ["Manager"] = new RoleDto
        {
            Name = "Manager",
            DisplayName = "Manager",
            Description = "Hotel operations and staff management",
            IsBuiltIn = true,
            Permissions = new List<string>
            {
                "users.view", "users.create", "users.edit",
                "guests.view", "guests.edit",
                "tasks.view", "tasks.create", "tasks.edit", "tasks.delete", "tasks.assign",
                "broadcast.view", "broadcast.send", "broadcast.emergency",
                "configuration.view", "configuration.edit",
                "reports.view"
            }
        },
        ["Agent"] = new RoleDto
        {
            Name = "Agent",
            DisplayName = "Agent",
            Description = "Guest services and task management",
            IsBuiltIn = true,
            Permissions = new List<string>
            {
                "users.view",
                "guests.view", "guests.edit",
                "tasks.view", "tasks.create", "tasks.edit",
                "broadcast.view",
                "configuration.view"
            }
        },
        ["SuperAdmin"] = new RoleDto
        {
            Name = "SuperAdmin",
            DisplayName = "Super Admin",
            Description = "System administration (Hostr staff only)",
            IsBuiltIn = true,
            Permissions = new List<string>
            {
                "system.admin", "system.debug", "system.monitor",
                "users.view", "users.create", "users.edit", "users.delete",
                "roles.view", "roles.create", "roles.edit", "roles.delete",
                "guests.view", "guests.edit", "guests.delete",
                "tasks.view", "tasks.create", "tasks.edit", "tasks.delete", "tasks.assign",
                "broadcast.view", "broadcast.send", "broadcast.emergency",
                "configuration.view", "configuration.edit",
                "reports.view", "reports.export",
                "billing.view", "billing.manage",
                "audit.view", "tenant.manage"
            }
        }
    };

    // Available permissions grouped by category
    private static readonly List<PermissionCategoryDto> PermissionCategories = new()
    {
        new PermissionCategoryDto
        {
            Name = "users",
            DisplayName = "User Management",
            Permissions = new List<PermissionDto>
            {
                new() { Name = "users.view", DisplayName = "View Users", Category = "users", Description = "View staff and user information" },
                new() { Name = "users.create", DisplayName = "Create Users", Category = "users", Description = "Add new staff members" },
                new() { Name = "users.edit", DisplayName = "Edit Users", Category = "users", Description = "Modify user information and roles" },
                new() { Name = "users.delete", DisplayName = "Delete Users", Category = "users", Description = "Remove users from the system" }
            }
        },
        new PermissionCategoryDto
        {
            Name = "roles",
            DisplayName = "Role & Permission Management",
            Permissions = new List<PermissionDto>
            {
                new() { Name = "roles.view", DisplayName = "View Roles", Category = "roles", Description = "View role definitions and permissions" },
                new() { Name = "roles.create", DisplayName = "Create Roles", Category = "roles", Description = "Create custom roles" },
                new() { Name = "roles.edit", DisplayName = "Edit Roles", Category = "roles", Description = "Modify role permissions" },
                new() { Name = "roles.delete", DisplayName = "Delete Roles", Category = "roles", Description = "Remove custom roles" }
            }
        },
        new PermissionCategoryDto
        {
            Name = "guests",
            DisplayName = "Guest Management",
            Permissions = new List<PermissionDto>
            {
                new() { Name = "guests.view", DisplayName = "View Guests", Category = "guests", Description = "View guest information and conversations" },
                new() { Name = "guests.edit", DisplayName = "Edit Guests", Category = "guests", Description = "Modify guest information" },
                new() { Name = "guests.delete", DisplayName = "Delete Guests", Category = "guests", Description = "Remove guest records" }
            }
        },
        new PermissionCategoryDto
        {
            Name = "tasks",
            DisplayName = "Task Management",
            Permissions = new List<PermissionDto>
            {
                new() { Name = "tasks.view", DisplayName = "View Tasks", Category = "tasks", Description = "View tasks and requests" },
                new() { Name = "tasks.create", DisplayName = "Create Tasks", Category = "tasks", Description = "Create new tasks" },
                new() { Name = "tasks.edit", DisplayName = "Edit Tasks", Category = "tasks", Description = "Modify task details" },
                new() { Name = "tasks.delete", DisplayName = "Delete Tasks", Category = "tasks", Description = "Remove tasks" },
                new() { Name = "tasks.assign", DisplayName = "Assign Tasks", Category = "tasks", Description = "Assign tasks to staff members" }
            }
        },
        new PermissionCategoryDto
        {
            Name = "broadcast",
            DisplayName = "Broadcasting & Communications",
            Permissions = new List<PermissionDto>
            {
                new() { Name = "broadcast.view", DisplayName = "View Broadcasts", Category = "broadcast", Description = "View broadcast history" },
                new() { Name = "broadcast.send", DisplayName = "Send Broadcasts", Category = "broadcast", Description = "Send messages to guests" },
                new() { Name = "broadcast.emergency", DisplayName = "Emergency Broadcasts", Category = "broadcast", Description = "Send emergency alerts" }
            }
        },
        new PermissionCategoryDto
        {
            Name = "configuration",
            DisplayName = "System Configuration",
            Permissions = new List<PermissionDto>
            {
                new() { Name = "configuration.view", DisplayName = "View Configuration", Category = "configuration", Description = "View system settings" },
                new() { Name = "configuration.edit", DisplayName = "Edit Configuration", Category = "configuration", Description = "Modify system settings" }
            }
        },
        new PermissionCategoryDto
        {
            Name = "reports",
            DisplayName = "Reports & Analytics",
            Permissions = new List<PermissionDto>
            {
                new() { Name = "reports.view", DisplayName = "View Reports", Category = "reports", Description = "View analytics and reports" },
                new() { Name = "reports.export", DisplayName = "Export Reports", Category = "reports", Description = "Export report data" }
            }
        },
        new PermissionCategoryDto
        {
            Name = "billing",
            DisplayName = "Billing & Subscription",
            Permissions = new List<PermissionDto>
            {
                new() { Name = "billing.view", DisplayName = "View Billing", Category = "billing", Description = "View billing information" },
                new() { Name = "billing.manage", DisplayName = "Manage Billing", Category = "billing", Description = "Manage subscription and payment" }
            }
        },
        new PermissionCategoryDto
        {
            Name = "system",
            DisplayName = "System Administration",
            Permissions = new List<PermissionDto>
            {
                new() { Name = "system.admin", DisplayName = "System Admin", Category = "system", Description = "Full system administration access" },
                new() { Name = "system.debug", DisplayName = "Debug Access", Category = "system", Description = "Access debug information" },
                new() { Name = "system.monitor", DisplayName = "System Monitoring", Category = "system", Description = "Monitor system performance" },
                new() { Name = "audit.view", DisplayName = "Audit Logs", Category = "system", Description = "View system audit logs" },
                new() { Name = "tenant.manage", DisplayName = "Tenant Management", Category = "system", Description = "Manage tenant settings" }
            }
        }
    };

    public RolesController(
        HostrDbContext context,
        ILogger<RolesController> logger,
        IAuthService authService)
    {
        _context = context;
        _logger = logger;
        _authService = authService;
    }

    [HttpGet]
    public async Task<ActionResult<object>> GetRoles()
    {
        try
        {
            var tenantId = int.Parse(HttpContext.Items["TenantId"]?.ToString() ?? "0");
            if (tenantId == 0)
                return Unauthorized();

            using var scope = new TenantScope(_context, tenantId);

            // Get user counts for each role
            var userCounts = await _context.UserTenants
                .Where(ut => ut.TenantId == tenantId)
                .GroupBy(ut => ut.Role)
                .ToDictionaryAsync(g => g.Key, g => g.Count());

            // Return built-in roles with user counts
            var roles = BuiltInRoles.Values.Select(role => new RoleDto
            {
                Name = role.Name,
                DisplayName = role.DisplayName,
                Description = role.Description,
                Permissions = role.Permissions,
                UserCount = userCounts.GetValueOrDefault(role.Name, 0),
                IsBuiltIn = role.IsBuiltIn,
                CreatedAt = DateTime.UtcNow // Built-in roles don't have creation dates
            }).ToList();

            return Ok(new { roles });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving roles");
            return StatusCode(500, new { message = "Error retrieving roles" });
        }
    }

    [HttpGet("{roleName}")]
    public ActionResult<RoleDto> GetRole(string roleName)
    {
        try
        {
            if (!BuiltInRoles.TryGetValue(roleName, out var role))
            {
                return NotFound(new { message = "Role not found" });
            }

            return Ok(role);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving role {RoleName}", roleName);
            return StatusCode(500, new { message = "Error retrieving role" });
        }
    }

    [HttpGet("permissions")]
    public ActionResult<object> GetPermissions()
    {
        try
        {
            var allPermissions = PermissionCategories
                .SelectMany(c => c.Permissions)
                .ToList();

            return Ok(new
            {
                categories = PermissionCategories,
                permissions = allPermissions
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving permissions");
            return StatusCode(500, new { message = "Error retrieving permissions" });
        }
    }

    [HttpGet("permissions/{roleName}")]
    public ActionResult<object> GetRolePermissions(string roleName)
    {
        try
        {
            if (!BuiltInRoles.TryGetValue(roleName, out var role))
            {
                return NotFound(new { message = "Role not found" });
            }

            var rolePermissions = role.Permissions;
            var categorizedPermissions = PermissionCategories.Select(category => new
            {
                category.Name,
                category.DisplayName,
                Permissions = category.Permissions.Select(permission => new
                {
                    permission.Name,
                    permission.DisplayName,
                    permission.Description,
                    HasPermission = rolePermissions.Contains(permission.Name)
                }).ToList()
            }).ToList();

            return Ok(new
            {
                role = role.Name,
                displayName = role.DisplayName,
                permissions = categorizedPermissions
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving role permissions for {RoleName}", roleName);
            return StatusCode(500, new { message = "Error retrieving role permissions" });
        }
    }

    [HttpGet("stats")]
    public async Task<ActionResult<RoleStatsDto>> GetRoleStats()
    {
        try
        {
            var tenantId = int.Parse(HttpContext.Items["TenantId"]?.ToString() ?? "0");
            if (tenantId == 0)
                return Unauthorized();

            using var scope = new TenantScope(_context, tenantId);

            var usersByRole = await _context.UserTenants
                .Where(ut => ut.TenantId == tenantId)
                .GroupBy(ut => ut.Role)
                .ToDictionaryAsync(g => g.Key, g => g.Count());

            // Calculate permission usage across roles
            var permissionUsage = new Dictionary<string, int>();
            foreach (var role in BuiltInRoles.Values)
            {
                foreach (var permission in role.Permissions)
                {
                    permissionUsage[permission] = permissionUsage.GetValueOrDefault(permission, 0) + 1;
                }
            }

            var allPermissions = PermissionCategories
                .SelectMany(c => c.Permissions)
                .ToDictionary(p => p.Name, p => p);

            var mostUsedPermissions = permissionUsage
                .OrderByDescending(kvp => kvp.Value)
                .Take(10)
                .Select(kvp => new PermissionUsageDto
                {
                    Permission = kvp.Key,
                    DisplayName = allPermissions.GetValueOrDefault(kvp.Key)?.DisplayName ?? kvp.Key,
                    RoleCount = kvp.Value
                })
                .ToList();

            var stats = new RoleStatsDto
            {
                TotalRoles = BuiltInRoles.Count,
                BuiltInRoles = BuiltInRoles.Count,
                CustomRoles = 0, // No custom roles in current implementation
                UsersByRole = usersByRole,
                MostUsedPermissions = mostUsedPermissions
            };

            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving role stats");
            return StatusCode(500, new { message = "Error retrieving role statistics" });
        }
    }

    [HttpPost("validate-permissions")]
    public ActionResult<object> ValidatePermissions([FromBody] List<string> permissions)
    {
        try
        {
            var allValidPermissions = PermissionCategories
                .SelectMany(c => c.Permissions)
                .Select(p => p.Name)
                .ToHashSet();

            var validPermissions = permissions.Where(p => allValidPermissions.Contains(p)).ToList();
            var invalidPermissions = permissions.Except(validPermissions).ToList();

            return Ok(new
            {
                valid = validPermissions,
                invalid = invalidPermissions,
                allValid = invalidPermissions.Count == 0
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating permissions");
            return StatusCode(500, new { message = "Error validating permissions" });
        }
    }

    // Note: Custom role creation would require additional database models
    // This is a simplified implementation focusing on built-in roles
    [HttpPost]
    public ActionResult<object> CreateRole([FromBody] CreateRoleRequest request)
    {
        return BadRequest(new { message = "Custom role creation is not supported in this version. Please use the built-in roles: Owner, Manager, Agent, SuperAdmin" });
    }

    [HttpPut("{roleName}")]
    public ActionResult<object> UpdateRole(string roleName, [FromBody] UpdateRoleRequest request)
    {
        return BadRequest(new { message = "Built-in roles cannot be modified. Role permissions are fixed for security reasons." });
    }

    [HttpDelete("{roleName}")]
    public ActionResult DeleteRole(string roleName)
    {
        return BadRequest(new { message = "Built-in roles cannot be deleted. Only custom roles can be removed." });
    }
}