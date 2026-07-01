using AutoMapper;
using Microsoft.EntityFrameworkCore;
using TimeManagementBackend.Data;
using TimeManagementBackend.Exceptions;
using TimeManagementBackend.Models;
using TimeManagementBackend.Models.DTOs;

namespace TimeManagementBackend.Services;

public class WorkSessionService(AppDbContext db, IMapper mapper) : IWorkSessionService
{
    private const int AllowedDeltaMinutes = 5;

    // ── Clock actions ──────────────────────────────────────────────────────────

    public async Task<WorkSessionDto> ClockInAsync(string userId, ClockInDto dto, CancellationToken ct = default)
    {
        var serverStamp = TruncateToMinute(DateTimeOffset.UtcNow);
        var effectiveTime = ResolveEffectiveTime(dto.RecordedAt, serverStamp);
        var date = DateOnly.FromDateTime(effectiveTime.UtcDateTime);

        if (dto.TimeZoneId != null)
            ValidateLocalDate(dto.RecordedAt ?? effectiveTime, dto.TimeZoneId, date);

        await using var tx = await db.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, ct);
        try
        {
            if (await db.WorkSessions.AnyAsync(
                s => s.UserId == userId && s.Status == WorkSessionStatus.Open, ct))
                throw new ValidationException("You are already clocked in.");

            var vacationTotal = await db.VacationDays
                .Where(v => v.UserId == userId && v.Date == date)
                .SumAsync(v => (decimal?)v.Amount, ct) ?? 0m;

            if (vacationTotal >= 1.0m)
                throw new ValidationException(
                    "You have a full-day vacation scheduled today and cannot clock in.");

            var session = new WorkSession
            {
                UserId = userId,
                Date = date,
                ClockIn = effectiveTime,
                ClockInServerStamp = serverStamp,
                Status = WorkSessionStatus.Open,
            };
            db.WorkSessions.Add(session);

            await UpsertWorkDayAsync(userId, date, workedFromHome: dto.WorkedFromHome, ct: ct);

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                throw new ValidationException("You are already clocked in.");
            }

