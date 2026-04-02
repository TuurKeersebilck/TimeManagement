using TimeManagementBackend.Models.DTOs;

namespace TimeManagementBackend.Services;

public interface ITimeLogService
{
    Task<IEnumerable<TimeLogDto>> GetForUserAsync(string userId, CancellationToken cancellationToken = default);
    Task<TimeLogDto?> GetByIdAsync(int id, string userId, CancellationToken cancellationToken = default);
    Task<bool> ExistsForDateAsync(string userId, DateOnly date, int? excludeId = null, CancellationToken cancellationToken = default);
    Task<TimeLogDto> CreateAsync(TimeLogCreateDto createDto, string userId, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(int id, TimeLogUpdateDto updateDto, string userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int id, string userId, CancellationToken cancellationToken = default);
    Task<MyTargetDto> GetMyTargetAsync(string userId, CancellationToken cancellationToken = default);
}
