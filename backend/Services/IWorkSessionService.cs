using TimeManagementBackend.Models.DTOs;

namespace TimeManagementBackend.Services;

public interface IWorkSessionService
{
    Task<WorkSessionDto> ClockInAsync(string userId, ClockInDto dto, CancellationToken ct = default);
    Task<WorkSessionDto> ClockOutAsync(string userId, ClockOutDto dto, CancellationToken ct = default);
    Task<BreakRecordDto> StartBreakAsync(string userId, CancellationToken ct = default);
    Task<BreakRecordDto> EndBreakAsync(string userId, EndBreakDto dto, CancellationToken ct = default);

    Task<TodayStatusDto> GetTodayAsync(string userId, CancellationToken ct = default);
    Task<TodayLiveDto?> GetTodayLiveAsync(string userId, CancellationToken ct = default);
    Task<IEnumerable<WorkDaySummaryDto>> GetSummariesAsync(string userId, DateOnly? dateFrom, DateOnly? dateTo, CancellationToken ct = default);

    Task<WorkScheduleDto> GetMyWorkScheduleAsync(string userId, CancellationToken ct = default);
    Task<WorkDayDto> UpdateDayAsync(string userId, DateOnly date, UpdateWorkDayDto dto, CancellationToken ct = default);
}
