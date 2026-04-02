using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using TimeManagementBackend.Config;

namespace TimeManagementBackend.Services;

public class EmailService(SmtpConfig config, ILogger<EmailService> logger) : IEmailService
{
    public async Task SendPasswordResetEmailAsync(string toEmail, string toName, string resetLink)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("Time Management", config.From));
        message.To.Add(new MailboxAddress(toName, toEmail));
        message.Subject = "Reset your password";

        message.Body = new TextPart("html")
        {
            Text = $"""
                <div style="font-family:sans-serif;max-width:480px;margin:0 auto">
                  <h2 style="color:#1e293b">Reset your password</h2>
                  <p style="color:#475569">Hi {toName},</p>
                  <p style="color:#475569">
                    Click the button below to reset your password.
                    This link expires in <strong>1 hour</strong> and can only be used once.
                  </p>
                  <a href="{resetLink}"
                     style="display:inline-block;margin:16px 0;padding:12px 24px;background:#6366f1;color:#fff;border-radius:8px;text-decoration:none;font-weight:600">
                    Reset password
                  </a>
                  <p style="color:#94a3b8;font-size:13px">
                    If you didn't request this, you can safely ignore this email.
                  </p>
                </div>
                """
        };

        using var client = new SmtpClient();
        try
        {
            await client.ConnectAsync(config.Host, config.Port, SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(config.User, config.Password);
            await client.SendAsync(message);
        }
        finally
        {
            await client.DisconnectAsync(true);
        }

        logger.LogInformation("Password reset email sent to {Email}", toEmail);
    }
}