            await tx.CommitAsync(ct);
            await db.Entry(session).Collection(s => s.Breaks).LoadAsync(ct);
            return mapper.Map<WorkSessionDto>(session);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<WorkSessionDto> ClockOutAsync(string userId, ClockOutDto dto, CancellationToken ct = default)
    {
        var serverStamp = TruncateToMinute(DateTimeOffset.UtcNow);
        var effectiveTime = ResolveEffectiveTime(dto.RecordedAt, serverStamp);

        await using var tx = await db.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, ct);
        try
        {
            var session = await db.WorkSessions
                .Include(s => s.Breaks)
                .FirstOrDefaultAsync(s => s.UserId == userId && s.Status == WorkSessionStatus.Open, ct)
                ?? throw new ValidationException("You are not clocked in.");

            if (session.Breaks.Any(b => b.BreakEnd == null))
                throw new ValidationException("You must end your break before clocking out.");

            if (effectiveTime < session.ClockIn)
                throw new ValidationException(
                    $"Clock-out time cannot be before clock-in at {session.ClockIn:HH:mm} UTC.");

            session.ClockOut = effectiveTime;
            session.ClockOutServerStamp = serverStamp;
            session.Status = WorkSessionStatus.Closed;

            if (dto.Description != null)
                await UpsertWorkDayAsync(userId, session.Date, description: dto.Description, ct: ct);

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return mapper.Map<WorkSessionDto>(session);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<BreakRecordDto> StartBreakAsync(string userId, CancellationToken ct = default)
    {
        var serverStamp = TruncateToMinute(DateTimeOffset.UtcNow);

        await using var tx = await db.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, ct);
        try
        {
            var session = await db.WorkSessions
                .Include(s => s.Breaks)
                .FirstOrDefaultAsync(s => s.UserId == userId && s.Status == WorkSessionStatus.Open, ct)
                ?? throw new ValidationException("You are not clocked in.");

            if (session.Breaks.Any(b => b.BreakEnd == null))
                throw new ValidationException("You are already on a break.");

            var vacationTotal = await db.VacationDays
                .Where(v => v.UserId == userId && v.Date == session.Date)
                .SumAsync(v => (decimal?)v.Amount, ct) ?? 0m;

            if (vacationTotal == 0.5m)
                throw new ValidationException("Breaks are not permitted on half-day vacation days.");

            var breakRecord = new BreakRecord
            {
                WorkSessionId = session.Id,
                BreakStart = serverStamp,
                BreakStartServerStamp = serverStamp,
            };

            try
            {
                db.BreakRecords.Add(breakRecord);
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                throw new ValidationException("You are already on a break.");
            }

            await tx.CommitAsync(ct);
            return mapper.Map<BreakRecordDto>(breakRecord);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<BreakRecordDto> EndBreakAsync(string userId, EndBreakDto dto, CancellationToken ct = default)
    {
        var serverStamp = TruncateToMinute(DateTimeOffset.UtcNow);
        var effectiveTime = ResolveEffectiveTime(dto.RecordedAt, serverStamp);

        await using var tx = await db.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, ct);
        try
        {
            var session = await db.WorkSessions
                .Include(s => s.Breaks)
                .FirstOrDefaultAsync(s => s.UserId == userId && s.Status == WorkSessionStatus.Open, ct)
                ?? throw new ValidationException("You are not clocked in.");

            var openBreak = session.Breaks.FirstOrDefault(b => b.BreakEnd == null)
                ?? throw new ValidationException("You are not on a break.");

            var config = await db.AppConfigurations.FirstOrDefaultAsync(ct);
            var target = await db.EmployeeTargets.FirstOrDefaultAsync(t => t.UserId == userId, ct);
            var minimumMinutes = target?.MinimumBreakMinutes ?? config?.MinimumBreakMinutes;

            if (minimumMinutes is > 0)
            {
                var elapsed = (int)(effectiveTime - openBreak.BreakStart).TotalMinutes;
                if (elapsed < minimumMinutes.Value)
                    throw new BreakTooShortException(minimumMinutes.Value, Math.Max(0, elapsed));
            }

            openBreak.BreakEnd = effectiveTime;
            openBreak.BreakEndServerStamp = serverStamp;

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return mapper.Map<BreakRecordDto>(openBreak);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    // ── Read endpoints ─────────────────────────────────────────────────────────

    public async Task<TodayStatusDto> GetTodayAsync(string userId, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var sessions = await db.WorkSessions
            .Include(s => s.Breaks)
            .Where(s => s.UserId == userId && s.Date == today)
            .OrderBy(s => s.ClockIn)
            .ToListAsync(ct);

        var workDay = await db.WorkDays
            .FirstOrDefaultAsync(d => d.UserId == userId && d.Date == today, ct);

        return new TodayStatusDto
        {
            OpenSession = mapper.Map<WorkSessionDto>(sessions.FirstOrDefault(s => s.Status == WorkSessionStatus.Open)),
            ClosedSessions = mapper.Map<List<WorkSessionDto>>(
                sessions.Where(s => s.Status == WorkSessionStatus.Closed).ToList()),
            WorkDay = workDay != null ? mapper.Map<WorkDayDto>(workDay) : null,
        };
    }

    public async Task<TodayLiveDto?> GetTodayLiveAsync(string userId, CancellationToken ct = default)
    {
        var session = await db.WorkSessions
            .Include(s => s.Breaks)
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Status == WorkSessionStatus.Open, ct);

        if (session == null) return null;

        var now = DateTimeOffset.UtcNow;
        var openBreakStart = session.Breaks.FirstOrDefault(b => b.BreakEnd == null)?.BreakStart;

        var closedBreakMinutes = session.Breaks
            .Where(b => b.BreakEnd != null)
            .Sum(b => (b.BreakEnd!.Value - b.BreakStart).TotalMinutes);

        var elapsedMinutes = (now - session.ClockIn).TotalMinutes - closedBreakMinutes;

        return new TodayLiveDto
        {
            SessionId = session.Id,
            ClockIn = session.ClockIn,
            ElapsedMinutes = Math.Max(0, elapsedMinutes),
            IsOnBreak = openBreakStart.HasValue,
            BreakStartedAt = openBreakStart,
        };
    }

    public async Task<IEnumerable<WorkDaySummaryDto>> GetSummariesAsync(
        string userId, DateOnly? dateFrom, DateOnly? dateTo, CancellationToken ct = default)
    {
        var query = db.WorkSessions
            .Include(s => s.Breaks)
            .Where(s => s.UserId == userId && s.Status != WorkSessionStatus.Invalidated)
            .AsQueryable();

        if (dateFrom.HasValue) query = query.Where(s => s.Date >= dateFrom.Value);
        if (dateTo.HasValue) query = query.Where(s => s.Date <= dateTo.Value);

        var sessions = await query
            .OrderByDescending(s => s.Date)
            .ThenBy(s => s.ClockIn)
            .ToListAsync(ct);

        var vacations = await db.VacationDays
            .Include(v => v.VacationType)
            .Where(v => v.UserId == userId)
            .ToListAsync(ct);

        var workDays = await db.WorkDays
            .Where(d => d.UserId == userId)
            .ToListAsync(ct);

        var vacationByDate = vacations
            .GroupBy(v => v.Date)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(v => v.Amount).First());

        var workDayByDate = workDays.ToDictionary(d => d.Date);

        return sessions
            .GroupBy(s => s.Date)
            .Select(g =>
            {
                var closed = g.Where(s => s.Status == WorkSessionStatus.Closed).ToList();
                var totalHours = closed.Sum(s =>
                {
                    var breakMinutes = s.Breaks
                        .Where(b => b.BreakEnd != null)
                        .Sum(b => (b.BreakEnd!.Value - b.BreakStart).TotalMinutes);
                    return (s.ClockOut!.Value - s.ClockIn).TotalHours - breakMinutes / 60.0;
                });

                vacationByDate.TryGetValue(g.Key, out var vacation);
                workDayByDate.TryGetValue(g.Key, out var workDay);

                return new WorkDaySummaryDto
                {
                    Date = g.Key,
                    TotalWorkedHours = Math.Max(0, totalHours),
                    HasOpenSession = g.Any(s => s.Status == WorkSessionStatus.Open),
                    WorkDay = workDay != null ? mapper.Map<WorkDayDto>(workDay) : null,
                    Sessions = mapper.Map<List<WorkSessionDto>>(g.ToList()),
                    VacationAmount = vacation?.Amount,
                    VacationTypeName = vacation?.VacationType?.Name,
                };
            })
            .OrderByDescending(s => s.Date)
            .ToList();
    }

    public async Task<WorkScheduleDto> GetMyWorkScheduleAsync(string userId, CancellationToken ct = default)
    {
        var config = await db.AppConfigurations.FirstOrDefaultAsync(ct);
        var employeeTarget = await db.EmployeeTargets
            .FirstOrDefaultAsync(t => t.UserId == userId, ct);

        var allTargets = await db.WorkdayTargets
            .Where(t => t.UserId == userId || t.UserId == null)
            .ToListAsync(ct);

        // Mon–Sun order (DayOfWeek: Sun=0, Mon=1 … Sat=6 — sort Sunday last)
        var schedule = Enum.GetValues<DayOfWeek>()
            .OrderBy(d => d == DayOfWeek.Sunday ? 7 : (int)d)
            .Select(dow =>
            {
                var userRow = allTargets.FirstOrDefault(t => t.UserId == userId && t.DayOfWeek == dow);
                var globalRow = allTargets.FirstOrDefault(t => t.UserId == null && t.DayOfWeek == dow);
                var effective = userRow ?? globalRow;
                return new WorkdayTargetDto { DayOfWeek = dow, Hours = effective?.Hours ?? 0m };
            })
            .ToList();

        return new WorkScheduleDto
        {
            WorkdayTargets = schedule,
            MinimumBreakMinutes = employeeTarget?.MinimumBreakMinutes ?? config?.MinimumBreakMinutes,
            DailyOvertimeAllowanceHours = employeeTarget?.DailyOvertimeAllowanceHours
                ?? config?.DefaultDailyOvertimeAllowanceHours,
            WeeklyOvertimeAllowanceHours = employeeTarget?.WeeklyOvertimeAllowanceHours
                ?? config?.DefaultWeeklyOvertimeAllowanceHours,
        };
    }

    public async Task<WorkDayDto> UpdateDayAsync(
        string userId, DateOnly date, UpdateWorkDayDto dto, CancellationToken ct = default)
    {
        var workDay = await db.WorkDays
            .FirstOrDefaultAsync(d => d.UserId == userId && d.Date == date, ct);

        if (workDay == null)
        {
            workDay = new WorkDay { UserId = userId, Date = date };
            db.WorkDays.Add(workDay);
        }

        if (dto.WorkedFromHome.HasValue) workDay.WorkedFromHome = dto.WorkedFromHome.Value;
        if (dto.Description != null) workDay.Description = dto.Description;

        await db.SaveChangesAsync(ct);
        return mapper.Map<WorkDayDto>(workDay);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static DateTimeOffset TruncateToMinute(DateTimeOffset dt) =>
        new DateTimeOffset(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, 0, TimeSpan.Zero);

    private static DateTimeOffset ResolveEffectiveTime(DateTimeOffset? clientTime, DateTimeOffset serverStamp)
    {
        if (!clientTime.HasValue) return serverStamp;
        var clientTruncated = TruncateToMinute(clientTime.Value.ToUniversalTime());
        return Math.Abs((clientTruncated - serverStamp).TotalMinutes) <= AllowedDeltaMinutes
            ? clientTruncated
            : serverStamp;
    }

    private static void ValidateLocalDate(DateTimeOffset recordedAt, string timeZoneId, DateOnly expectedDate)
    {
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            var localTime = TimeZoneInfo.ConvertTimeFromUtc(recordedAt.UtcDateTime, tz);
            var derivedDate = DateOnly.FromDateTime(localTime);
            if (derivedDate != expectedDate)
                throw new ValidationException(
                    "The submitted local date does not match the date derived from your timezone and timestamp.");
        }
        catch (TimeZoneNotFoundException)
        {
            // Unknown timezone ID — skip validation rather than rejecting legitimate events
        }
    }

    private async Task UpsertWorkDayAsync(
        string userId, DateOnly date,
        bool? workedFromHome = null, string? description = null,
        CancellationToken ct = default)
    {
        var workDay = await db.WorkDays
            .FirstOrDefaultAsync(d => d.UserId == userId && d.Date == date, ct);

        if (workDay == null)
        {
            workDay = new WorkDay { UserId = userId, Date = date };
            db.WorkDays.Add(workDay);
        }

        if (workedFromHome.HasValue) workDay.WorkedFromHome = workedFromHome.Value;
        if (description != null) workDay.Description = description;
    }
}
