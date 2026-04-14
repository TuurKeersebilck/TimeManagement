using Microsoft.EntityFrameworkCore;
using TimeManagementBackend.Data;
using TimeManagementBackend.Exceptions;
using TimeManagementBackend.Helpers;
using TimeManagementBackend.Models;
using TimeManagementBackend.Models.DTOs;

namespace TimeManagementBackend.Services;

public class AdminService(AppDbContext context) : IAdminService
{
    private readonly AppDbContext _context = context;

    public async Task<IEnumerable<AdminDaySummaryDto>> GetAllDaySummariesAsync(string? userId = null, DateOnly? dateFrom = null, DateOnly? dateTo = null, CancellationToken ct = default)
    {
        var query = _context.ClockEvents
            .AsNoTracking()
            .Include(e => e.User)
            .AsQueryable();

        if (!string.IsNullOrEmpty(userId))
            query = query.Where(e => e.UserId == userId);
        if (dateFrom.HasValue)
            query = query.Where(e => e.Date >= dateFrom.Value);
        if (dateTo.HasValue)
            query = query.Where(e => e.Date <= dateTo.Value);

        var events = await query.ToListAsync(ct);

        return events
            .GroupBy(e => new { e.UserId, e.Date })
            .Select(g =>
            {
                var first = g.First();
                var clockIn = g.FirstOrDefault(e => e.Type == Models.ClockEventType.ClockIn);
                var breakStart = g.FirstOrDefault(e => e.Type == Models.ClockEventType.BreakStart);
                var breakEnd = g.FirstOrDefault(e => e.Type == Models.ClockEventType.BreakEnd);
                var clockOut = g.FirstOrDefault(e => e.Type == Models.ClockEventType.ClockOut);
                var isComplete = clockIn != null && breakStart != null && breakEnd != null && clockOut != null;
                var totalHours = clockIn != null && clockOut != null
                    ? TimeCalculationHelper.CalculateWorkedHours(
                        clockIn.RecordedAt, clockOut.RecordedAt,
                        breakStart?.RecordedAt, breakEnd?.RecordedAt)
                    : 0.0;

                return new AdminDaySummaryDto
                {
                    UserId = g.Key.UserId,
                    EmployeeName = first.User.FullName,
                    EmployeeEmail = first.User.Email ?? string.Empty,
                    Date = g.Key.Date,
                    ClockIn = clockIn?.RecordedAt,
                    BreakStart = breakStart?.RecordedAt,
                    BreakEnd = breakEnd?.RecordedAt,
                    ClockOut = clockOut?.RecordedAt,
                    TotalHours = totalHours,
                    Description = clockOut?.Description,
                    IsComplete = isComplete,
                };
            })
            .OrderByDescending(s => s.Date)
            .ToList();
    }

    public async Task<IEnumerable<EmployeeDto>> GetEmployeesAsync(CancellationToken ct = default)
    {
        var config = await _context.AppConfigurations.FirstOrDefaultAsync(ct);

        var (weekStart, weekEnd) = TimeCalculationHelper.GetCurrentWeekBounds();

        var users = await _context.Users
            .AsNoTracking()
            .OrderBy(u => u.FullName)
            .ToListAsync(ct);

        var weekEvents = await _context.ClockEvents
            .AsNoTracking()
            .Where(e => e.Date >= weekStart && e.Date <= weekEnd)
            .ToListAsync(ct);

        var targets = await _context.EmployeeTargets
            .AsNoTracking()
            .ToListAsync(ct);

        return users.Select(u =>
        {
            var userEvents = weekEvents.Where(e => e.UserId == u.Id);
            var weeklyLogged = (decimal)userEvents
                .GroupBy(e => e.Date)
                .Sum(g =>
                {
                    var ci = g.FirstOrDefault(e => e.Type == Models.ClockEventType.ClockIn)?.RecordedAt;
                    var co = g.FirstOrDefault(e => e.Type == Models.ClockEventType.ClockOut)?.RecordedAt;
                    var bs = g.FirstOrDefault(e => e.Type == Models.ClockEventType.BreakStart)?.RecordedAt;
                    var be = g.FirstOrDefault(e => e.Type == Models.ClockEventType.BreakEnd)?.RecordedAt;
                    return ci.HasValue && co.HasValue
                        ? TimeCalculationHelper.CalculateWorkedHours(ci.Value, co.Value, bs, be)
                        : 0.0;
                });

            var target = targets.FirstOrDefault(t => t.UserId == u.Id);
            var resolvedWeekly = target?.WeeklyHours ?? config?.DefaultWeeklyHours;

            return new EmployeeDto
            {
                Id = u.Id,
                FullName = u.FullName,
                Email = u.Email ?? string.Empty,
                WeeklyHoursLogged = Math.Round(weeklyLogged, 2),
                ResolvedWeeklyTarget = resolvedWeekly,
            };
        });
    }

