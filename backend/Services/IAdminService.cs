using TimeManagementBackend.Models.DTOs;

namespace TimeManagementBackend.Services;

public interface IAdminService
{
    Task<IEnumerable<AdminDaySummaryDto>> GetAllDaySummariesAsync(string? userId = null, DateOnly? dateFrom = null, DateOnly? dateTo = null, CancellationToken ct = default);
    Task<IEnumerable<EmployeeDto>> GetEmployeesAsync(CancellationToken ct = default);

    // Vacation types
    Task<IEnumerable<VacationTypeDto>> GetVacationTypesAsync(CancellationToken ct = default);
    Task<VacationTypeDto> CreateVacationTypeAsync(VacationTypeFormDto dto, CancellationToken ct = default);
    Task<VacationTypeDto> UpdateVacationTypeAsync(int id, VacationTypeFormDto dto, CancellationToken ct = default);
    Task DeleteVacationTypeAsync(int id, CancellationToken ct = default);

    // Employee vacation balances
    Task<IEnumerable<EmployeeVacationBalanceDto>> GetEmployeeBalancesAsync(string userId, CancellationToken ct = default);
    Task<EmployeeVacationBalanceDto> AssignVacationTypeAsync(string userId, AssignVacationTypeDto dto, CancellationToken ct = default);
    Task<EmployeeVacationBalanceDto> UpdateEmployeeBalanceAsync(int balanceId, UpdateVacationBalanceDto dto, CancellationToken ct = default);
    Task RemoveEmployeeVacationTypeAsync(int balanceId, CancellationToken ct = default);

    // Vacation overview (admin calendar)
    Task<IEnumerable<AdminVacationDayDto>> GetAllVacationDaysAsync(string? userId = null, int? vacationTypeId = null, int? year = null, int? month = null, CancellationToken ct = default);

    // Payroll export
    Task<string> GeneratePayrollCsvAsync(int year, int month, string? userId = null, int timezoneOffsetMinutes = 0, CancellationToken ct = default);

    // Working hours targets
    Task<EmployeeTargetDto> GetEmployeeTargetAsync(string userId, CancellationToken ct = default);
    Task<EmployeeTargetDto> SetEmployeeTargetAsync(string userId, SetEmployeeTargetDto dto, CancellationToken ct = default);
    Task<IEnumerable<WeekSummaryDto>> GetEmployeeWeeklySummaryAsync(string userId, int weeks, CancellationToken ct = default);
}
