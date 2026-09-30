namespace TimeManagementBackend.Services;

public interface IEmailService
{
    Task SendPasswordResetEmailAsync(string toEmail, string toName, string resetLink);

    Task SendAdjustmentRequestEmailAsync(
        string toEmail,
        string toName,
        string requesterName,
        DateOnly date,
        string sessionsSummary,
        string reason,
        string approveLink);

    Task SendMissedClockInReminderAsync(string toEmail, string toName, DateOnly missedDate);

    /// <summary>
    /// Tells the admin that monthly settlements await review. <paramref name="pendingByMonth"/> holds
    /// the first day of each month with its number of pending settlements, oldest first.
    /// </summary>
    Task SendSettlementReviewEmailAsync(
        string toEmail,
        IReadOnlyList<(DateOnly Month, int PendingCount)> pendingByMonth,
        string reviewLink,
        bool isReminder);

    Task SendAdjustmentOutcomeEmailAsync(string toEmail, string toName, DateOnly date, bool approved);

    Task SendInviteEmailAsync(string toEmail, string inviteLink);

    Task SendCalendarTokenExpiringEmailAsync(string toEmail, string toName, DateTimeOffset expiresAt);
}
