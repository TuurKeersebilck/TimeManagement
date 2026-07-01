using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using TimeManagementBackend.Config;

namespace TimeManagementBackend.Services;

public class EmailService(SmtpConfig config, ILogger<EmailService> logger) : IEmailService
{
    private const int TimeoutSeconds = 30;

    public async Task SendPasswordResetEmailAsync(string toEmail, string toName, string resetLink)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("Logr", config.From));
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

        await SendMessageAsync(message, $"password-reset to {toEmail}");
    }

    public async Task SendAdjustmentRequestEmailAsync(
        string toEmail,
        string toName,
        string requesterName,
        DateOnly date,
        string sessionsSummary,
        string reason,
        string approveLink)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("Logr", config.From));
        message.To.Add(new MailboxAddress(toName, toEmail));
        message.Subject = $"Time adjustment request from {requesterName} — {date:yyyy-MM-dd}";

        message.Body = new TextPart("html")
        {
            Text = $"""
                <div style="font-family:sans-serif;max-width:520px;margin:0 auto">
                  <h2 style="color:#1e293b">Time Adjustment Request</h2>
                  <p style="color:#475569"><strong>{requesterName}</strong> has requested a time adjustment for <strong>{date:dddd, MMMM d yyyy}</strong>.</p>
                  <pre style="background:#f8fafc;border:1px solid #e2e8f0;border-radius:6px;padding:12px;font-size:13px;color:#334155;white-space:pre-wrap">{System.Net.WebUtility.HtmlEncode(sessionsSummary)}</pre>
                  <p style="color:#475569"><strong>Reason:</strong> {System.Net.WebUtility.HtmlEncode(reason)}</p>
                  <a href="{approveLink}"
                     style="display:inline-block;margin:16px 0;padding:12px 28px;background:#22c55e;color:#fff;border-radius:8px;text-decoration:none;font-weight:600;font-size:15px">
                    Approve Request
                  </a>
                  <p style="color:#94a3b8;font-size:12px;margin-top:8px">
                    Clicking approve will immediately update the employee's time log. This link expires in 30 days.
                  </p>
                </div>
                """
        };

        await SendMessageAsync(message, $"adjustment-request to {toEmail}");
    }

    public async Task SendMissedClockInReminderAsync(string toEmail, string toName, DateOnly missedDate)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("Logr", config.From));
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

        await SendMessageAsync(message, $"missed-clock-in reminder to {toEmail} for {missedDate}");
    }

    public async Task SendAdjustmentOutcomeEmailAsync(string toEmail, string toName, DateOnly date, bool approved)
    {
        var (statusWord, color, detail) = approved
            ? ("approved", "#22c55e", "Your time log for this day has been updated accordingly.")
            : ("rejected", "#ef4444", "No changes have been made to your time log. If you believe this is a mistake, please contact your manager.");

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("Logr", config.From));
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

        await SendMessageAsync(message, $"adjustment-outcome ({statusWord}) to {toEmail}");
    }

    public async Task SendInviteEmailAsync(string toEmail, string inviteLink)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("Logr", config.From));
        message.To.Add(new MailboxAddress(toEmail, toEmail));
        message.Subject = "You've been invited to Logr";

        message.Body = new TextPart("html")
        {
            Text = $"""
                <div style="font-family:sans-serif;max-width:480px;margin:0 auto">
                  <h2 style="color:#1e293b">You've been invited to Logr</h2>
                  <p style="color:#475569">
                    Your employer has invited you to create an account on Logr, a time tracking tool.
                  </p>
                  <p style="color:#475569">
                    Click the button below to set up your account.
                    This link expires in <strong>48 hours</strong> and can only be used once.
                  </p>
                  <a href="{inviteLink}"
                     style="display:inline-block;margin:16px 0;padding:12px 24px;background:#6366f1;color:#fff;border-radius:8px;text-decoration:none;font-weight:600">
                    Accept invitation
                  </a>
                  <p style="color:#94a3b8;font-size:13px">
                    If you weren't expecting this email, you can safely ignore it.
                  </p>
                </div>
                """
        };

        await SendMessageAsync(message, $"invite to {toEmail}");
    }

    private async Task SendMessageAsync(MimeMessage message, string context)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(TimeoutSeconds));
        using var client = new SmtpClient();
        try
        {
            await client.ConnectAsync(config.Host, config.Port, SecureSocketOptions.StartTls, cts.Token);
            await client.AuthenticateAsync(config.User, config.Password, cts.Token);
            await client.SendAsync(message, cancellationToken: cts.Token);
            logger.LogInformation("Email sent: {Context}", context);
        }
        catch (OperationCanceledException)
        {
            logger.LogError(
                "SMTP timeout after {TimeoutSeconds}s — could not send {Context}. Host: {Host}:{Port}, User: {User}",
                TimeoutSeconds, context, config.Host, config.Port, config.User);
            throw new TimeoutException($"SMTP connection to {config.Host}:{config.Port} timed out after {TimeoutSeconds} seconds.");
        }
        catch (MailKit.Security.AuthenticationException ex)
        {
            logger.LogError(
                "SMTP authentication failed for {User} on {Host}:{Port} — {Message}",
                config.User, config.Host, config.Port, ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "SMTP error sending {Context} via {Host}:{Port} as {User}",
                context, config.Host, config.Port, config.User);
            throw;
        }
        finally
        {
            try
            {
                using var disconnectCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await client.DisconnectAsync(true, disconnectCts.Token);
            }
            catch { /* best-effort disconnect */ }
        }
    }
}
