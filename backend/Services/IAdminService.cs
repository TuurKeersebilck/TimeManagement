using TimeManagementBackend.Models.DTOs;

namespace TimeManagementBackend.Services;

public interface IAdminService
{
    Task<IEnumerable<AdminTimeLogDto>> GetAllTimeLogsAsync(string? userId = null, DateOnly? dateFrom = null, DateOnly? dateTo = null, CancellationToken ct = default);
    Task<IEnumerable<EmployeeDto>> GetEmployeesAsync(CancellationToken ct = default);

    // Vacation types
    Task<IEnumerable<VacationTypeDto>> GetVacationTypesAsync(CancellationToken ct = default);
    Task<VacationTypeDto> CreateVacationTypeAsync(VacationTypeCreateDto dto, CancellationToken ct = default);
    Task<VacationTypeDto> UpdateVacationTypeAsync(int id, VacationTypeUpdateDto dto, CancellationToken ct = default);
    Task DeleteVacationTypeAsync(int id, CancellationToken ct = default);

    // Employee vacation balances
    Task<IEnumerable<EmployeeVacationBalanceDto>> GetEmployeeBalancesAsync(string userId, CancellationToken ct = default);
    Task<EmployeeVacationBalanceDto> AssignVacationTypeAsync(string userId, AssignVacationTypeDto dto, CancellationToken ct = default);
    Task<EmployeeVacationBalanceDto> UpdateEmployeeBalanceAsync(int balanceId, UpdateVacationBalanceDto dto, CancellationToken ct = default);
    Task RemoveEmployeeVacationTypeAsync(int balanceId, CancellationToken ct = default);

    // Vacation overview (admin calendar)
    Task<IEnumerable<AdminVacationDayDto>> GetAllVacationDaysAsync(string? userId = null, int? vacationTypeId = null, int? year = null, int? month = null, CancellationToken ct = default);
}
