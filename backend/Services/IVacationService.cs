using TimeManagementBackend.Models.DTOs;

namespace TimeManagementBackend.Services;

public interface IVacationService
{
    Task<IEnumerable<VacationBalanceDto>> GetMyBalancesAsync(string userId, CancellationToken ct = default);
    Task<IEnumerable<VacationDayDto>> GetMyVacationDaysAsync(string userId, CancellationToken ct = default);
    Task<bool> ExistsForDateAndTypeAsync(string userId, DateOnly date, int vacationTypeId, CancellationToken ct = default);
    Task<VacationDayDto> CreateVacationDayAsync(string userId, CreateVacationDayDto dto, CancellationToken ct = default);
    Task<VacationDayDto> UpdateVacationDayAsync(string userId, int id, CreateVacationDayDto dto, CancellationToken ct = default);
    Task DeleteVacationDayAsync(string userId, int id, CancellationToken ct = default);
    Task<VacationRangeResultDto> CreateVacationRangeAsync(string userId, CreateVacationRangeDto dto, CancellationToken ct = default);
    Task<IEnumerable<TeamVacationDayDto>> GetTeamVacationDaysAsync(int year, CancellationToken ct = default);
}
