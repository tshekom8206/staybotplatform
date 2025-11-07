using System.ComponentModel.DataAnnotations;
using Hostr.Contracts.DTOs.Common;

namespace Hostr.Contracts.DTOs.Auth;

public record LoginRequest
{
    [Required, EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required]
    public string Password { get; init; } = string.Empty;
}

public record LoginResponse
{
    public string Token { get; init; } = string.Empty;
    public UserDto User { get; init; } = new();
    public TenantDto Tenant { get; init; } = new();
}

public record InviteRequest
{
    [Required, EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required]
    public string Role { get; init; } = string.Empty;

    public string TenantSlug { get; init; } = string.Empty;
}

public record AcceptInviteRequest
{
    [Required]
    public string Token { get; init; } = string.Empty;

    [Required, MinLength(8)]
    public string Password { get; init; } = string.Empty;
}

public record SwitchTenantRequest
{
    [Required]
    public string TenantSlug { get; init; } = string.Empty;
}

public record ForgotPasswordRequest
{
    [Required, EmailAddress]
    public string Email { get; init; } = string.Empty;
}

public record VerifyOtpRequest
{
    [Required, EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required, StringLength(6, MinimumLength = 6)]
    public string Otp { get; init; } = string.Empty;
}

public record ResetPasswordRequest
{
    [Required, EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required, StringLength(6, MinimumLength = 6)]
    public string Otp { get; init; } = string.Empty;

    [Required, MinLength(8)]
    public string NewPassword { get; init; } = string.Empty;
}
