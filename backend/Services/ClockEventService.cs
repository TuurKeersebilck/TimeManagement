using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;
using TimeManagementBackend.Data;
using TimeManagementBackend.Exceptions;
using TimeManagementBackend.Helpers;
using TimeManagementBackend.Models;
using TimeManagementBackend.Models.DTOs;
using Microsoft.EntityFrameworkCore.Storage;

namespace TimeManagementBackend.Services;

public class ClockEventService(AppDbContext db, IMapper mapper) : IClockEventService
{
    private const int AllowedDeltaMinutes = 5;

    public async Task<IEnumerable<ClockEventDto>> GetTodayEventsAsync(string userId, DateOnly localDate, CancellationToken ct = default)
    {
        return await db.ClockEvents
            .AsNoTracking()
            .Where(e => e.UserId == userId && e.Date == localDate)
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
        var now = DateTimeOffset.UtcNow;

        // Truncate to minute precision for the ±5 min check
        var nowTruncated = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, TimeSpan.Zero);
        var recordedTruncated = new DateTimeOffset(
            dto.RecordedAt.UtcDateTime.Year, dto.RecordedAt.UtcDateTime.Month, dto.RecordedAt.UtcDateTime.Day,
            dto.RecordedAt.UtcDateTime.Hour, dto.RecordedAt.UtcDateTime.Minute, 0, TimeSpan.Zero);

        var delta = Math.Abs((recordedTruncated - nowTruncated).TotalMinutes);
        if (delta > AllowedDeltaMinutes)
            throw new ValidationException(
                $"Recorded time must be within {AllowedDeltaMinutes} minutes of the current time. " +
                "Please submit an adjustment request if you need to log a different time.");

        var today = dto.LocalDate;
        var description = dto.Type == ClockEventType.ClockOut ? dto.Description : null;

        await using var tx = await db.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, ct);
        try
        {
            var existingToday = await db.ClockEvents
                .Where(e => e.UserId == userId && e.Date == today)
                .ToListAsync(ct);

            if (existingToday.Any(e => e.Type == dto.Type))
                throw new ValidationException($"You have already submitted a {dto.Type} event for today.");

            ValidateOrder(dto.Type, existingToday.Select(e => e.Type).ToHashSet());

            // Chronological check: new event must not be before any existing event
            if (existingToday.Count > 0)
            {
                var latestExisting = existingToday.Max(e => e.RecordedAt);
                if (recordedTruncated < latestExisting)
                    throw new ValidationException(
                        $"Recorded time cannot be before your previous event at {latestExisting.ToOffset(TimeSpan.Zero):HH:mm} UTC.");
            }

            var entity = new ClockEvent
            {
                UserId = userId,
                Date = today,
                Type = dto.Type,
                ActualAt = nowTruncated,
                RecordedAt = recordedTruncated,
                Description = description,
                WorkedFromHome = dto.Type == ClockEventType.ClockIn && dto.WorkedFromHome,
            };

            db.ClockEvents.Add(entity);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return mapper.Map<ClockEventDto>(entity);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    private static readonly ClockEventType[] ExpectedOrder =
        [ClockEventType.ClockIn, ClockEventType.BreakStart, ClockEventType.BreakEnd, ClockEventType.ClockOut];

    private static void ValidateOrder(ClockEventType type, HashSet<ClockEventType> completed)
    {
        var expectedIndex = Array.IndexOf(ExpectedOrder, type);
        for (var i = 0; i < expectedIndex; i++)
        {
            if (!completed.Contains(ExpectedOrder[i]))
                throw new ValidationException(
                    $"You must complete {ExpectedOrder[i]} before submitting {type}.");
        }
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

    public async Task<DaySummaryDto> UpdateDayAsync(string userId, DateOnly date, UpdateDayDto dto, CancellationToken ct = default)
    {
        var events = await db.ClockEvents
            .Where(e => e.UserId == userId && e.Date == date)
            .ToListAsync(ct);

        if (events.Count == 0)
            throw new Exceptions.ResourceNotFoundException("No events found for this date.");

        if (dto.Description != null)
        {
            var clockOut = events.FirstOrDefault(e => e.Type == ClockEventType.ClockOut);
            if (clockOut == null)
                throw new ValidationException("Cannot set a description before clocking out.");
            clockOut.Description = dto.Description;
        }

        if (dto.WorkedFromHome.HasValue)
        {
            var clockIn = events.FirstOrDefault(e => e.Type == ClockEventType.ClockIn);
            if (clockIn == null)
                throw new ValidationException("Cannot set work from home status without a clock-in event.");
            clockIn.WorkedFromHome = dto.WorkedFromHome.Value;
        }

        await db.SaveChangesAsync(ct);
        return BuildDaySummary(date, events);
    }

    internal static DaySummaryDto BuildDaySummary(DateOnly date, List<ClockEvent> events)
    {
        var clockInEvent = events.FirstOrDefault(e => e.Type == ClockEventType.ClockIn);
        var clockIn = clockInEvent?.RecordedAt;
        var breakStart = events.FirstOrDefault(e => e.Type == ClockEventType.BreakStart)?.RecordedAt;
        var breakEnd = events.FirstOrDefault(e => e.Type == ClockEventType.BreakEnd)?.RecordedAt;
        var clockOut = events.FirstOrDefault(e => e.Type == ClockEventType.ClockOut)?.RecordedAt;
        var description = events.FirstOrDefault(e => e.Type == ClockEventType.ClockOut)?.Description;
        var workedFromHome = clockInEvent?.WorkedFromHome ?? false;

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
            WorkedFromHome = workedFromHome,
        };
    }
}
