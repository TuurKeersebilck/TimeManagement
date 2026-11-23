using TimeManagementBackend.Models.DTOs;

namespace TimeManagementBackend.Services;

public interface IVacationService
{
    /// <param name="includeRequests">
    /// Fill PendingDays/DraftDays from the employee's own requests. Off for the admin's view of an
    /// employee, which shows balances exactly as before requests existed (and never sees drafts).
    /// </param>
    Task<IEnumerable<VacationBalanceDto>> GetMyBalancesAsync(string userId, int? year = null, CancellationToken ct = default, bool includeRequests = true);
    Task<IEnumerable<VacationDayDto>> GetMyVacationDaysAsync(string userId, DateOnly? dateFrom = null, DateOnly? dateTo = null, CancellationToken ct = default);
    Task<bool> ExistsForDateAndTypeAsync(string userId, DateOnly date, int vacationTypeId, CancellationToken ct = default);
    Task<VacationDayDto> CreateVacationDayAsync(string userId, CreateVacationDayDto dto, bool asAdmin = false, CancellationToken ct = default);
    Task<VacationDayDto> UpdateVacationDayAsync(string userId, int id, CreateVacationDayDto dto, bool asAdmin = false, CancellationToken ct = default);
    Task DeleteVacationDayAsync(string userId, int id, bool asAdmin = false, CancellationToken ct = default);
    Task<VacationRangeResultDto> CreateVacationRangeAsync(string userId, CreateVacationRangeDto dto, bool asAdmin = false, CancellationToken ct = default);
    Task<VacationDayDto?> GetVacationForDateAsync(string userId, DateOnly date, CancellationToken ct = default);

    /// <summary>
    /// The days a range would book: working days, minus public holidays off and dates that already
    /// have a day of this type. Shared by direct bookings and leave requests so both count alike.
    /// </summary>
    Task<VacationRangePlan> PlanRangeAsync(string userId, int vacationTypeId, DateOnly startDate, DateOnly endDate, CancellationToken ct = default);

    /// <summary>
    /// The yearly balance for a type, or null when it is unlimited. Throws when a limited type isn't
    /// assigned to the employee, or when <paramref name="asAdmin"/> is false and the type is admin-only.
    /// </summary>
    Task<decimal?> GetYearlyBalanceAsync(string userId, int vacationTypeId, bool asAdmin, CancellationToken ct = default);

    /// <summary>Days of a type booked in a calendar year.</summary>
    Task<decimal> GetUsedVacationDaysAsync(string userId, int vacationTypeId, int year, int? excludeId = null, CancellationToken ct = default);
}
