using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Hostr.Contracts.DTOs.Auth;
using Hostr.Contracts.DTOs.Common;
using Hostr.Api.Services;

namespace Hostr.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ITenantService _tenantService;
    private readonly IAuditService _auditService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, ITenantService tenantService, IAuditService auditService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _tenantService = tenantService;
        _auditService = auditService;
        _logger = logger;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, [FromQuery] string? tenantSlug = null)
    {
        try
        {
            var (success, token, user, tenant) = await _authService.LoginAsync(request.Email, request.Password, tenantSlug);

            if (!success || user == null || tenant == null)
            {
                return Unauthorized(new ApiResponse<object>
                {
                    Success = false,
                    Error = "Invalid email or password"
                });
            }

            var response = new LoginResponse
            {
                Token = token,
                User = new UserDto
                {
                    Id = user.Id,
                    Email = user.Email!,
                    IsActive = user.IsActive,
                    CreatedAt = user.CreatedAt
                },
                Tenant = new TenantDto
                {
                    Id = tenant.Id,
                    Slug = tenant.Slug,
                    Name = tenant.Name,
                    Plan = tenant.Plan,
                    ThemePrimary = tenant.ThemePrimary,
                    Timezone = tenant.Timezone,
                    Features = _tenantService.GetFeatures(tenant.Plan)
                }
            };

            // Log the login action
            await _auditService.LogAsync(tenant.Id, user.Id, "login", "User", user.Id);

            return Ok(new ApiResponse<LoginResponse>
            {
                Success = true,
                Data = response
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login error for email: {Email}", request.Email);
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Error = "Internal server error"
            });
        }
    }

    [HttpPost("invite")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> InviteUser([FromBody] InviteRequest request)
    {
        try
        {
            var tenantId = int.Parse(HttpContext.Items["TenantId"]?.ToString() ?? "0");
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

            var (success, message) = await _authService.InviteUserAsync(request.Email, request.Role, tenantId, userId);

            return Ok(new ApiResponse<object>
            {
                Success = success,
                Data = new { Message = message }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Invite error");
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Error = "Internal server error"
            });
        }
    }

    [HttpPost("accept-invite")]
    public async Task<IActionResult> AcceptInvite([FromBody] AcceptInviteRequest request)
    {
        try
        {
            var (success, token, user, tenant) = await _authService.AcceptInviteAsync(request.Token, request.Password);

            if (!success || user == null || tenant == null)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Error = "Invalid or expired invite token"
                });
            }

            var response = new LoginResponse
            {
                Token = token,
                User = new UserDto
                {
                    Id = user.Id,
                    Email = user.Email!,
                    IsActive = user.IsActive,
                    CreatedAt = user.CreatedAt
                },
                Tenant = new TenantDto
                {
                    Id = tenant.Id,
                    Slug = tenant.Slug,
                    Name = tenant.Name,
                    Plan = tenant.Plan,
                    ThemePrimary = tenant.ThemePrimary,
                    Timezone = tenant.Timezone,
                    Features = _tenantService.GetFeatures(tenant.Plan)
                }
            };

            return Ok(new ApiResponse<LoginResponse>
            {
                Success = true,
                Data = response
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Accept invite error");
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Error = "Internal server error"
            });
        }
    }

    [HttpPost("switch-tenant")]
    [Authorize]
    public async Task<IActionResult> SwitchTenant([FromBody] SwitchTenantRequest request)
    {
        try
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

            var (success, token, user, tenant) = await _authService.SwitchTenantAsync(userId, request.TenantSlug);

            if (!success || user == null || tenant == null)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Error = "Unable to switch to the specified tenant"
                });
            }

            var response = new LoginResponse
            {
                Token = token,
                User = new UserDto
                {
                    Id = user.Id,
                    Email = user.Email!,
                    IsActive = user.IsActive,
                    CreatedAt = user.CreatedAt
                },
                Tenant = new TenantDto
                {
                    Id = tenant.Id,
                    Slug = tenant.Slug,
                    Name = tenant.Name,
                    Plan = tenant.Plan,
                    ThemePrimary = tenant.ThemePrimary,
                    Timezone = tenant.Timezone,
                    Features = _tenantService.GetFeatures(tenant.Plan)
                }
            };

            return Ok(new ApiResponse<LoginResponse>
            {
                Success = true,
                Data = response
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Switch tenant error");
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Error = "Internal server error"
            });
        }
    }

    //need to add a background job to delete old otps

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        try
        {
            var (success, message) = await _authService.ForgotPasswordAsync(request.Email);

            return Ok(new ApiResponse<object>
            {
                Success = true,
                Data = new { Message = message }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Forgot password error for email: {Email}", request.Email);
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Error = "Internal server error"
            });
        }
    }

    [HttpPost("verify-otp")]
    public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest request)
    {
        try
        {
            var (success, message) = await _authService.VerifyOtpAsync(request.Email, request.Otp);

            if (!success)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Error = message
                });
            }

            return Ok(new ApiResponse<object>
            {
                Success = true,
                Data = new { Message = message }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Verify OTP error for email: {Email}", request.Email);
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Error = "Internal server error"
            });
        }
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        try
        {
            var (success, message) = await _authService.ResetPasswordAsync(request.Email, request.Otp, request.NewPassword);

            if (!success)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Error = message
                });
            }

            return Ok(new ApiResponse<object>
            {
                Success = true,
                Data = new { Message = message }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Reset password error for email: {Email}", request.Email);
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Error = "Internal server error"
            });
        }
    }
}
