using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using TimeManagementBackend.Data;
using TimeManagementBackend.Exceptions;
using TimeManagementBackend.Helpers;
using TimeManagementBackend.Models;
using TimeManagementBackend.Models.DTOs;

namespace TimeManagementBackend.Services;

public class AdminService(AppDbContext context, UserManager<User> userManager) : IAdminService
{
    private readonly AppDbContext _context = context;
    private readonly UserManager<User> _userManager = userManager;

    private static double CalcSessionHours(WorkSession s)
    {
        if (s.Status != WorkSessionStatus.Closed || !s.ClockOut.HasValue) return 0;
        var raw = (s.ClockOut.Value - s.ClockIn).TotalHours;
        var breakHours = s.Breaks
            .Where(b => b.BreakEnd.HasValue)
            .Sum(b => (b.BreakEnd!.Value - b.BreakStart).TotalHours);
        return Math.Max(0, raw - breakHours);
    }

    private static readonly DayOfWeek[] s_weekdays =
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday];

    private static decimal SumWeeklyTarget(List<WorkdayTarget> perEmployee, List<WorkdayTarget> globals)
        => s_weekdays.Sum(day =>
        {
            var emp = perEmployee.FirstOrDefault(t => t.DayOfWeek == day);
            if (emp != null) return emp.Hours;
            return globals.FirstOrDefault(t => t.DayOfWeek == day)?.Hours ?? 0m;
        });

    public async Task<IEnumerable<AdminDaySummaryDto>> GetAllDaySummariesAsync(string? userId = null, DateOnly? dateFrom = null, DateOnly? dateTo = null, CancellationToken ct = default)
    {
        var sessionQuery = _context.WorkSessions
            .AsNoTracking()
            .Include(s => s.User)
            .Include(s => s.Breaks)
            .AsQueryable();

        if (!string.IsNullOrEmpty(userId))
            sessionQuery = sessionQuery.Where(s => s.UserId == userId);
        if (dateFrom.HasValue)
            sessionQuery = sessionQuery.Where(s => s.Date >= dateFrom.Value);
        if (dateTo.HasValue)
            sessionQuery = sessionQuery.Where(s => s.Date <= dateTo.Value);

        var sessions = await sessionQuery.ToListAsync(ct);

        var userIdList = sessions.Select(s => s.UserId).Distinct().ToList();
        var dateList = sessions.Select(s => s.Date).Distinct().ToList();

        var workDays = await _context.WorkDays
            .AsNoTracking()
            .Where(d => userIdList.Contains(d.UserId) && dateList.Contains(d.Date))
            .ToListAsync(ct);

        return sessions
            .GroupBy(s => new { s.UserId, s.Date })
            .Select(g =>
            {
                var first = g.First();
                var workDay = workDays.FirstOrDefault(d => d.UserId == g.Key.UserId && d.Date == g.Key.Date);
                var totalHours = g.Where(s => s.Status == WorkSessionStatus.Closed).Sum(CalcSessionHours);

                return new AdminDaySummaryDto
                {
                    UserId = g.Key.UserId,
                    EmployeeName = first.User.FullName,
                    EmployeeEmail = first.User.Email ?? string.Empty,
                    Date = g.Key.Date,
                    TotalHours = totalHours,
                    Description = workDay?.Description,
                    WorkedFromHome = workDay?.WorkedFromHome ?? false,
                    HasOpenSession = g.Any(s => s.Status == WorkSessionStatus.Open),
                    HasInvalidatedSession = g.Any(s => s.Status == WorkSessionStatus.Invalidated),
                    Sessions = g
                        .OrderBy(s => s.ClockIn)
                        .Select(s => new AdminSessionDto
                        {
                            ClockIn = s.ClockIn,
                            ClockOut = s.ClockOut,
                            Status = s.Status,
                            Hours = CalcSessionHours(s),
                            Breaks = s.Breaks
                                .OrderBy(b => b.BreakStart)
                                .Select(b => new AdminBreakDto { BreakStart = b.BreakStart, BreakEnd = b.BreakEnd })
                                .ToList(),
                        })
                        .ToList(),
                };
            })
            .OrderByDescending(s => s.Date)
            .ToList();
    }

    public async Task<IEnumerable<EmployeeDto>> GetEmployeesAsync(UserRole? role = null, CancellationToken ct = default)
    {
        var config = await _context.AppConfigurations.FirstOrDefaultAsync(ct);

        var (weekStart, weekEnd) = TimeCalculationHelper.GetCurrentWeekBounds();

        var usersQuery = _context.Users.AsNoTracking().AsQueryable();
        if (role.HasValue)
            usersQuery = usersQuery.Where(u => u.Role == role.Value);

        var users = await usersQuery
            .OrderBy(u => u.FullName)
            .ToListAsync(ct);

        var weekSessions = await _context.WorkSessions
            .AsNoTracking()
            .Include(s => s.Breaks)
            .Where(s => s.Status == WorkSessionStatus.Closed && s.Date >= weekStart && s.Date <= weekEnd)
            .ToListAsync(ct);

        var allWorkdayTargets = await _context.WorkdayTargets
            .AsNoTracking()
            .ToListAsync(ct);
        var globalWorkdayTargets = allWorkdayTargets.Where(t => t.UserId == null).ToList();

        var weeklyByUser = weekSessions
            .GroupBy(s => s.UserId)
            .ToDictionary(g => g.Key, g => (decimal)g.Sum(CalcSessionHours));

        return users.Select(u =>
        {
            var weeklyLogged = weeklyByUser.TryGetValue(u.Id, out var h) ? h : 0m;
            var userWorkdays = allWorkdayTargets.Where(t => t.UserId == u.Id).ToList();
            var resolvedWeekly = (decimal?)SumWeeklyTarget(userWorkdays, globalWorkdayTargets);

            return new EmployeeDto
            {
                Id = u.Id,
                FullName = u.FullName,
                Email = u.Email ?? string.Empty,
                WeeklyHoursLogged = Math.Round(weeklyLogged, 2),
                ResolvedWeeklyTarget = resolvedWeekly,
                IsDisabled = u.IsDisabled,
            };
        });
    }

    public async Task DisableEmployeeAsync(string userId, CancellationToken ct = default)
    {
        var user = await _context.Users.FindAsync([userId], ct)
            ?? throw new ResourceNotFoundException($"User {userId} not found.");
        user.IsDisabled = true;
        await _context.SaveChangesAsync(ct);
    }

    public async Task EnableEmployeeAsync(string userId, CancellationToken ct = default)
    {
        var user = await _context.Users.FindAsync([userId], ct)
            ?? throw new ResourceNotFoundException($"User {userId} not found.");
        user.IsDisabled = false;
        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteEmployeeAsync(string userId, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new ResourceNotFoundException($"User {userId} not found.");

        if (!user.IsDisabled)
            throw new Exceptions.ValidationException("Employee must be disabled before permanent deletion.");

        // Delete all related data before removing the identity user record
        await _context.BreakRecords.Where(b => b.WorkSession.UserId == userId).ExecuteDeleteAsync(ct);
        await _context.WorkSessions.Where(s => s.UserId == userId).ExecuteDeleteAsync(ct);
        await _context.WorkDays.Where(d => d.UserId == userId).ExecuteDeleteAsync(ct);
        await _context.WorkdayTargets.Where(t => t.UserId == userId).ExecuteDeleteAsync(ct);
        await _context.TimeBankAdjustments.Where(a => a.UserId == userId).ExecuteDeleteAsync(ct);
        await _context.MonthlySettlements.Where(s => s.UserId == userId).ExecuteDeleteAsync(ct);
        await _context.TimeAdjustmentRequests.Where(e => e.UserId == userId).ExecuteDeleteAsync(ct);
        await _context.EmployeeVacationBalances.Where(b => b.UserId == userId).ExecuteDeleteAsync(ct);
        await _context.VacationDays.Where(d => d.UserId == userId).ExecuteDeleteAsync(ct);
        await _context.EmployeeTargets.Where(t => t.UserId == userId).ExecuteDeleteAsync(ct);
        await _context.Notifications.Where(n => n.RecipientUserId == userId).ExecuteDeleteAsync(ct);
        await _context.PasswordResetTokens.Where(t => t.UserId == userId).ExecuteDeleteAsync(ct);

        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded)
            throw new Exception(string.Join(", ", result.Errors.Select(e => e.Description)));
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
        entity.DeletedAt = DateTimeOffset.UtcNow;
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
            HasOverride = target?.MinimumBreakMinutes.HasValue ?? false,
            MinimumBreakMinutes = target?.MinimumBreakMinutes,
            ResolvedMinimumBreakMinutes = target?.MinimumBreakMinutes ?? config?.MinimumBreakMinutes,
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

        target.MinimumBreakMinutes = dto.MinimumBreakMinutes;
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

        var sessions = await _context.WorkSessions
            .AsNoTracking()
            .Include(s => s.Breaks)
            .Where(s => s.UserId == userId && s.Status == WorkSessionStatus.Closed && s.Date >= from && s.Date <= to)
            .ToListAsync(ct);

        var workdayTargets = await _context.WorkdayTargets
            .AsNoTracking()
            .Where(t => t.UserId == userId || t.UserId == null)
            .ToListAsync(ct);
        var perEmployee = workdayTargets.Where(t => t.UserId == userId).ToList();
        var globals = workdayTargets.Where(t => t.UserId == null).ToList();
        var weeklyTarget = (decimal?)SumWeeklyTarget(perEmployee, globals);

        return weekRanges.Select(w =>
        {
            var hoursLogged = (decimal)sessions
                .Where(s => s.Date >= w.Start && s.Date <= w.End)
                .Sum(CalcSessionHours);

            // ISO week number
            var weekNum = (w.Start.DayOfYear - 1) / 7 + 1;

            return new WeekSummaryDto
            {
                WeekLabel = $"W{weekNum}",
                WeekStart = w.Start.ToString("yyyy-MM-dd"),
                HoursLogged = Math.Round(hoursLogged, 2),
                Target = weeklyTarget,
            };
        });
    }

    // ─── Payroll export ───────────────────────────────────────────────────────

    public async Task<string> GeneratePayrollCsvAsync(int year, int month, string? userId = null, CancellationToken ct = default)
    {
        var monthStart = new DateOnly(year, month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var lastDate = monthEnd < today ? monthEnd : today;

        var employeesQuery = _context.Users.AsNoTracking().Where(u => u.Role == UserRole.Employee);
        if (!string.IsNullOrEmpty(userId))
            employeesQuery = employeesQuery.Where(u => u.Id == userId);
        var employees = await employeesQuery.OrderBy(u => u.FullName).ToListAsync(ct);
        var employeeIds = employees.Select(e => e.Id).ToList();

        var daySummaries = await GetAllDaySummariesAsync(userId, monthStart, lastDate, ct);
        var hoursByUserDate = daySummaries.ToDictionary(s => (s.UserId, s.Date), s => s);

        var workdayTargets = await _context.WorkdayTargets
            .AsNoTracking()
            .Where(t => t.UserId == null || employeeIds.Contains(t.UserId))
            .ToListAsync(ct);
        var globalWorkdayTargets = workdayTargets.Where(t => t.UserId == null).ToList();

        decimal GetBaseWeekdayTarget(string empUserId, DayOfWeek dayOfWeek)
        {
            var target = workdayTargets.FirstOrDefault(t => t.UserId == empUserId && t.DayOfWeek == dayOfWeek)
                       ?? globalWorkdayTargets.FirstOrDefault(t => t.DayOfWeek == dayOfWeek);
            return target?.Hours ?? 0m;
        }

        var holidaysQuery = _context.PublicHolidays
            .AsNoTracking()
            .Where(h => h.Date.Year == year && h.Date.Month == month && !h.IsWorkingDay);
        var holidayByDate = (await holidaysQuery.ToListAsync(ct))
            .Where(h => h.Date.DayOfWeek != DayOfWeek.Saturday && h.Date.DayOfWeek != DayOfWeek.Sunday)
            .ToDictionary(h => h.Date, h => h.Name);

        var vacationsQuery = _context.VacationDays
            .AsNoTracking()
            .Where(d => d.Date.Year == year && d.Date.Month == month);
        if (!string.IsNullOrEmpty(userId))
            vacationsQuery = vacationsQuery.Where(d => d.UserId == userId);

        var vacationsByUserDate = (await vacationsQuery
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
            .ToListAsync(ct))
            .GroupBy(v => (v.UserId, v.Date))
            .ToDictionary(g => g.Key, g => g.OrderByDescending(v => v.Amount).First());

        var settlementsQuery = _context.MonthlySettlements
            .AsNoTracking()
            .Where(s => s.Year == year && s.Month == month && s.Status == SettlementStatus.Settled);
        if (!string.IsNullOrEmpty(userId))
            settlementsQuery = settlementsQuery.Where(s => s.UserId == userId);
        var paidOvertimeByUser = await settlementsQuery
            .ToDictionaryAsync(s => s.UserId, s => s.PaidOutHours ?? 0m, ct);

        var rows = new List<(DateOnly Date, string EmployeeName, double Hours, string VacationType, string Description)>();

        foreach (var emp in employees)
        {
            for (var date = monthStart; date <= lastDate; date = date.AddDays(1))
            {
                var baseTarget = GetBaseWeekdayTarget(emp.Id, date.DayOfWeek);
                holidayByDate.TryGetValue(date, out var holidayName);
                var isHoliday = baseTarget > 0 && holidayName != null;
                var hasVacation = vacationsByUserDate.TryGetValue((emp.Id, date), out var vacation);
                hoursByUserDate.TryGetValue((emp.Id, date), out var daySummary);
                var workedHours = daySummary?.TotalHours ?? 0.0;

                if (baseTarget == 0 && workedHours == 0 && !hasVacation && !isHoliday)
                    continue;

                string vacationTypeCell;
                if (isHoliday)
                    vacationTypeCell = $"Holiday: {holidayName}";
                else if (hasVacation)
                    vacationTypeCell = vacation!.VacationTypeName;
                else if (baseTarget > 0 && workedHours == 0)
                    vacationTypeCell = "Missing Log";
                else
                    vacationTypeCell = "";

                var description = hasVacation && !string.IsNullOrEmpty(vacation!.Note)
                    ? vacation.Note
                    : daySummary?.Description ?? "";

                rows.Add((date, emp.FullName, workedHours, vacationTypeCell, description ?? ""));
            }
        }

        var sb = new System.Text.StringBuilder();

        sb.AppendLine("OVERTIME SUMMARY");
        sb.AppendLine("Employee,Approved Overtime Hours");
        foreach (var emp in employees)
        {
            var approvedOvertime = paidOvertimeByUser.TryGetValue(emp.Id, out var hours)
                ? hours.ToString("F2", CultureInfo.InvariantCulture)
                : "";
            sb.AppendLine(string.Join(",", CsvEscape(emp.FullName), approvedOvertime));
        }
        sb.AppendLine();

        sb.AppendLine("Date,Day,Employee,Hours Worked,Vacation Type,Description");

        foreach (var row in rows.OrderBy(r => r.Date).ThenBy(r => r.EmployeeName))
        {
            sb.AppendLine(string.Join(",",
                row.Date.ToString("yyyy-MM-dd"),
                row.Date.DayOfWeek.ToString(),
                CsvEscape(row.EmployeeName),
                row.Hours.ToString("F2", CultureInfo.InvariantCulture),
                CsvEscape(row.VacationType),
                CsvEscape(row.Description)
            ));
        }

        return sb.ToString();
    }

    private static string CsvEscape(string value)
    {
        if (value.Length > 0 && (value[0] == '=' || value[0] == '+' || value[0] == '-' || value[0] == '@'))
            value = "'" + value;
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }

    // ─── Workday targets (per-employee schedule) ──────────────────────────────

    public async Task<IEnumerable<WorkdayTargetDto>> GetEmployeeWorkdayTargetsAsync(string userId, CancellationToken ct = default)
    {
        return await _context.WorkdayTargets
            .Where(t => t.UserId == userId)
            .OrderBy(t => t.DayOfWeek)
            .Select(t => new WorkdayTargetDto { DayOfWeek = t.DayOfWeek, Hours = t.Hours })
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<WorkdayTargetDto>> SetEmployeeWorkdayTargetsAsync(
        string userId, IEnumerable<WorkdayTargetDto> targets, CancellationToken ct = default)
    {
        var targetList = targets.ToList();
        var submittedDays = targetList.Select(t => t.DayOfWeek).ToHashSet();

        var existing = await _context.WorkdayTargets
            .Where(t => t.UserId == userId)
            .ToListAsync(ct);

        // The submitted set is authoritative — a day missing from it means "inherit
        // the global default again", so any existing override for that day is cleared.
        _context.WorkdayTargets.RemoveRange(existing.Where(t => !submittedDays.Contains(t.DayOfWeek)));

        foreach (var dto in targetList)
        {
            var row = existing.FirstOrDefault(t => t.DayOfWeek == dto.DayOfWeek);
            if (row == null)
            {
                row = new WorkdayTarget { UserId = userId, DayOfWeek = dto.DayOfWeek };
                _context.WorkdayTargets.Add(row);
            }
            row.Hours = dto.Hours;
        }

        await _context.SaveChangesAsync(ct);
        return await GetEmployeeWorkdayTargetsAsync(userId, ct);
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

    // ── Time bank adjustments ─────────────────────────────────────────────────

    public async Task<IEnumerable<TimeBankAdjustmentDto>> GetTimeBankAdjustmentsAsync(
        string userId, int? year, int? month, CancellationToken ct = default)
    {
        var query = _context.TimeBankAdjustments
            .Include(a => a.CreatedByUser)
            .Where(a => a.UserId == userId)
            .AsQueryable();

        if (year.HasValue && month.HasValue)
        {
            var from = new DateOnly(year.Value, month.Value, 1);
            var to = new DateOnly(year.Value, month.Value, DateTime.DaysInMonth(year.Value, month.Value));
            query = query.Where(a => a.EffectiveDate >= from && a.EffectiveDate <= to);
        }
        else if (year.HasValue)
        {
            var from = new DateOnly(year.Value, 1, 1);
            var to = new DateOnly(year.Value, 12, 31);
            query = query.Where(a => a.EffectiveDate >= from && a.EffectiveDate <= to);
        }

        return await query
            .OrderByDescending(a => a.EffectiveDate)
            .ThenByDescending(a => a.CreatedAt)
            .Select(a => new TimeBankAdjustmentDto
            {
                Id = a.Id,
                UserId = a.UserId,
                EffectiveDate = a.EffectiveDate,
                Hours = a.Hours,
                Reason = a.Reason,
                SourceSettlementId = a.SourceSettlementId,
                CreatedByUserId = a.CreatedByUserId,
                CreatedByName = a.CreatedByUser != null ? a.CreatedByUser.FullName : null,
                CreatedAt = a.CreatedAt,
            })
            .ToListAsync(ct);
    }

    public async Task<TimeBankAdjustmentDto> CreateTimeBankAdjustmentAsync(
        string userId, CreateTimeBankAdjustmentDto dto, string adminUserId, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new ResourceNotFoundException("Employee not found.");

        if (await SettlementLockHelper.IsMonthSettledAsync(_context, userId, dto.EffectiveDate, ct))
            throw new ValidationException(
                "Cannot create a time bank adjustment for a month that has already been settled.");

        var adjustment = new TimeBankAdjustment
        {
            UserId = userId,
            EffectiveDate = dto.EffectiveDate,
            Hours = dto.Hours,
            Reason = dto.Reason,
            CreatedByUserId = adminUserId,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        _context.TimeBankAdjustments.Add(adjustment);
        await _context.SaveChangesAsync(ct);

        return new TimeBankAdjustmentDto
        {
            Id = adjustment.Id,
            UserId = adjustment.UserId,
            EffectiveDate = adjustment.EffectiveDate,
            Hours = adjustment.Hours,
            Reason = adjustment.Reason,
            CreatedByUserId = adjustment.CreatedByUserId,
            CreatedByName = (await _userManager.FindByIdAsync(adminUserId))?.FullName,
            CreatedAt = adjustment.CreatedAt,
        };
    }

    public async Task DeleteTimeBankAdjustmentAsync(int id, CancellationToken ct = default)
    {
        var adjustment = await _context.TimeBankAdjustments.FindAsync([id], ct)
            ?? throw new ResourceNotFoundException("Time bank adjustment not found.");

        if (adjustment.SourceSettlementId.HasValue)
            throw new ValidationException(
                "This adjustment was created automatically by a monthly settlement and cannot be deleted manually.");

        if (await SettlementLockHelper.IsMonthSettledAsync(_context, adjustment.UserId, adjustment.EffectiveDate, ct))
            throw new ValidationException(
                "Cannot delete a time bank adjustment from a month that has already been settled.");

        _context.TimeBankAdjustments.Remove(adjustment);
        await _context.SaveChangesAsync(ct);
    }
}
