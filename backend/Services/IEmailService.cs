namespace TimeManagementBackend.Services;

/// <summary>One requested period in the admin's vacation request email.</summary>
public record VacationRequestEmailItem(
    string VacationTypeName, DateOnly StartDate, DateOnly EndDate, decimal TotalDays, string? Note);

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

    /// <summary>
    /// Tells the admin an employee requested leave: everything they submitted at once, in one
    /// email. The button opens the review page in the app.
    /// </summary>
    Task SendVacationRequestEmailAsync(
        string toEmail,
        string requesterName,
        IReadOnlyList<VacationRequestEmailItem> requests,
        string reviewLink);

    Task SendVacationRequestOutcomeEmailAsync(
        string toEmail,
        string toName,
        string vacationTypeName,
        DateOnly startDate,
        DateOnly endDate,
        bool approved,
        string? reviewNote);

    Task SendInviteEmailAsync(string toEmail, string inviteLink);

    Task SendCalendarTokenExpiringEmailAsync(string toEmail, string toName, DateTimeOffset expiresAt);
}
