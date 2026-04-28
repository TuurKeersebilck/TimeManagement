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

    public async Task SendAdjustmentRequestEmailAsync(
        string toEmail,
        string toName,
        string requesterName,
        DateOnly date,
        DateTimeOffset? requestedClockIn,
        DateTimeOffset? requestedBreakStart,
        DateTimeOffset? requestedBreakEnd,
        DateTimeOffset? requestedClockOut,
        string reason,
        string approveLink)
    {
        // Show the employee's local time (the time they actually entered, not UTC)
        static string Fmt(DateTimeOffset? t) => t.HasValue ? t.Value.ToString("HH:mm") : "—";

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("Time Management", config.From));
        message.To.Add(new MailboxAddress(toName, toEmail));
        message.Subject = $"Time adjustment request from {requesterName} — {date:yyyy-MM-dd}";

        message.Body = new TextPart("html")
        {
            Text = $"""
                <div style="font-family:sans-serif;max-width:520px;margin:0 auto">
                  <h2 style="color:#1e293b">Time Adjustment Request</h2>
                  <p style="color:#475569"><strong>{requesterName}</strong> has requested a time adjustment for <strong>{date:dddd, MMMM d yyyy}</strong>.</p>
                  <table style="width:100%;border-collapse:collapse;margin:16px 0">
                    <tr style="background:#f8fafc"><td style="padding:8px 12px;color:#64748b;font-size:13px">Clock In</td><td style="padding:8px 12px;font-weight:600">{Fmt(requestedClockIn)}</td></tr>
                    <tr><td style="padding:8px 12px;color:#64748b;font-size:13px">Break Start</td><td style="padding:8px 12px;font-weight:600">{Fmt(requestedBreakStart)}</td></tr>
                    <tr style="background:#f8fafc"><td style="padding:8px 12px;color:#64748b;font-size:13px">Break End</td><td style="padding:8px 12px;font-weight:600">{Fmt(requestedBreakEnd)}</td></tr>
                    <tr><td style="padding:8px 12px;color:#64748b;font-size:13px">Clock Out</td><td style="padding:8px 12px;font-weight:600">{Fmt(requestedClockOut)}</td></tr>
                  </table>
                  <p style="color:#475569"><strong>Reason:</strong> {System.Net.WebUtility.HtmlEncode(reason)}</p>
                  <a href="{approveLink}"
                     style="display:inline-block;margin:16px 0;padding:12px 28px;background:#22c55e;color:#fff;border-radius:8px;text-decoration:none;font-weight:600;font-size:15px">
                    Approve Request
                  </a>
                  <p style="color:#94a3b8;font-size:12px;margin-top:8px">
                    Clicking approve will immediately update the employee's time log. This link expires in 7 days.
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

        logger.LogInformation("Adjustment request email sent to admin {Email}", toEmail);
    }

    public async Task SendMissedClockInReminderAsync(string toEmail, string toName, DateOnly missedDate)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("Time Management", config.From));
        message.To.Add(new MailboxAddress(toName, toEmail));
        message.Subject = $"Missing time log — {missedDate:yyyy-MM-dd}";

        message.Body = new TextPart("html")
        {
            Text = $"""
                <div style="font-family:sans-serif;max-width:480px;margin:0 auto">
                  <h2 style="color:#1e293b">Missing Time Log</h2>
                  <p style="color:#475569">Hi {toName},</p>
                  <p style="color:#475569">
                    No clock-in was recorded for <strong>{missedDate:dddd, MMMM d yyyy}</strong>.
                    If this was a working day, please open the app and submit a time adjustment request with your hours and a brief reason.
                  </p>
                  <p style="color:#94a3b8;font-size:13px">
                    If you were absent or on leave, you can ignore this message.
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

        logger.LogInformation("Missed clock-in reminder sent to {Email} for {Date}", toEmail, missedDate);
    }

    public async Task SendAdjustmentOutcomeEmailAsync(string toEmail, string toName, DateOnly date, bool approved)
    {
        var (statusWord, color, detail) = approved
            ? ("approved", "#22c55e", "Your time log for this day has been updated accordingly.")
            : ("rejected", "#ef4444", "No changes have been made to your time log. If you believe this is a mistake, please contact your manager.");

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("Time Management", config.From));
        message.To.Add(new MailboxAddress(toName, toEmail));
        message.Subject = $"Your time adjustment request for {date:yyyy-MM-dd} has been {statusWord}";

        message.Body = new TextPart("html")
        {
            Text = $"""
                <div style="font-family:sans-serif;max-width:480px;margin:0 auto">
                  <h2 style="color:#1e293b">Time Adjustment Request {char.ToUpper(statusWord[0])}{statusWord[1..]}</h2>
                  <p style="color:#475569">Hi {toName},</p>
                  <p style="color:#475569">
                    Your time adjustment request for <strong>{date:dddd, MMMM d yyyy}</strong> has been
                    <strong style="color:{color}">{statusWord}</strong>.
                  </p>
                  <p style="color:#475569">{detail}</p>
                  <p style="color:#94a3b8;font-size:13px">You can view your time logs in the app at any time.</p>
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

        logger.LogInformation("Adjustment outcome email ({Status}) sent to {Email}", statusWord, toEmail);
    }
}
