using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using Hostr.Api.Data;
using Hostr.Api.Models;
using Hostr.Api.Services;

namespace Hostr.Api.Controllers;

public class StaffMemberDto
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Role { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public DateTime RoleAssignedAt { get; set; }
}

public class CreateStaffMemberRequest
{
    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required, MinLength(6)]
    public string Password { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string Role { get; set; } = string.Empty;

    [Phone, MaxLength(20)]
    public string? PhoneNumber { get; set; }

    public bool IsActive { get; set; } = true;
}

public class UpdateStaffMemberRequest
{
    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string Role { get; set; } = string.Empty;

    [Phone, MaxLength(20)]
    public string? PhoneNumber { get; set; }

    public bool IsActive { get; set; }
}

public class ChangePasswordRequest
{
    [Required, MinLength(6)]
    public string NewPassword { get; set; } = string.Empty;
}

public class StaffStatsDto
{
    public int TotalStaff { get; set; }
    public int ActiveStaff { get; set; }
    public int InactiveStaff { get; set; }
    public Dictionary<string, int> StaffByRole { get; set; } = new();
    public List<StaffActivityDto> RecentActivity { get; set; } = new();
}

public class StaffActivityDto
{
    public string StaffEmail { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class StaffController : ControllerBase
{
    private readonly HostrDbContext _context;
    private readonly UserManager<User> _userManager;
    private readonly ILogger<StaffController> _logger;
    private readonly IAuthService _authService;

    public StaffController(
        HostrDbContext context,
        UserManager<User> userManager,
        ILogger<StaffController> logger,
        IAuthService authService)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
        _authService = authService;
    }

    [HttpGet("current")]
    public async Task<ActionResult<StaffMemberDto>> GetCurrentStaffMember()
    {
        try
        {
            // Extract user ID directly from JWT claims
            var userIdClaim = HttpContext.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                           ?? HttpContext.User?.FindFirst("sub")?.Value;
            if (!int.TryParse(userIdClaim, out var currentUserId))
                return Unauthorized();

            // Try to get tenant ID from context, default to 1 if not found
            var tenantId = int.Parse(HttpContext.Items["TenantId"]?.ToString() ?? "1");

            using var scope = new TenantScope(_context, tenantId);

            var userTenant = await _context.UserTenants
                .Include(ut => ut.User)
                .FirstOrDefaultAsync(ut => ut.UserId == currentUserId && ut.TenantId == tenantId);

            if (userTenant == null)
                return NotFound(new { message = "Current user not found in this tenant" });

            var staffMember = new StaffMemberDto
            {
                Id = userTenant.User.Id,
                Email = userTenant.User.Email!,
                UserName = userTenant.User.UserName!,
                IsActive = userTenant.User.IsActive,
                CreatedAt = userTenant.User.CreatedAt,
                Role = userTenant.Role,
                PhoneNumber = userTenant.User.PhoneNumber ?? "",
                RoleAssignedAt = userTenant.CreatedAt
            };

            return Ok(staffMember);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving current staff member");
            return StatusCode(500, new { message = "Error retrieving current staff member" });
        }
    }

    [HttpGet]
    public async Task<ActionResult<object>> GetStaffMembers([FromQuery] string? role = null)
    {
        try
        {
            var tenantId = int.Parse(HttpContext.Items["TenantId"]?.ToString() ?? "0");
            if (tenantId == 0)
                return Unauthorized();

            using var scope = new TenantScope(_context, tenantId);

            var query = _context.UserTenants
                .Include(ut => ut.User)
                .Where(ut => ut.TenantId == tenantId)
                .AsQueryable();

            if (!string.IsNullOrEmpty(role))
            {
                query = query.Where(ut => ut.Role.ToLower() == role.ToLower());
            }

            var staffMembers = await query
                .OrderBy(ut => ut.User.Email)
                .Select(ut => new StaffMemberDto
                {
                    Id = ut.User.Id,
                    Email = ut.User.Email!,
                    UserName = ut.User.UserName!,
                    IsActive = ut.User.IsActive,
                    CreatedAt = ut.User.CreatedAt,
                    Role = ut.Role,
                    PhoneNumber = ut.User.PhoneNumber ?? "",
                    RoleAssignedAt = ut.CreatedAt
                })
                .ToListAsync();

            return Ok(new { staff = staffMembers });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving staff members");
            return StatusCode(500, new { message = "Error retrieving staff members" });
        }
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<StaffMemberDto>> GetStaffMember(int id)
    {
        try
        {
            var tenantId = int.Parse(HttpContext.Items["TenantId"]?.ToString() ?? "0");
            if (tenantId == 0)
                return Unauthorized();

            using var scope = new TenantScope(_context, tenantId);

            var userTenant = await _context.UserTenants
                .Include(ut => ut.User)
                .FirstOrDefaultAsync(ut => ut.UserId == id && ut.TenantId == tenantId);

            if (userTenant == null)
                return NotFound(new { message = "Staff member not found" });

            var staffMember = new StaffMemberDto
            {
                Id = userTenant.User.Id,
                Email = userTenant.User.Email!,
                UserName = userTenant.User.UserName!,
                IsActive = userTenant.User.IsActive,
                CreatedAt = userTenant.User.CreatedAt,
                Role = userTenant.Role,
                PhoneNumber = userTenant.User.PhoneNumber ?? "",
                RoleAssignedAt = userTenant.CreatedAt
            };

            return Ok(staffMember);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving staff member {StaffId}", id);
            return StatusCode(500, new { message = "Error retrieving staff member" });
        }
    }

    [HttpPost]
    public async Task<ActionResult<StaffMemberDto>> CreateStaffMember([FromBody] CreateStaffMemberRequest request)
    {
        try
        {
            var tenantId = int.Parse(HttpContext.Items["TenantId"]?.ToString() ?? "0");
            if (tenantId == 0)
                return Unauthorized();

            // Validate role
            var validRoles = new[] { "Owner", "Manager", "Agent", "SuperAdmin", "Admin" };
            if (!validRoles.Contains(request.Role))
            {
                return BadRequest(new { message = "Invalid role specified" });
            }

            // Check if user already exists
            var existingUser = await _userManager.FindByEmailAsync(request.Email);
            if (existingUser != null)
            {
                return BadRequest(new { message = "User with this email already exists" });
            }

            // Create new user
            var user = new User
            {
                UserName = request.Email,
                Email = request.Email,
                PhoneNumber = request.PhoneNumber,
                IsActive = request.IsActive,
                CreatedAt = DateTime.UtcNow
            };

            var result = await _userManager.CreateAsync(user, request.Password);
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                return BadRequest(new { message = $"Failed to create user: {errors}" });
            }

            // Add user to tenant with role
            using var scope = new TenantScope(_context, tenantId);

            var userTenant = new UserTenant
            {
                UserId = user.Id,
                TenantId = tenantId,
                Role = request.Role,
                CreatedAt = DateTime.UtcNow
            };

            _context.UserTenants.Add(userTenant);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Staff member created: {Email} with role {Role} for tenant {TenantId}",
                request.Email, request.Role, tenantId);

            var staffMember = new StaffMemberDto
            {
                Id = user.Id,
                Email = user.Email,
                UserName = user.UserName!,
                IsActive = user.IsActive,
                CreatedAt = user.CreatedAt,
                Role = userTenant.Role,
                PhoneNumber = user.PhoneNumber ?? "",
                RoleAssignedAt = userTenant.CreatedAt
            };

            return CreatedAtAction(nameof(GetStaffMember), new { id = user.Id }, staffMember);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating staff member");
            return StatusCode(500, new { message = "Error creating staff member" });
        }
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<StaffMemberDto>> UpdateStaffMember(int id, [FromBody] UpdateStaffMemberRequest request)
    {
        try
        {
            var tenantId = int.Parse(HttpContext.Items["TenantId"]?.ToString() ?? "0");
            if (tenantId == 0)
                return Unauthorized();

            // Validate role
            var validRoles = new[] { "Owner", "Manager", "Agent", "SuperAdmin", "Admin" };
            if (!validRoles.Contains(request.Role))
            {
                return BadRequest(new { message = "Invalid role specified" });
            }

            using var scope = new TenantScope(_context, tenantId);

            var userTenant = await _context.UserTenants
                .Include(ut => ut.User)
                .FirstOrDefaultAsync(ut => ut.UserId == id && ut.TenantId == tenantId);

            if (userTenant == null)
                return NotFound(new { message = "Staff member not found" });

            var user = userTenant.User;

            // Update user details
            user.Email = request.Email;
            user.UserName = request.Email;
            user.PhoneNumber = request.PhoneNumber;
            user.IsActive = request.IsActive;

            // Ensure DateTime fields have UTC kind to avoid PostgreSQL timezone errors
            if (user.CreatedAt.Kind == DateTimeKind.Unspecified)
            {
                user.CreatedAt = DateTime.SpecifyKind(user.CreatedAt, DateTimeKind.Utc);
            }
            if (userTenant.CreatedAt.Kind == DateTimeKind.Unspecified)
            {
                userTenant.CreatedAt = DateTime.SpecifyKind(userTenant.CreatedAt, DateTimeKind.Utc);
            }

            // Update role if changed
            if (userTenant.Role != request.Role)
            {
                userTenant.Role = request.Role;
                _logger.LogInformation("Role changed for user {UserId} from {OldRole} to {NewRole}",
                    id, userTenant.Role, request.Role);
            }

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                var errors = string.Join(", ", updateResult.Errors.Select(e => e.Description));
                return BadRequest(new { message = $"Failed to update user: {errors}" });
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Staff member updated: {Email} for tenant {TenantId}",
                request.Email, tenantId);

            var staffMember = new StaffMemberDto
            {
                Id = user.Id,
                Email = user.Email!,
                UserName = user.UserName!,
                IsActive = user.IsActive,
                CreatedAt = user.CreatedAt,
                Role = userTenant.Role,
                PhoneNumber = user.PhoneNumber ?? "",
                RoleAssignedAt = userTenant.CreatedAt
            };

            return Ok(staffMember);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating staff member {StaffId}", id);
            return StatusCode(500, new { message = "Error updating staff member" });
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<ActionResult> DeleteStaffMember(int id)
    {
        try
        {
            var tenantId = int.Parse(HttpContext.Items["TenantId"]?.ToString() ?? "0");
            if (tenantId == 0)
                return Unauthorized();

            using var scope = new TenantScope(_context, tenantId);

            var userTenant = await _context.UserTenants
                .Include(ut => ut.User)
                .FirstOrDefaultAsync(ut => ut.UserId == id && ut.TenantId == tenantId);

            if (userTenant == null)
                return NotFound(new { message = "Staff member not found" });

            // Don't allow deletion of the last owner
            if (userTenant.Role == "Owner")
            {
                var ownerCount = await _context.UserTenants
                    .CountAsync(ut => ut.TenantId == tenantId && ut.Role == "Owner");

                if (ownerCount <= 1)
                {
                    return BadRequest(new { message = "Cannot delete the last owner of the tenant" });
                }
            }

            // Remove user from tenant
            _context.UserTenants.Remove(userTenant);

            // Check if user belongs to other tenants, if not, delete the user
            var otherTenants = await _context.UserTenants
                .Where(ut => ut.UserId == id && ut.TenantId != tenantId)
                .AnyAsync();

            if (!otherTenants)
            {
                await _userManager.DeleteAsync(userTenant.User);
                _logger.LogInformation("User {UserId} deleted completely as they had no other tenant associations", id);
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Staff member removed: {Email} from tenant {TenantId}",
                userTenant.User.Email, tenantId);

            return Ok(new { message = "Staff member deleted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting staff member {StaffId}", id);
            return StatusCode(500, new { message = "Error deleting staff member" });
        }
    }

    [HttpPost("{id:int}/change-password")]
    public async Task<ActionResult> ChangePassword(int id, [FromBody] ChangePasswordRequest request)
    {
        try
        {
            var tenantId = int.Parse(HttpContext.Items["TenantId"]?.ToString() ?? "0");
            if (tenantId == 0)
                return Unauthorized();

            using var scope = new TenantScope(_context, tenantId);

            var userTenant = await _context.UserTenants
                .Include(ut => ut.User)
                .FirstOrDefaultAsync(ut => ut.UserId == id && ut.TenantId == tenantId);

            if (userTenant == null)
                return NotFound(new { message = "Staff member not found" });

            var user = userTenant.User;

            // Remove old password and set new one
            var removeResult = await _userManager.RemovePasswordAsync(user);
            if (!removeResult.Succeeded)
            {
                return BadRequest(new { message = "Failed to reset password" });
            }

            var addResult = await _userManager.AddPasswordAsync(user, request.NewPassword);
            if (!addResult.Succeeded)
            {
                var errors = string.Join(", ", addResult.Errors.Select(e => e.Description));
                return BadRequest(new { message = $"Failed to set new password: {errors}" });
            }

            _logger.LogInformation("Password changed for staff member {UserId} by admin", id);

            return Ok(new { message = "Password changed successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing password for staff member {StaffId}", id);
            return StatusCode(500, new { message = "Error changing password" });
        }
    }

    [HttpGet("roles")]
    public ActionResult<object> GetAvailableRoles()
    {
        var roles = new[]
        {
            new { value = "Owner", label = "Owner", description = "Full system access and billing management" },
            new { value = "Manager", label = "Manager", description = "Hotel operations and staff management" },
            new { value = "Agent", label = "Agent", description = "Guest services and task management" },
            new { value = "SuperAdmin", label = "Super Admin", description = "System administration (Hostr staff only)" }
        };

        return Ok(new { roles });
    }

    [HttpGet("stats")]
    public async Task<ActionResult<StaffStatsDto>> GetStaffStats()
    {
        try
        {
            var tenantId = int.Parse(HttpContext.Items["TenantId"]?.ToString() ?? "0");
            if (tenantId == 0)
                return Unauthorized();

            using var scope = new TenantScope(_context, tenantId);

            var staffMembers = await _context.UserTenants
                .Include(ut => ut.User)
                .Where(ut => ut.TenantId == tenantId)
                .ToListAsync();

            var stats = new StaffStatsDto
            {
                TotalStaff = staffMembers.Count,
                ActiveStaff = staffMembers.Count(ut => ut.User.IsActive),
                InactiveStaff = staffMembers.Count(ut => !ut.User.IsActive),
                StaffByRole = staffMembers
                    .GroupBy(ut => ut.Role)
                    .ToDictionary(g => g.Key, g => g.Count())
            };

            // Add recent activity (simplified - could be enhanced with actual activity tracking)
            stats.RecentActivity = staffMembers
                .OrderByDescending(ut => ut.CreatedAt)
                .Take(5)
                .Select(ut => new StaffActivityDto
                {
                    StaffEmail = ut.User.Email!,
                    Action = "Added to team",
                    Timestamp = ut.CreatedAt
                })
                .ToList();

            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving staff stats");
            return StatusCode(500, new { message = "Error retrieving staff statistics" });
        }
    }
}