using System.ComponentModel.DataAnnotations;

namespace Hostr.Api.Models;

public class PasswordResetOtp
{
    public int Id { get; set; }

    public int UserId { get; set; }

    [Required, MaxLength(10)]
    public string Otp { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public bool IsUsed { get; set; } = false;

    public DateTime CreatedAt { get; set; }

    // Navigation property
    public virtual User User { get; set; } = null!;
}

