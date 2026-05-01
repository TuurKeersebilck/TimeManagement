namespace TimeManagementBackend.Services;

public interface IEmailService
{
    Task SendPasswordResetEmailAsync(string toEmail, string toName, string resetLink);

    Task SendAdjustmentRequestEmailAsync(
        string toEmail,
        string toName,
        string requesterName,
        DateOnly date,
        DateTimeOffset? requestedClockIn,
        DateTimeOffset? requestedBreakStart,
        DateTimeOffset? requestedBreakEnd,
        DateTimeOffset? requestedClockOut,
        string reason,
        string approveLink);

    Task SendMissedClockInReminderAsync(string toEmail, string toName, DateOnly missedDate);

    Task SendAdjustmentOutcomeEmailAsync(string toEmail, string toName, DateOnly date, bool approved);

    Task SendInviteEmailAsync(string toEmail, string inviteLink);
}
