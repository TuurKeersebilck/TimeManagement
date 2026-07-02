using TimeManagementBackend.Models.DTOs;

namespace TimeManagementBackend.Services;

public interface IOvertimeCalculationService
{
    Task<OvertimeResultDto> CalculateAsync(string userId, int year, int month, CancellationToken ct = default);
}
