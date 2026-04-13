namespace TimeManagementBackend.Services;

public interface IEmailService
{
    Task SendPasswordResetEmailAsync(string toEmail, string toName, string resetLink);

    Task SendAdjustmentRequestEmailAsync(
        string toEmail,
        string toName,
        string requesterName,
        DateOnly date,
        TimeSpan? requestedClockIn,
        TimeSpan? requestedBreakStart,
        TimeSpan? requestedBreakEnd,
        TimeSpan? requestedClockOut,
        string reason,
        string approveLink);

    Task SendMissedClockInReminderAsync(string toEmail, string toName, DateOnly missedDate);
}
