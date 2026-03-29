using TimeManagementBackend.Models.DTOs;

namespace TimeManagementBackend.Services;

public interface IAdminService
{
    Task<IEnumerable<AdminTimeLogDto>> GetAllTimeLogsAsync(string? userId = null, CancellationToken ct = default);
    Task<IEnumerable<EmployeeDto>> GetEmployeesAsync(CancellationToken ct = default);
}
