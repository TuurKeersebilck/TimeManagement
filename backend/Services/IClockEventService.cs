using TimeManagementBackend.Models.DTOs;

namespace TimeManagementBackend.Services;

public interface IClockEventService
{
    Task<IEnumerable<ClockEventDto>> GetTodayEventsAsync(string userId, CancellationToken ct = default);
    Task<IEnumerable<DaySummaryDto>> GetSummariesAsync(string userId, CancellationToken ct = default);
    Task<ClockEventDto> SubmitEventAsync(string userId, SubmitClockEventDto dto, CancellationToken ct = default);
    Task<MyTargetDto> GetMyTargetAsync(string userId, CancellationToken ct = default);
}
