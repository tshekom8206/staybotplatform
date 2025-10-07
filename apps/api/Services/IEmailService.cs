namespace Hostr.Api.Services;

public interface IEmailService
{
    /// <summary>
    /// Sends a password reset email to a user
    /// </summary>
    Task<bool> SendPasswordResetEmailAsync(
        string toEmail,
        string userName,
        string resetToken,
        string resetUrl);

    /// <summary>
    /// Sends an invitation email to a new user
    /// </summary>
    Task<bool> SendInviteEmailAsync(
        string toEmail,
        string tenantName,
        string inviteToken,
        string inviteUrl,
        string role);

    /// <summary>
    /// Sends a generic email with HTML body
    /// </summary>
    Task<bool> SendEmailAsync(
        string toEmail,
        string subject,
        string htmlBody,
        string? textBody = null);

    /// <summary>
    /// Sends an email to multiple recipients
    /// </summary>
    Task<bool> SendBulkEmailAsync(
        List<string> toEmails,
        string subject,
        string htmlBody,
        string? textBody = null);
}

public class EmailResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime SentAt { get; set; }
    public string MessageId { get; set; } = string.Empty;
}
