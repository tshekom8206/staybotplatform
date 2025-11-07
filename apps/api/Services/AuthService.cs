using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Hostr.Api.Models;
using Hostr.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Hostr.Api.Services;

public interface IAuthService
{
    Task<(bool Success, string Token, User? User, Tenant? Tenant)> LoginAsync(string email, string password, string? tenantSlug = null);
    Task<(bool Success, string Message)> InviteUserAsync(string email, string role, int tenantId, int invitedBy);
    Task<(bool Success, string Token, User? User, Tenant? Tenant)> AcceptInviteAsync(string inviteToken, string password);
    Task<(bool Success, string Token, User? User, Tenant? Tenant)> SwitchTenantAsync(int userId, string tenantSlug);
    Task<(bool Success, string Message)> ForgotPasswordAsync(string email);
    Task<(bool Success, string Message)> VerifyOtpAsync(string email, string otp);
    Task<(bool Success, string Message)> ResetPasswordAsync(string email, string otp, string newPassword);
    string GenerateJwtToken(User user, Tenant tenant, string role);
    int? GetCurrentUserId(HttpContext httpContext);
}

public class AuthService : IAuthService
{
    private readonly UserManager<User> _userManager;
    private readonly HostrDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthService> _logger;
    private readonly IEmailService _emailService;

    public AuthService(
        UserManager<User> userManager,
        HostrDbContext context,
        IConfiguration configuration,
        ILogger<AuthService> logger,
        IEmailService emailService)
    {
        _userManager = userManager;
        _context = context;
        _configuration = configuration;
        _logger = logger;
        _emailService = emailService;
    }

