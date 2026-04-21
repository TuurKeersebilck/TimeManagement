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

        var vacationList = await db.VacationDays
            .AsNoTracking()
            .Include(v => v.VacationType)
            .Where(v => v.UserId == userId)
            .ToListAsync(ct);

        // A user can have multiple vacation entries per date (different types, e.g. two half-days).
        // Group them and keep the one with the highest amount for display purposes.
        var vacationByDate = vacationList
            .GroupBy(v => v.Date)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(v => v.Amount).First());

        return events
            .GroupBy(e => e.Date)
            .Select(g =>
            {
                vacationByDate.TryGetValue(g.Key, out var vacation);
                return BuildDaySummary(g.Key, g.ToList(), vacation);
            })
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

            // Sum all vacation amounts for the day — a user can have multiple entries
            // (e.g. two half-days of different types) that together equal a full day.
            var vacationTotal = await db.VacationDays
                .Where(v => v.UserId == userId && v.Date == today)
                .SumAsync(v => (decimal?)v.Amount, ct) ?? 0m;

            if (vacationTotal >= 1.0m && dto.Type == ClockEventType.ClockIn)
                throw new ValidationException("You have a full-day vacation scheduled today and cannot clock in.");

            var isHalfDay = vacationTotal == 0.5m;
            ValidateOrder(dto.Type, existingToday.Select(e => e.Type).ToHashSet(), isHalfDay);

            // Minimum break enforcement (skipped on half-days since breaks are not permitted there)
            if (dto.Type == ClockEventType.BreakEnd && !isHalfDay)
            {
                var config = await db.AppConfigurations.FirstOrDefaultAsync(ct);
                var target = await db.EmployeeTargets.FirstOrDefaultAsync(t => t.UserId == userId, ct);
                var minimumBreakMinutes = target?.MinimumBreakMinutes ?? config?.MinimumBreakMinutes;

                if (minimumBreakMinutes.HasValue && minimumBreakMinutes.Value > 0)
                {
                    var breakStart = existingToday.First(e => e.Type == ClockEventType.BreakStart);
                    var elapsedMinutes = (recordedTruncated - breakStart.RecordedAt).TotalMinutes;
                    if (elapsedMinutes < minimumBreakMinutes.Value)
                        throw new ValidationException(
                            $"Minimum break duration is {minimumBreakMinutes.Value} minutes. " +
                            $"Your break has only lasted {(int)elapsedMinutes} minute(s).");
                }
            }

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
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                // The DB unique constraint on (UserId, Date, Type) fired — a concurrent
                // request raced past the application-level duplicate check above.
                throw new ValidationException($"You have already submitted a {dto.Type} event for today.");
            }
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

    private static void ValidateOrder(ClockEventType type, HashSet<ClockEventType> completed, bool isHalfDay = false)
    {
        if (isHalfDay)
        {
            if (type is ClockEventType.BreakStart or ClockEventType.BreakEnd)
                throw new ValidationException("Break events are not permitted on half-day vacation days.");
            if (type == ClockEventType.ClockOut && !completed.Contains(ClockEventType.ClockIn))
                throw new ValidationException("You must complete ClockIn before submitting ClockOut.");
            return;
        }

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
            MinimumBreakMinutes = target?.MinimumBreakMinutes ?? config?.MinimumBreakMinutes,
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

        var vacation = await db.VacationDays
            .AsNoTracking()
            .Include(v => v.VacationType)
            .Where(v => v.UserId == userId && v.Date == date)
            .FirstOrDefaultAsync(ct);

        await db.SaveChangesAsync(ct);
        return BuildDaySummary(date, events, vacation);
    }

    internal static DaySummaryDto BuildDaySummary(DateOnly date, List<ClockEvent> events, VacationDay? vacation = null)
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
            VacationAmount = vacation?.Amount,
            VacationTypeName = vacation?.VacationType?.Name,
        };
    }
}
