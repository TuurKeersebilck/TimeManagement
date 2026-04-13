using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;
using TimeManagementBackend.Data;
using TimeManagementBackend.Exceptions;
using TimeManagementBackend.Helpers;
using TimeManagementBackend.Models;
using TimeManagementBackend.Models.DTOs;

namespace TimeManagementBackend.Services;

public class ClockEventService(AppDbContext db, IMapper mapper) : IClockEventService
{
    private const int AllowedDeltaMinutes = 5;

    // Expected order of clock event types for a day
    private static readonly ClockEventType[] ExpectedOrder =
        [ClockEventType.ClockIn, ClockEventType.BreakStart, ClockEventType.BreakEnd, ClockEventType.ClockOut];

    public async Task<IEnumerable<ClockEventDto>> GetTodayEventsAsync(string userId, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        return await db.ClockEvents
            .AsNoTracking()
            .Where(e => e.UserId == userId && e.Date == today)
            .OrderBy(e => e.Type)
            .ProjectTo<ClockEventDto>(mapper.ConfigurationProvider)
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<DaySummaryDto>> GetSummariesAsync(string userId, CancellationToken ct = default)
    {
        var events = await db.ClockEvents
            .AsNoTracking()
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.Date)
            .ToListAsync(ct);

        return events
            .GroupBy(e => e.Date)
            .Select(g => BuildDaySummary(g.Key, g.ToList()))
            .OrderByDescending(s => s.Date)
            .ToList();
    }

    public async Task<ClockEventDto> SubmitEventAsync(string userId, SubmitClockEventDto dto, CancellationToken ct = default)
    {
        var actualTime = DateTime.Now.TimeOfDay;
        var today = DateOnly.FromDateTime(DateTime.Now);

        // Server-side ±5 minute validation
        var delta = Math.Abs((dto.RecordedTime - actualTime).TotalMinutes);
        if (delta > AllowedDeltaMinutes)
            throw new ValidationException(
                $"Recorded time must be within {AllowedDeltaMinutes} minutes of the current time. " +
                "Please submit an adjustment request if you need to log a different time.");

        // Load existing events for today to validate ordering
        var existingToday = await db.ClockEvents
            .Where(e => e.UserId == userId && e.Date == today)
            .ToListAsync(ct);

        // Prevent duplicate event type for today
        if (existingToday.Any(e => e.Type == dto.Type))
            throw new ValidationException($"You have already submitted a {dto.Type} event for today.");

        // Enforce sequential order: can't submit step N before step N-1
        var expectedIndex = Array.IndexOf(ExpectedOrder, dto.Type);
        var completedTypes = existingToday.Select(e => e.Type).ToHashSet();
        for (var i = 0; i < expectedIndex; i++)
        {
            if (!completedTypes.Contains(ExpectedOrder[i]))
                throw new ValidationException(
                    $"You must complete {ExpectedOrder[i]} before submitting {dto.Type}.");
        }

        // Description only allowed on ClockOut
        var description = dto.Type == ClockEventType.ClockOut ? dto.Description : null;

        var entity = new ClockEvent
        {
            UserId = userId,
            Date = today,
            Type = dto.Type,
            ActualTime = actualTime,
            RecordedTime = dto.RecordedTime,
            Description = description,
        };

        db.ClockEvents.Add(entity);
        await db.SaveChangesAsync(ct);

        return mapper.Map<ClockEventDto>(entity);
    }

    public async Task<MyTargetDto> GetMyTargetAsync(string userId, CancellationToken ct = default)
    {
        var config = await db.AppConfigurations.FirstOrDefaultAsync(ct);
        var target = await db.EmployeeTargets.FirstOrDefaultAsync(t => t.UserId == userId, ct);

        return new MyTargetDto
        {
            DailyHours = target?.DailyHours ?? config?.DefaultDailyHours,
            WeeklyHours = target?.WeeklyHours ?? config?.DefaultWeeklyHours,
        };
    }

    internal static DaySummaryDto BuildDaySummary(DateOnly date, List<ClockEvent> events)
    {
        var clockIn = events.FirstOrDefault(e => e.Type == ClockEventType.ClockIn)?.RecordedTime;
        var breakStart = events.FirstOrDefault(e => e.Type == ClockEventType.BreakStart)?.RecordedTime;
        var breakEnd = events.FirstOrDefault(e => e.Type == ClockEventType.BreakEnd)?.RecordedTime;
        var clockOut = events.FirstOrDefault(e => e.Type == ClockEventType.ClockOut)?.RecordedTime;
        var description = events.FirstOrDefault(e => e.Type == ClockEventType.ClockOut)?.Description;

        var isComplete = clockIn.HasValue && breakStart.HasValue && breakEnd.HasValue && clockOut.HasValue;

        var totalHours = clockIn.HasValue && clockOut.HasValue
            ? TimeCalculationHelper.CalculateWorkedHours(clockIn.Value, clockOut.Value, breakStart, breakEnd)
            : 0.0;

        return new DaySummaryDto
        {
            Date = date,
            ClockIn = clockIn,
            BreakStart = breakStart,
            BreakEnd = breakEnd,
            ClockOut = clockOut,
            TotalHours = totalHours,
            Description = description,
            IsComplete = isComplete,
        };
    }
}