    public async Task<(bool Success, string Token, User? User, Tenant? Tenant)> LoginAsync(string email, string password, string? tenantSlug = null)
    {
        try
        {
            _logger.LogInformation("Login attempt for email: {Email}", email);

            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                _logger.LogWarning("User not found for email: {Email}", email);
                return (false, string.Empty, null, null);
            }

            if (!user.IsActive)
            {
                _logger.LogWarning("User account is inactive for email: {Email}", email);
                return (false, string.Empty, null, null);
            }

            _logger.LogInformation("Found user: {UserId}, checking password...", user.Id);

            var isValidPassword = await _userManager.CheckPasswordAsync(user, password);
            _logger.LogInformation("Password validation result for {Email}: {IsValid}", email, isValidPassword);

            if (!isValidPassword)
            {
                _logger.LogWarning("Invalid password for email: {Email}", email);
                return (false, string.Empty, null, null);
            }

            // Get user tenants
            var userTenants = await _context.UserTenants
                .Include(ut => ut.Tenant)
                .Where(ut => ut.UserId == user.Id && ut.Tenant.Status == "Active")
                .ToListAsync();

            if (!userTenants.Any())
            {
                return (false, string.Empty, null, null);
            }

            // Select tenant
            UserTenant selectedUserTenant;
            if (!string.IsNullOrEmpty(tenantSlug))
            {
                selectedUserTenant = userTenants.FirstOrDefault(ut => ut.Tenant.Slug == tenantSlug);
                if (selectedUserTenant == null)
                {
                    return (false, string.Empty, null, null);
                }
            }
            else
            {
                // Default to first tenant
                selectedUserTenant = userTenants.First();
            }

            var token = GenerateJwtToken(user, selectedUserTenant.Tenant, selectedUserTenant.Role);
            return (true, token, user, selectedUserTenant.Tenant);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login failed for email: {Email}", email);
            return (false, string.Empty, null, null);
        }
    }

    public async Task<(bool Success, string Message)> InviteUserAsync(string email, string role, int tenantId, int invitedBy)
    {
        try
        {
            // Check if user already exists
            var existingUser = await _userManager.FindByEmailAsync(email);
            if (existingUser != null)
            {
                // Check if already member of this tenant
                var existingMembership = await _context.UserTenants
                    .AnyAsync(ut => ut.UserId == existingUser.Id && ut.TenantId == tenantId);

                if (existingMembership)
                {
                    return (false, "User is already a member of this tenant");
                }

                // Add to tenant
                var userTenant = new UserTenant
                {
                    UserId = existingUser.Id,
                    TenantId = tenantId,
                    Role = role
                };

                _context.UserTenants.Add(userTenant);
                await _context.SaveChangesAsync();

                // Send notification email to existing user
                var userTenantInfo = await _context.Tenants.FindAsync(tenantId);
                if (userTenantInfo != null)
                {
                    var resetToken = await _userManager.GeneratePasswordResetTokenAsync(existingUser);
                    var resetUrl = $"{_configuration["AppUrl"]}/auth/reset-password?token={Uri.EscapeDataString(resetToken)}&email={Uri.EscapeDataString(email)}";

                    await _emailService.SendPasswordResetEmailAsync(
                        email,
                        existingUser.UserName ?? existingUser.Email!,
                        resetToken,
                        resetUrl);
                }

                return (true, "User added to tenant successfully");
            }

            // Create new user
            var user = new User
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(user);
            if (!result.Succeeded)
            {
                return (false, string.Join(", ", result.Errors.Select(e => e.Description)));
            }

            // Add to tenant
            var newUserTenant = new UserTenant
            {
                UserId = user.Id,
                TenantId = tenantId,
                Role = role
            };

            _context.UserTenants.Add(newUserTenant);
            await _context.SaveChangesAsync();

            // Generate invite token
            var inviteToken = await _userManager.GeneratePasswordResetTokenAsync(user);

            // Get tenant information for email
            var tenant = await _context.Tenants.FindAsync(tenantId);
            if (tenant != null)
            {
                // Generate invite URL with token
                var inviteUrl = $"{_configuration["AppUrl"]}/auth/setup-password?token={Uri.EscapeDataString(inviteToken)}&email={Uri.EscapeDataString(email)}";

                // Send invite email
                var emailSent = await _emailService.SendInviteEmailAsync(
                    email,
                    tenant.Name,
                    inviteToken,
                    inviteUrl,
                    role);

                if (!emailSent)
                {
                    _logger.LogWarning("Failed to send invite email to {Email}, but user was created", email);
                }
            }

            _logger.LogInformation("Invite sent to {Email} for tenant {TenantId}", email, tenantId);

            return (true, "Invitation sent successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Invite failed for email: {Email}", email);
            return (false, "Failed to send invitation");
        }
    }

    public async Task<(bool Success, string Token, User? User, Tenant? Tenant)> AcceptInviteAsync(string inviteToken, string password)
    {
        try
        {
            // Decode token to get user email (simplified - in production use proper token structure)
            var handler = new JwtSecurityTokenHandler();

            // For now, assume token contains email - in production, implement proper invite token structure
            // This is a simplified implementation
            return (false, string.Empty, null, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Accept invite failed");
            return (false, string.Empty, null, null);
        }
    }

    public async Task<(bool Success, string Token, User? User, Tenant? Tenant)> SwitchTenantAsync(int userId, string tenantSlug)
    {
        try
        {
            var userTenant = await _context.UserTenants
                .Include(ut => ut.User)
                .Include(ut => ut.Tenant)
                .FirstOrDefaultAsync(ut => ut.UserId == userId &&
                                          ut.Tenant.Slug == tenantSlug &&
                                          ut.Tenant.Status == "Active");

            if (userTenant == null)
            {
                return (false, string.Empty, null, null);
            }

            var token = GenerateJwtToken(userTenant.User, userTenant.Tenant, userTenant.Role);
            return (true, token, userTenant.User, userTenant.Tenant);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Switch tenant failed for user: {UserId}", userId);
            return (false, string.Empty, null, null);
        }
    }

    public async Task<(bool Success, string Message)> ForgotPasswordAsync(string email)
    {
        try
        {
            var user = await _userManager.FindByEmailAsync(email);

            // Always return success to prevent email enumeration attacks
            if (user == null || !user.IsActive)
            {
                _logger.LogWarning("Password reset requested for non-existent or inactive user: {Email}", email);
                return (true, "If the email exists in our system, a password reset code has been sent.");
            }

            // Generate 6-digit OTP
            var random = new Random();
            var otp = random.Next(100000, 999999).ToString();

            // Store OTP in database with 15-minute expiration
            var otpRecord = new PasswordResetOtp
            {
                UserId = user.Id,
                Otp = otp,
                ExpiresAt = DateTime.UtcNow.AddMinutes(15),
                IsUsed = false
            };

            _context.PasswordResetOtps.Add(otpRecord);
            await _context.SaveChangesAsync();

            // Build reset URL (for frontend navigation)
            var resetUrl = $"{_configuration["AppUrl"]}/auth/verify-otp?email={Uri.EscapeDataString(email)}";

            // Send OTP email
            var emailSent = await _emailService.SendPasswordResetEmailAsync(
                user.Email!,
                user.Email!,
                otp,
                resetUrl);

            if (!emailSent)
            {
                _logger.LogError("Failed to send password reset email to: {Email}", email);
                return (false, "Failed to send password reset code. Please try again later.");
            }

            _logger.LogInformation("Password reset OTP sent to: {Email}", email);
            return (true, "If the email exists in our system, a password reset code has been sent.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Forgot password failed for email: {Email}", email);
            return (false, "An error occurred. Please try again later.");
        }
    }

    public async Task<(bool Success, string Message)> VerifyOtpAsync(string email, string otp)
    {
        try
        {
            var user = await _userManager.FindByEmailAsync(email);

            if (user == null)
            {
                _logger.LogWarning("OTP verification attempted for non-existent user: {Email}", email);
                return (false, "Invalid verification code.");
            }

            // Find valid OTP
            var otpRecord = await _context.PasswordResetOtps
                .Where(o => o.UserId == user.Id &&
                           o.Otp == otp &&
                           !o.IsUsed &&
                           o.ExpiresAt > DateTime.UtcNow)
                .OrderByDescending(o => o.CreatedAt)
                .FirstOrDefaultAsync();

            if (otpRecord == null)
            {
                _logger.LogWarning("Invalid or expired OTP for user: {Email}", email);
                return (false, "Invalid or expired verification code.");
            }

            _logger.LogInformation("OTP verified successfully for user: {Email}", email);
            return (true, "Verification code is valid.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OTP verification failed for email: {Email}", email);
            return (false, "An error occurred. Please try again later.");
        }
    }

    public async Task<(bool Success, string Message)> ResetPasswordAsync(string email, string otp, string newPassword)
    {
        try
        {
            var user = await _userManager.FindByEmailAsync(email);

            if (user == null)
            {
                _logger.LogWarning("Password reset attempted for non-existent user: {Email}", email);
                return (false, "Invalid password reset request.");
            }

            if (!user.IsActive)
            {
                _logger.LogWarning("Password reset attempted for inactive user: {Email}", email);
                return (false, "User account is inactive.");
            }

            // Find and validate OTP
            var otpRecord = await _context.PasswordResetOtps
                .Where(o => o.UserId == user.Id &&
                           o.Otp == otp &&
                           !o.IsUsed &&
                           o.ExpiresAt > DateTime.UtcNow)
                .OrderByDescending(o => o.CreatedAt)
                .FirstOrDefaultAsync();

            if (otpRecord == null)
            {
                _logger.LogWarning("Invalid or expired OTP for password reset: {Email}", email);
                return (false, "Invalid or expired verification code.");
            }

            // Remove old password and set new one
            await _userManager.RemovePasswordAsync(user);
            var result = await _userManager.AddPasswordAsync(user, newPassword);

            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                _logger.LogWarning("Password reset failed for {Email}: {Errors}", email, errors);
                return (false, "Failed to reset password. " + errors);
            }

            // Mark OTP as used
            otpRecord.IsUsed = true;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Password successfully reset for user: {Email}", email);
            return (true, "Password has been reset successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Reset password failed for email: {Email}", email);
            return (false, "An error occurred. Please try again later.");
        }
    }

    public string GenerateJwtToken(User user, Tenant tenant, string role)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email!),
            new Claim("tid", tenant.Id.ToString()),
            new Claim("tenant_slug", tenant.Slug),
            new Claim("role", role),
            new Claim("plan", tenant.Plan),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(double.Parse(_configuration["Jwt:ExpiryMinutes"]!)),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public int? GetCurrentUserId(HttpContext httpContext)
    {
        var userIdClaim = httpContext.User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (int.TryParse(userIdClaim, out var userId))
        {
            return userId;
        }
        return null;
    }
}
