using TimeManagementBackend.Models.DTOs;

namespace TimeManagementBackend.Services;

public interface ISettlementService
{
    Task GenerateForAllEmployeesAsync(int year, int month, CancellationToken ct = default);

    /// <summary>
    /// Emails the admin notification address a summary of settlements awaiting review, if the
    /// setting is on and anything is pending. <paramref name="isReminder"/> picks the wording:
    /// "ready for review" right after generation, "still waiting" for the weekly follow-up.
    /// </summary>
    Task SendReviewEmailAsync(string appUrl, bool isReminder, CancellationToken ct = default);

    Task<IEnumerable<MonthlySettlementDto>> GetSettlementsAsync(int year, int month, CancellationToken ct = default);
    Task<MonthlySettlementDto> GetSettlementDetailAsync(int id, CancellationToken ct = default);
    Task ConfirmAsync(int id, ConfirmSettlementDto dto, string adminUserId, CancellationToken ct = default);
    Task<IEnumerable<MonthlySettlementDto>> GetEmployeeHistoryAsync(string userId, CancellationToken ct = default);
}
