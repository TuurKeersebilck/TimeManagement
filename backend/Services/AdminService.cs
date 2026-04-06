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

    public async Task<IEnumerable<AdminTimeLogDto>> GetAllTimeLogsAsync(string? userId = null, DateOnly? dateFrom = null, DateOnly? dateTo = null, CancellationToken ct = default)
    {
        var query = _context.TimeLogs
            .AsNoTracking()
            .Join(
                _context.Users,
                log => log.UserId,
                user => user.Id,
                (log, user) => new { log, user }
            );

        if (!string.IsNullOrEmpty(userId))
            query = query.Where(x => x.log.UserId == userId);
        if (dateFrom.HasValue)
            query = query.Where(x => x.log.Date >= dateFrom.Value);
        if (dateTo.HasValue)
            query = query.Where(x => x.log.Date <= dateTo.Value);

        return await query
            .OrderByDescending(x => x.log.Date)
            .Select(x => new AdminTimeLogDto
            {
                Id = x.log.Id,
                UserId = x.log.UserId!,
                EmployeeName = x.user.FullName,
                EmployeeEmail = x.user.Email!,
                Date = x.log.Date,
                StartTime = x.log.StartTime,
                EndTime = x.log.EndTime,
                BreakStart = x.log.BreakStart,
                BreakEnd = x.log.BreakEnd,
                Description = x.log.Description,
            })
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<EmployeeDto>> GetEmployeesAsync(CancellationToken ct = default)
    {
        var config = await _context.AppConfigurations.FirstOrDefaultAsync(ct);

        var (weekStart, weekEnd) = TimeCalculationHelper.GetCurrentWeekBounds();

        var users = await _context.Users
            .AsNoTracking()
            .OrderBy(u => u.FullName)
            .ToListAsync(ct);

        var weekLogs = await _context.TimeLogs
            .AsNoTracking()
            .Where(l => l.Date >= weekStart && l.Date <= weekEnd)
            .ToListAsync(ct);

        var targets = await _context.EmployeeTargets
            .AsNoTracking()
            .ToListAsync(ct);

        return users.Select(u =>
        {
            var userLogs = weekLogs.Where(l => l.UserId == u.Id);
            var weeklyLogged = (decimal)userLogs.Sum(l =>
                TimeCalculationHelper.CalculateWorkedHours(l.StartTime, l.EndTime, l.BreakStart, l.BreakEnd));

            var target = targets.FirstOrDefault(t => t.UserId == u.Id);
            var resolvedWeekly = target?.WeeklyHours ?? config?.DefaultWeeklyHours;

            return new EmployeeDto
            {
                Id = u.Id,
                FullName = u.FullName,
                Email = u.Email!,
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

        var logs = await _context.TimeLogs
            .AsNoTracking()
            .Where(l => l.UserId == userId && l.Date >= from && l.Date <= to)
            .ToListAsync(ct);

        var target = await GetEmployeeTargetAsync(userId, ct);

        return weekRanges.Select(w =>
        {
            var weekLogs = logs.Where(l => l.Date >= w.Start && l.Date <= w.End);
            var hoursLogged = (decimal)weekLogs.Sum(l =>
                TimeCalculationHelper.CalculateWorkedHours(l.StartTime, l.EndTime, l.BreakStart, l.BreakEnd));

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

    public async Task<string> GeneratePayrollCsvAsync(int year, int month, string? userId = null, CancellationToken ct = default)
    {
        var dateFrom = new DateOnly(year, month, 1);
        var dateTo = dateFrom.AddMonths(1).AddDays(-1);

        var logsQuery = _context.TimeLogs
            .AsNoTracking()
            .Join(_context.Users, l => l.UserId, u => u.Id, (l, u) => new { l, u })
            .Where(x => x.l.Date >= dateFrom && x.l.Date <= dateTo);

        if (!string.IsNullOrEmpty(userId))
            logsQuery = logsQuery.Where(x => x.l.UserId == userId);

        var logs = await logsQuery
            .OrderBy(x => x.u.FullName)
            .ThenBy(x => x.l.Date)
            .Select(x => new AdminTimeLogDto
            {
                Id = x.l.Id,
                UserId = x.l.UserId!,
                EmployeeName = x.u.FullName,
                EmployeeEmail = x.u.Email!,
                Date = x.l.Date,
                StartTime = x.l.StartTime,
                EndTime = x.l.EndTime,
                BreakStart = x.l.BreakStart,
                BreakEnd = x.l.BreakEnd,
                Description = x.l.Description,
            })
            .ToListAsync(ct);

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

        // Also include employees with vacations but no logs
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
        sb.AppendLine("Employee,Email,Date,Day,Start,End,Break Duration (h),Net Hours,Description");

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
                log.StartTime.ToString(@"HH\:mm"),
                log.EndTime.ToString(@"HH\:mm"),
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
