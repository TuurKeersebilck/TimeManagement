using TimeManagementBackend.Models.DTOs;

namespace TimeManagementBackend.Services;

public interface ISettlementService
{
    Task GenerateForAllEmployeesAsync(int year, int month, CancellationToken ct = default);
    Task<IEnumerable<MonthlySettlementDto>> GetSettlementsAsync(int year, int month, CancellationToken ct = default);
    Task<MonthlySettlementDto> GetSettlementDetailAsync(int id, CancellationToken ct = default);
    Task ConfirmAsync(int id, ConfirmSettlementDto dto, string adminUserId, CancellationToken ct = default);
    Task<IEnumerable<MonthlySettlementDto>> GetEmployeeHistoryAsync(string userId, CancellationToken ct = default);
}