    // ─── Vacation types ───────────────────────────────────────────────────────

    public async Task<IEnumerable<VacationTypeDto>> GetVacationTypesAsync(CancellationToken ct = default)
    {
        return await _context.VacationTypes
            .AsNoTracking()
            .OrderBy(v => v.Name)
            .Select(v => new VacationTypeDto
            {
                Id = v.Id,
                Name = v.Name,
                Description = v.Description,
                Color = v.Color,
                AssignedEmployeeCount = v.EmployeeBalances.Count(),
            })
            .ToListAsync(ct);
    }

    public async Task<VacationTypeDto> CreateVacationTypeAsync(VacationTypeFormDto dto, CancellationToken ct = default)
    {
        var entity = new VacationType
        {
            Name = dto.Name,
            Description = dto.Description,
            Color = dto.Color,
        };

        _context.VacationTypes.Add(entity);
        await _context.SaveChangesAsync(ct);

        return new VacationTypeDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Description = entity.Description,
            Color = entity.Color,
            AssignedEmployeeCount = 0,
        };
    }

    public async Task<VacationTypeDto> UpdateVacationTypeAsync(int id, VacationTypeFormDto dto, CancellationToken ct = default)
    {
        var entity = await _context.VacationTypes
            .FirstOrDefaultAsync(v => v.Id == id, ct)
            ?? throw new ResourceNotFoundException($"Vacation type {id} not found.");

        entity.Name = dto.Name;
        entity.Description = dto.Description;
        entity.Color = dto.Color;

        await _context.SaveChangesAsync(ct);

        var assignedCount = await _context.EmployeeVacationBalances.CountAsync(b => b.VacationTypeId == id, ct);

        return new VacationTypeDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Description = entity.Description,
            Color = entity.Color,
            AssignedEmployeeCount = assignedCount,
        };
    }

    public async Task DeleteVacationTypeAsync(int id, CancellationToken ct = default)
    {
        var entity = await _context.VacationTypes.FindAsync([id], ct)
            ?? throw new ResourceNotFoundException($"Vacation type {id} not found.");

        entity.IsDeleted = true;
        entity.DeletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
    }

    // ─── Employee vacation balances ───────────────────────────────────────────

    public async Task<IEnumerable<EmployeeVacationBalanceDto>> GetEmployeeBalancesAsync(string userId, CancellationToken ct = default)
    {
        return await _context.EmployeeVacationBalances
            .AsNoTracking()
            .Where(b => b.UserId == userId)
            .OrderBy(b => b.VacationType.Name)
            .Select(b => new EmployeeVacationBalanceDto
            {
                Id = b.Id,
                VacationTypeId = b.VacationTypeId,
                VacationTypeName = b.VacationType.Name,
                VacationTypeColor = b.VacationType.Color,
                YearlyBalance = b.YearlyBalance,
            })
            .ToListAsync(ct);
    }

    public async Task<EmployeeVacationBalanceDto> AssignVacationTypeAsync(string userId, AssignVacationTypeDto dto, CancellationToken ct = default)
    {
        var entity = new EmployeeVacationBalance
        {
            UserId = userId,
            VacationTypeId = dto.VacationTypeId,
            YearlyBalance = dto.YearlyBalance,
        };

        _context.EmployeeVacationBalances.Add(entity);
        await _context.SaveChangesAsync(ct);

        await _context.Entry(entity).Reference(e => e.VacationType).LoadAsync(ct);

        return new EmployeeVacationBalanceDto
        {
            Id = entity.Id,
            VacationTypeId = entity.VacationTypeId,
            VacationTypeName = entity.VacationType.Name,
            VacationTypeColor = entity.VacationType.Color,
            YearlyBalance = entity.YearlyBalance,
        };
    }

    public async Task<EmployeeVacationBalanceDto> UpdateEmployeeBalanceAsync(int balanceId, UpdateVacationBalanceDto dto, CancellationToken ct = default)
    {
        var entity = await _context.EmployeeVacationBalances
            .Include(b => b.VacationType)
            .FirstOrDefaultAsync(b => b.Id == balanceId, ct)
            ?? throw new ResourceNotFoundException($"Balance {balanceId} not found.");

        entity.YearlyBalance = dto.YearlyBalance;
        await _context.SaveChangesAsync(ct);

        return new EmployeeVacationBalanceDto
        {
            Id = entity.Id,
            VacationTypeId = entity.VacationTypeId,
            VacationTypeName = entity.VacationType.Name,
            VacationTypeColor = entity.VacationType.Color,
            YearlyBalance = entity.YearlyBalance,
        };
    }

    public async Task RemoveEmployeeVacationTypeAsync(int balanceId, CancellationToken ct = default)
    {
        var entity = await _context.EmployeeVacationBalances.FindAsync([balanceId], ct)
            ?? throw new ResourceNotFoundException($"Balance {balanceId} not found.");

        _context.EmployeeVacationBalances.Remove(entity);
        await _context.SaveChangesAsync(ct);
    }

    // ─── Working hours targets ────────────────────────────────────────────────

    public async Task<EmployeeTargetDto> GetEmployeeTargetAsync(string userId, CancellationToken ct = default)
    {
        var config = await _context.AppConfigurations.FirstOrDefaultAsync(ct);
        var target = await _context.EmployeeTargets.FirstOrDefaultAsync(t => t.UserId == userId, ct);

        return new EmployeeTargetDto
        {
            DailyHours = target?.DailyHours,
            WeeklyHours = target?.WeeklyHours,
            ResolvedDailyHours = target?.DailyHours ?? config?.DefaultDailyHours,
            ResolvedWeeklyHours = target?.WeeklyHours ?? config?.DefaultWeeklyHours,
            HasOverride = target != null && (target.DailyHours.HasValue || target.WeeklyHours.HasValue),
        };
    }

    public async Task<EmployeeTargetDto> SetEmployeeTargetAsync(string userId, SetEmployeeTargetDto dto, CancellationToken ct = default)
    {
        var target = await _context.EmployeeTargets.FirstOrDefaultAsync(t => t.UserId == userId, ct);
        if (target == null)
        {
            target = new EmployeeTarget { UserId = userId };
            _context.EmployeeTargets.Add(target);
        }

        target.DailyHours = dto.DailyHours;
        target.WeeklyHours = dto.WeeklyHours;
        await _context.SaveChangesAsync(ct);

        return await GetEmployeeTargetAsync(userId, ct);
    }

    public async Task<IEnumerable<WeekSummaryDto>> GetEmployeeWeeklySummaryAsync(string userId, int weeks, CancellationToken ct = default)
    {
        var (thisWeekStart, _) = TimeCalculationHelper.GetCurrentWeekBounds();

        // Build week ranges from oldest to newest
        var weekRanges = Enumerable.Range(0, weeks)
            .Select(i => thisWeekStart.AddDays(-7 * (weeks - 1 - i)))
            .Select(start => (Start: start, End: start.AddDays(6)))
            .ToList();

        var from = weekRanges[0].Start;
        var to = weekRanges[^1].End;

        var events = await _context.ClockEvents
            .AsNoTracking()
            .Where(e => e.UserId == userId && e.Date >= from && e.Date <= to)
            .ToListAsync(ct);

        var target = await GetEmployeeTargetAsync(userId, ct);

        return weekRanges.Select(w =>
        {
            var weekEvents = events.Where(e => e.Date >= w.Start && e.Date <= w.End);
            var hoursLogged = (decimal)weekEvents
                .GroupBy(e => e.Date)
                .Sum(g =>
                {
                    var ci = g.FirstOrDefault(e => e.Type == Models.ClockEventType.ClockIn)?.RecordedAt;
                    var co = g.FirstOrDefault(e => e.Type == Models.ClockEventType.ClockOut)?.RecordedAt;
                    var bs = g.FirstOrDefault(e => e.Type == Models.ClockEventType.BreakStart)?.RecordedAt;
                    var be = g.FirstOrDefault(e => e.Type == Models.ClockEventType.BreakEnd)?.RecordedAt;
                    return ci.HasValue && co.HasValue
                        ? TimeCalculationHelper.CalculateWorkedHours(ci.Value, co.Value, bs, be)
                        : 0.0;
                });

            // ISO week number
            var jan1 = new DateOnly(w.Start.Year, 1, 1);
            var weekNum = (w.Start.DayOfYear - 1) / 7 + 1;

            return new WeekSummaryDto
            {
                WeekLabel = $"W{weekNum}",
                WeekStart = w.Start.ToString("yyyy-MM-dd"),
                HoursLogged = Math.Round(hoursLogged, 2),
                Target = target.ResolvedWeeklyHours,
            };
        });
    }

    // ─── Payroll export ───────────────────────────────────────────────────────

    public async Task<string> GeneratePayrollCsvAsync(int year, int month, string? userId = null, int timezoneOffsetMinutes = 0, CancellationToken ct = default)
    {
        var dateFrom = new DateOnly(year, month, 1);
        var dateTo = dateFrom.AddMonths(1).AddDays(-1);

        var eventsQuery = _context.ClockEvents
            .AsNoTracking()
            .Include(e => e.User)
            .Where(e => e.Date >= dateFrom && e.Date <= dateTo);

        if (!string.IsNullOrEmpty(userId))
            eventsQuery = eventsQuery.Where(e => e.UserId == userId);

        var rawEvents = await eventsQuery.ToListAsync(ct);

        var logs = rawEvents
            .GroupBy(e => new { e.UserId, e.Date })
            .Select(g =>
            {
                var first = g.First();
                var ci = g.FirstOrDefault(e => e.Type == Models.ClockEventType.ClockIn);
                var bs = g.FirstOrDefault(e => e.Type == Models.ClockEventType.BreakStart);
                var be = g.FirstOrDefault(e => e.Type == Models.ClockEventType.BreakEnd);
                var co = g.FirstOrDefault(e => e.Type == Models.ClockEventType.ClockOut);
                return new AdminDaySummaryDto
                {
                    UserId = g.Key.UserId,
                    EmployeeName = first.User.FullName,
                    EmployeeEmail = first.User.Email ?? string.Empty,
                    Date = g.Key.Date,
                    ClockIn = ci?.RecordedAt,
                    BreakStart = bs?.RecordedAt,
                    BreakEnd = be?.RecordedAt,
                    ClockOut = co?.RecordedAt,
                    TotalHours = ci != null && co != null
                        ? TimeCalculationHelper.CalculateWorkedHours(ci.RecordedAt, co.RecordedAt, bs?.RecordedAt, be?.RecordedAt)
                        : 0.0,
                    Description = co?.Description,
                    IsComplete = ci != null && bs != null && be != null && co != null,
                };
            })
            .OrderBy(s => s.EmployeeName)
            .ThenBy(s => s.Date)
            .ToList();

        var vacationsQuery = _context.VacationDays
            .AsNoTracking()
            .Where(d => d.Date.Year == year && d.Date.Month == month);

        if (!string.IsNullOrEmpty(userId))
            vacationsQuery = vacationsQuery.Where(d => d.UserId == userId);

        var vacations = await vacationsQuery
            .OrderBy(d => d.User.FullName)
            .ThenBy(d => d.Date)
            .Select(d => new AdminVacationDayDto
            {
                Id = d.Id,
                UserId = d.UserId,
                EmployeeName = d.User.FullName,
                VacationTypeId = d.VacationTypeId,
                VacationTypeName = d.VacationType.Name,
                Date = d.Date,
                Amount = d.Amount,
                Note = d.Note,
            })
            .ToListAsync(ct);

        var sb = new System.Text.StringBuilder();
        var monthName = new System.Globalization.CultureInfo("en-GB").DateTimeFormat.GetMonthName(month);

        sb.AppendLine($"Payroll Report,{monthName} {year}");
        sb.AppendLine($"Generated,{DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC");
        sb.AppendLine();

        // ── Section 1: Summary ──────────────────────────────────────────────
        sb.AppendLine("SUMMARY");
        sb.AppendLine("Name,Email,Days Worked,Total Hours,Vacation Days");

        var vacsByEmployee = vacations.GroupBy(v => v.UserId).ToDictionary(g => g.Key, g => g.ToList());

        // Collect all employee IDs in sorted name order (use first log entry for name/email)
        var employeeOrder = logs
            .GroupBy(l => l.UserId)
            .Select(g => (UserId: g.Key, Name: g.First().EmployeeName, Email: g.First().EmployeeEmail))
            .OrderBy(e => e.Name)
            .ToList();

        // Also include employees with vacations but no clock events
        var employeesWithVacsOnly = vacations
            .GroupBy(v => v.UserId)
            .Where(g => !employeeOrder.Any(e => e.UserId == g.Key))
            .Select(g => (UserId: g.Key, Name: g.First().EmployeeName, Email: string.Empty))
            .OrderBy(e => e.Name);

        var allEmployees = employeeOrder.Concat(employeesWithVacsOnly).ToList();

        foreach (var emp in allEmployees)
        {
            var empLogs = logs.Where(l => l.UserId == emp.UserId).ToList();
            var daysWorked = empLogs.Select(l => l.Date).Distinct().Count();
            var totalHours = empLogs.Sum(l => l.TotalHours);
            var vacDays = vacsByEmployee.TryGetValue(emp.UserId, out var vList)
                ? vList.Sum(v => (double)v.Amount)
                : 0.0;

            sb.AppendLine($"{CsvEscape(emp.Name)},{CsvEscape(emp.Email)},{daysWorked},{totalHours:F2},{vacDays:F1}");
        }

        // ── Section 2: Daily Detail ─────────────────────────────────────────
        sb.AppendLine();
        sb.AppendLine("DAILY DETAIL");
        sb.AppendLine("Employee,Email,Date,Day,Clock In,Break Start,Break End,Clock Out,Break Duration (h),Net Hours,Description");

        foreach (var log in logs)
        {
            var breakHours = log.BreakStart.HasValue && log.BreakEnd.HasValue
                ? (log.BreakEnd.Value - log.BreakStart.Value).TotalHours
                : 0.0;

            sb.AppendLine(string.Join(",",
                CsvEscape(log.EmployeeName),
                CsvEscape(log.EmployeeEmail),
                log.Date.ToString("yyyy-MM-dd"),
                log.Date.DayOfWeek.ToString(),
                FormatExportTime(log.ClockIn, timezoneOffsetMinutes),
                FormatExportTime(log.BreakStart, timezoneOffsetMinutes),
                FormatExportTime(log.BreakEnd, timezoneOffsetMinutes),
                FormatExportTime(log.ClockOut, timezoneOffsetMinutes),
                breakHours.ToString("F2"),
                log.TotalHours.ToString("F2"),
                CsvEscape(log.Description ?? "")
            ));
        }

        // ── Section 3: Vacation Days ────────────────────────────────────────
        if (vacations.Count != 0)
        {
            sb.AppendLine();
            sb.AppendLine("VACATION DAYS");
            sb.AppendLine("Employee,Email,Date,Day,Type,Amount,Note");

            foreach (var v in vacations)
            {
                sb.AppendLine(string.Join(",",
                    CsvEscape(v.EmployeeName),
                    CsvEscape(allEmployees.FirstOrDefault(e => e.UserId == v.UserId).Email ?? ""),
                    v.Date.ToString("yyyy-MM-dd"),
                    v.Date.DayOfWeek.ToString(),
                    CsvEscape(v.VacationTypeName),
                    ((double)v.Amount).ToString("F1"),
                    CsvEscape(v.Note ?? "")
                ));
            }
        }

        return sb.ToString();
    }

    private static string FormatExportTime(DateTimeOffset? utcTime, int timezoneOffsetMinutes)
    {
        if (utcTime == null) return "";
        var local = utcTime.Value.ToOffset(TimeSpan.FromMinutes(timezoneOffsetMinutes));
        return local.ToString("HH:mm");
    }

    private static string CsvEscape(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }

    // ─── Vacation overview ────────────────────────────────────────────────────

    public async Task<IEnumerable<AdminVacationDayDto>> GetAllVacationDaysAsync(
        string? userId = null,
        int? vacationTypeId = null,
        int? year = null,
        int? month = null,
        CancellationToken ct = default)
    {
        var query = _context.VacationDays
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrEmpty(userId))
            query = query.Where(d => d.UserId == userId);

        if (vacationTypeId.HasValue)
            query = query.Where(d => d.VacationTypeId == vacationTypeId.Value);

        if (year.HasValue)
            query = query.Where(d => d.Date.Year == year.Value);

        if (month.HasValue)
            query = query.Where(d => d.Date.Month == month.Value);

        return await query
            .OrderBy(d => d.Date)
            .Select(d => new AdminVacationDayDto
            {
                Id = d.Id,
                UserId = d.UserId,
                EmployeeName = d.User.FullName,
                VacationTypeId = d.VacationTypeId,
                VacationTypeName = d.VacationType.Name,
                VacationTypeColor = d.VacationType.Color,
                Date = d.Date,
                Amount = d.Amount,
                Note = d.Note,
            })
            .ToListAsync(ct);
    }
}
