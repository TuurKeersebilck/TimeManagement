using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
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

    public async Task<IEnumerable<EmployeeDto>> GetEmployeesAsync(CancellationToken ct = default)
    {
        var config = await _context.AppConfigurations.FirstOrDefaultAsync(ct);

        var (weekStart, weekEnd) = TimeCalculationHelper.GetCurrentWeekBounds();

        var users = await _context.Users
            .AsNoTracking()
            .OrderBy(u => u.FullName)
            .ToListAsync(ct);

        var weekSessions = await _context.WorkSessions
            .AsNoTracking()
            .Include(s => s.Breaks)
            .Where(s => s.Status == WorkSessionStatus.Closed && s.Date >= weekStart && s.Date <= weekEnd)
            .ToListAsync(ct);

        var allWorkdayTargets = await _context.WorkdayTargets
            .AsNoTracking()
            .Where(t => s_weekdays.Contains(t.DayOfWeek))
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
            DailyHours = target?.DailyHours,
            WeeklyHours = target?.WeeklyHours,
            ResolvedDailyHours = target?.DailyHours ?? config?.DefaultDailyHours,
            ResolvedWeeklyHours = target?.WeeklyHours ?? config?.DefaultWeeklyHours,
            HasOverride = target != null && (target.DailyHours.HasValue || target.WeeklyHours.HasValue || target.MinimumBreakMinutes.HasValue),
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

        target.DailyHours = dto.DailyHours;
        target.WeeklyHours = dto.WeeklyHours;
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
            .Where(t => (t.UserId == userId || t.UserId == null) && s_weekdays.Contains(t.DayOfWeek))
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

    public async Task<string> GeneratePayrollCsvAsync(int year, int month, string? userId = null, int timezoneOffsetMinutes = 0, CancellationToken ct = default)
    {
        var dateFrom = new DateOnly(year, month, 1);
        var dateTo = dateFrom.AddMonths(1).AddDays(-1);

        var sessionsQuery = _context.WorkSessions
            .AsNoTracking()
            .Include(s => s.User)
            .Include(s => s.Breaks)
            .Where(s => s.Status == WorkSessionStatus.Closed && s.Date >= dateFrom && s.Date <= dateTo);

        if (!string.IsNullOrEmpty(userId))
            sessionsQuery = sessionsQuery.Where(s => s.UserId == userId);

        var rawSessions = await sessionsQuery.ToListAsync(ct);

        var userIdListCsv = rawSessions.Select(s => s.UserId).Distinct().ToList();
        var dateListCsv = rawSessions.Select(s => s.Date).Distinct().ToList();
        var workDaysCsv = await _context.WorkDays
            .AsNoTracking()
            .Where(d => userIdListCsv.Contains(d.UserId) && dateListCsv.Contains(d.Date))
            .ToListAsync(ct);

        var logs = rawSessions
            .GroupBy(s => new { s.UserId, s.Date })
            .Select(g =>
            {
                var first = g.First();
                var workDay = workDaysCsv.FirstOrDefault(d => d.UserId == g.Key.UserId && d.Date == g.Key.Date);
                return new AdminDaySummaryDto
                {
                    UserId = g.Key.UserId,
                    EmployeeName = first.User.FullName,
                    EmployeeEmail = first.User.Email ?? string.Empty,
                    Date = g.Key.Date,
                    TotalHours = g.Sum(CalcSessionHours),
                    Description = workDay?.Description,
                    WorkedFromHome = workDay?.WorkedFromHome ?? false,
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
            .OrderBy(s => s.EmployeeName)
            .ThenBy(s => s.Date)
            .ToList();

        var publicHolidays = (await _context.PublicHolidays
            .AsNoTracking()
            .Where(h => h.Date.Year == year && h.Date.Month == month && !h.IsWorkingDay)
            .OrderBy(h => h.Date)
            .ToListAsync(ct))
            .Where(h => h.Date.DayOfWeek != DayOfWeek.Saturday && h.Date.DayOfWeek != DayOfWeek.Sunday)
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

        var settlementsQuery = _context.MonthlySettlements
            .AsNoTracking()
            .Where(s => s.Year == year && s.Month == month);

        if (!string.IsNullOrEmpty(userId))
            settlementsQuery = settlementsQuery.Where(s => s.UserId == userId);

        var settlements = await settlementsQuery.ToListAsync(ct);
        var settlementByUser = settlements.ToDictionary(s => s.UserId);

        var sb = new System.Text.StringBuilder();
        var monthName = new System.Globalization.CultureInfo("en-GB").DateTimeFormat.GetMonthName(month);

        sb.AppendLine($"Payroll Report,{monthName} {year}");
        sb.AppendLine($"Generated,{DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC");
        sb.AppendLine();

        // ── Section 1: Summary ──────────────────────────────────────────────
        sb.AppendLine("SUMMARY");
        sb.AppendLine("Name,Email,Days Worked,Regular Hours,Overtime Hours,Total Hours,Vacation Days,Outcome,Notes");

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

            settlementByUser.TryGetValue(emp.UserId, out var settlement);
            var overtimeHours = (double)(settlement?.OvertimeHours ?? 0m);
            var regularHours = Math.Max(0, totalHours - overtimeHours);
            var outcome = settlement?.Outcome.HasValue == true ? settlement.Outcome!.Value.ToString() : "";
            var notes = settlement?.Notes ?? "";

            sb.AppendLine(string.Join(",",
                CsvEscape(emp.Name),
                CsvEscape(emp.Email),
                daysWorked,
                regularHours.ToString("F2"),
                overtimeHours.ToString("F2"),
                totalHours.ToString("F2"),
                vacDays.ToString("F1"),
                CsvEscape(outcome),
                CsvEscape(notes)
            ));
        }

        // ── Section 2: Daily Detail ─────────────────────────────────────────
        var vacationLookup = vacations
            .GroupBy(v => (v.UserId, v.Date))
            .ToDictionary(g => g.Key, g => g.ToList());

        sb.AppendLine();
        sb.AppendLine("DAILY DETAIL");
        sb.AppendLine("Employee,Email,Date,Day,Clock In,Clock Out,Break Duration (h),Net Hours,WFH,Vacation,Description");

        foreach (var log in logs)
        {
            var dayVacs = vacationLookup.TryGetValue((log.UserId, log.Date), out var vl) ? vl : null;
            var vacationCell = dayVacs != null
                ? string.Join("; ", dayVacs.Select(v => $"{(v.Amount == 1.0m ? "Full" : "Half")} ({v.VacationTypeName})"))
                : "";

            foreach (var session in log.Sessions)
            {
                var breakHours = session.Breaks
                    .Where(b => b.BreakEnd.HasValue)
                    .Sum(b => (b.BreakEnd!.Value - b.BreakStart).TotalHours);

                sb.AppendLine(string.Join(",",
                    CsvEscape(log.EmployeeName),
                    CsvEscape(log.EmployeeEmail),
                    log.Date.ToString("yyyy-MM-dd"),
                    log.Date.DayOfWeek.ToString(),
                    FormatExportTime(session.ClockIn, timezoneOffsetMinutes),
                    FormatExportTime(session.ClockOut, timezoneOffsetMinutes),
                    breakHours.ToString("F2"),
                    session.Hours.ToString("F2"),
                    log.WorkedFromHome ? "Yes" : "No",
                    CsvEscape(vacationCell),
                    CsvEscape(log.Description ?? "")
                ));
            }
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

        // ── Section 4: Public Holidays ──────────────────────────────────────
        if (publicHolidays.Count != 0)
        {
            sb.AppendLine();
            sb.AppendLine("PUBLIC HOLIDAYS");
            sb.AppendLine("Date,Day,Name");

            foreach (var h in publicHolidays)
            {
                sb.AppendLine(string.Join(",",
                    h.Date.ToString("yyyy-MM-dd"),
                    h.Date.DayOfWeek.ToString(),
                    CsvEscape(h.Name)
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
        var existing = await _context.WorkdayTargets
            .Where(t => t.UserId == userId)
            .ToListAsync(ct);

        foreach (var dto in targets)
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
                CreatedByUserId = a.CreatedByUserId,
                CreatedByName = a.CreatedByUser != null ? a.CreatedByUser.FullName : null,
                CreatedAt = a.CreatedAt,
            })
            .ToListAsync(ct);
    }

    public async Task<TimeBankAdjustmentDto> CreateTimeBankAdjustmentAsync(
        CreateTimeBankAdjustmentDto dto, string adminUserId, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(dto.UserId)
            ?? throw new ResourceNotFoundException("Employee not found.");

        var adjustment = new TimeBankAdjustment
        {
            UserId = dto.UserId,
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

        _context.TimeBankAdjustments.Remove(adjustment);
        await _context.SaveChangesAsync(ct);
    }
}
