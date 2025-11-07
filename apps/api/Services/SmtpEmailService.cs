using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace Hostr.Api.Services;

public class SmtpEmailService : IEmailService
{
    private readonly SmtpSettings _smtpSettings;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(
        IOptions<SmtpSettings> smtpSettings,
        ILogger<SmtpEmailService> logger)
    {
        _smtpSettings = smtpSettings.Value;
        _logger = logger;
    }

    public async Task<bool> SendPasswordResetEmailAsync(
        string toEmail,
        string userName,
        string resetToken,
        string resetUrl)
    {
        try
        {
            var subject = "Your Hostr Password Reset Code";
            var htmlBody = GeneratePasswordResetHtml(userName, resetUrl, resetToken);
            var textBody = $"Hi {userName},\n\nYour password reset code is: {resetToken}\n\nThis code will expire in 15 minutes.\n\nIf you didn't request this, please ignore this email.";

            return await SendEmailAsync(toEmail, subject, htmlBody, textBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send password reset email to {Email}", toEmail);
            return false;
        }
    }

    public async Task<bool> SendInviteEmailAsync(
        string toEmail,
        string tenantName,
        string inviteToken,
        string inviteUrl,
        string role)
    {
        try
        {
            var subject = $"You've been invited to join {tenantName} on Hostr";
            var htmlBody = GenerateInviteHtml(tenantName, inviteUrl, role);
            var textBody = $"You've been invited to join {tenantName} as {role}.\n\nClick here to set up your account: {inviteUrl}";

            return await SendEmailAsync(toEmail, subject, htmlBody, textBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send invite email to {Email} for tenant {TenantName}", toEmail, tenantName);
            return false;
        }
    }

    public async Task<bool> SendEmailAsync(
        string toEmail,
        string subject,
        string htmlBody,
        string? textBody = null)
    {
        try
        {
            using var smtpClient = CreateSmtpClient();
            using var mailMessage = new MailMessage
            {
                From = new MailAddress(_smtpSettings.FromEmail, _smtpSettings.FromName),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            };

            mailMessage.To.Add(toEmail);

            // Add plain text alternative
            if (!string.IsNullOrEmpty(textBody))
            {
                var plainView = AlternateView.CreateAlternateViewFromString(textBody, null, "text/plain");
                mailMessage.AlternateViews.Add(plainView);
            }

            await smtpClient.SendMailAsync(mailMessage);

            _logger.LogInformation("Email sent successfully to {Email} with subject: {Subject}", toEmail, subject);
            return true;
        }
        catch (SmtpException smtpEx)
        {
            _logger.LogError(smtpEx, "SMTP error sending email to {Email}: {Message}", toEmail, smtpEx.Message);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error sending email to {Email}", toEmail);
            return false;
        }
    }

    public async Task<bool> SendBulkEmailAsync(
        List<string> toEmails,
        string subject,
        string htmlBody,
        string? textBody = null)
    {
        var successCount = 0;
        var failureCount = 0;

        foreach (var email in toEmails)
        {
            var success = await SendEmailAsync(email, subject, htmlBody, textBody);
            if (success)
                successCount++;
            else
                failureCount++;

            // Rate limiting: small delay between emails
            await Task.Delay(100);
        }

        _logger.LogInformation("Bulk email completed: {Success} sent, {Failures} failed", successCount, failureCount);
        return failureCount == 0;
    }

    private SmtpClient CreateSmtpClient()
    {
        var smtpClient = new SmtpClient(_smtpSettings.Host, _smtpSettings.Port)
        {
            EnableSsl = _smtpSettings.EnableSsl,
            Credentials = new NetworkCredential(_smtpSettings.Username, _smtpSettings.Password),
            Timeout = 30000 // 30 seconds
        };

        return smtpClient;
    }

    private string GeneratePasswordResetHtml(string userName, string resetUrl, string otp)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #2563eb; color: white; padding: 20px; text-align: center; }}
        .content {{ background-color: #f9fafb; padding: 30px; border-radius: 8px; margin: 20px 0; }}
        .otp-code {{ background-color: #dbeafe; color: #1e40af; padding: 20px; font-size: 32px; font-weight: bold; text-align: center; letter-spacing: 8px; border-radius: 8px; margin: 20px 0; }}
        .footer {{ text-align: center; color: #6b7280; font-size: 12px; margin-top: 30px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>Password Reset Request</h1>
        </div>
        <div class='content'>
            <h2>Hi {userName},</h2>
            <p>We received a request to reset your password for your Hostr account.</p>
            <p>Use the verification code below to reset your password:</p>
            <div class='otp-code'>{otp}</div>
            <p style='margin-top: 30px;'><strong>This code will expire in 15 minutes.</strong></p>
            <p>If you didn't request this password reset, please ignore this email or contact support if you have concerns.</p>
        </div>
        <div class='footer'>
            <p>© 2025 Hostr. All rights reserved.</p>
            <p>You received this email because a password reset was requested for your account.</p>
        </div>
    </div>
</body>
</html>";
    }

    private string GenerateInviteHtml(string tenantName, string inviteUrl, string role)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #2563eb; color: white; padding: 20px; text-align: center; }}
        .content {{ background-color: #f9fafb; padding: 30px; border-radius: 8px; margin: 20px 0; }}
        .button {{ background-color: #2563eb; color: white; padding: 12px 30px; text-decoration: none; border-radius: 6px; display: inline-block; margin: 20px 0; }}
        .role-badge {{ background-color: #dbeafe; color: #1e40af; padding: 4px 12px; border-radius: 4px; font-weight: bold; }}
        .footer {{ text-align: center; color: #6b7280; font-size: 12px; margin-top: 30px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>Welcome to Hostr!</h1>
        </div>
        <div class='content'>
            <h2>You've Been Invited</h2>
            <p>You've been invited to join <strong>{tenantName}</strong> on Hostr as a <span class='role-badge'>{role}</span>.</p>
            <p>Hostr is a comprehensive hotel management platform that helps teams deliver exceptional guest experiences.</p>
            <p>Click the button below to set up your account and get started:</p>
            <a href='{inviteUrl}' class='button'>Accept Invitation</a>
            <p style='margin-top: 30px;'><strong>This invitation will expire in 7 days.</strong></p>
            <p>If you have any questions, please contact your team administrator.</p>
        </div>
        <div class='footer'>
            <p>© 2025 Hostr. All rights reserved.</p>
            <p>You received this email because you were invited to join {tenantName}.</p>
        </div>
    </div>
</body>
</html>";
    }
}

public class SmtpSettings
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool EnableSsl { get; set; }
    public string FromEmail { get; set; } = string.Empty;
    public string FromName { get; set; } = string.Empty;
}
