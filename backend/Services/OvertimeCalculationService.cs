using Microsoft.EntityFrameworkCore;
using TimeManagementBackend.Data;
using TimeManagementBackend.Models;
using TimeManagementBackend.Models.DTOs;

namespace TimeManagementBackend.Services;

public class OvertimeCalculationService(AppDbContext db) : IOvertimeCalculationService
{
    public async Task<OvertimeResultDto> CalculateAsync(
        string userId, int year, int month, CancellationToken ct = default)
    {
        var monthStart = new DateOnly(year, month, 1);
        var monthEnd = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Cap at today so future days don't appear in PerDay or the balance
        var endDate = monthEnd < today ? monthEnd : today;

        // ── Load data for the month in bulk ──────────────────────────────────────

        var sessions = await db.WorkSessions
            .AsNoTracking()
            .Include(s => s.Breaks)
            .Where(s => s.UserId == userId && s.Date >= monthStart && s.Date <= endDate)
            .ToListAsync(ct);

        var adjustmentHours = await db.TimeBankAdjustments
            .Where(a => a.UserId == userId
                     && a.EffectiveDate >= monthStart
                     && a.EffectiveDate <= endDate)
            .SumAsync(a => (decimal?)a.Hours, ct) ?? 0m;

        var config = await db.AppConfigurations.AsNoTracking().FirstOrDefaultAsync(ct);
        var employeeTarget = await db.EmployeeTargets
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.UserId == userId, ct);

        var dailyAllowanceHours = employeeTarget?.DailyOvertimeAllowanceHours
            ?? config?.DefaultDailyOvertimeAllowanceHours
            ?? 0m;
        var weeklyAllowanceHours = employeeTarget?.WeeklyOvertimeAllowanceHours
            ?? config?.DefaultWeeklyOvertimeAllowanceHours
            ?? 0m;

        var nonWorkingHolidayDates = await db.PublicHolidays
            .Where(h => h.Date >= monthStart && h.Date <= endDate && !h.IsWorkingDay)
            .Select(h => h.Date)
            .ToHashSetAsync(ct);

        var vacationTotalByDate = await db.VacationDays
            .Where(v => v.UserId == userId && v.Date >= monthStart && v.Date <= endDate)
            .GroupBy(v => v.Date)
            .Select(g => new { Date = g.Key, Total = g.Sum(v => v.Amount) })
            .ToDictionaryAsync(g => g.Date, g => g.Total, ct);

        var workdayTargets = await db.WorkdayTargets
            .AsNoTracking()
            .Where(t => t.UserId == userId || t.UserId == null)
            .ToListAsync(ct);

        decimal GetEffectiveTarget(DateOnly d)
        {
            if (d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) return 0;
            if (nonWorkingHolidayDates.Contains(d)) return 0;

            var leaveTotal = vacationTotalByDate.TryGetValue(d, out var amt) ? amt : 0m;
            var leaveFraction = Math.Min(1.0m, leaveTotal);
            if (leaveFraction >= 1.0m) return 0;

            var effective = workdayTargets.FirstOrDefault(t => t.UserId == userId && t.DayOfWeek == d.DayOfWeek)
                         ?? workdayTargets.FirstOrDefault(t => t.UserId == null && t.DayOfWeek == d.DayOfWeek);

            if (effective == null || effective.Hours == 0) return 0;
            return effective.Hours * (1 - leaveFraction);
        }

        var sessionsByDate = sessions.GroupBy(s => s.Date).ToDictionary(g => g.Key, g => g.ToList());

        // ── Per-day calculation ───────────────────────────────────────────────────

        var minimumBreakMinutes = employeeTarget?.MinimumBreakMinutes ?? config?.MinimumBreakMinutes;

        var perDay = new List<PerDayOvertimeDto>();
        long totalBalanceMinutes = 0;

        for (var date = monthStart; date <= endDate; date = date.AddDays(1))
        {
            var targetHours = GetEffectiveTarget(date);
            var targetMinutes = (long)Math.Round(targetHours * 60);

            var dayWorkedMinutes = 0L;
            int? breakAutoDeductedMinutes = null;
            if (sessionsByDate.TryGetValue(date, out var daySessions))
            {
                // Day still in progress — exclude from balance to avoid a false deficit.
                if (daySessions.Any(s => s.Status == WorkSessionStatus.Open))
                {
                    perDay.Add(new PerDayOvertimeDto
                    {
                        Date = date,
                        WorkedHours = 0,
                        TargetHours = targetHours,
                        FlexDelta = 0,
                    });
                    continue;
                }

                var closedSessions = daySessions.Where(s => s.Status == WorkSessionStatus.Closed).ToList();
                var dayBreakMinutes = 0L;
                foreach (var s in closedSessions)
                {
                    var breakMinutes = s.Breaks
                        .Where(b => b.BreakEnd != null)
                        .Sum(b => (long)(b.BreakEnd!.Value - b.BreakStart).TotalMinutes);
                    dayBreakMinutes += breakMinutes;
                    var sessionMinutes = (long)(s.ClockOut!.Value - s.ClockIn).TotalMinutes - breakMinutes;
                    dayWorkedMinutes += Math.Max(0, sessionMinutes);
                }

                // No break was logged anywhere in the day's closed sessions — deduct the
                // resolved minimum break so unrecorded breaks don't inflate the flex balance.
                if (closedSessions.Count > 0 && dayBreakMinutes == 0 && minimumBreakMinutes is > 0)
                {
                    breakAutoDeductedMinutes = minimumBreakMinutes.Value;
                    dayWorkedMinutes = Math.Max(0, dayWorkedMinutes - minimumBreakMinutes.Value);
                }
            }

            var flexDeltaMinutes = dayWorkedMinutes - targetMinutes;
            totalBalanceMinutes += flexDeltaMinutes;

            perDay.Add(new PerDayOvertimeDto
            {
                Date = date,
                WorkedHours = Math.Round(dayWorkedMinutes / 60m, 2),
                TargetHours = targetHours,
                FlexDelta = Math.Round(flexDeltaMinutes / 60m, 2),
                BreakAutoDeductedMinutes = breakAutoDeductedMinutes,
            });
        }

        // ── Compliance flags ──────────────────────────────────────────────────────

        var complianceFlags = new List<ComplianceFlagDto>();

        // Daily violations
        foreach (var day in perDay)
        {
            if (day.TargetHours <= 0) continue;
            var dailyThreshold = day.TargetHours + dailyAllowanceHours;
            if (day.WorkedHours > dailyThreshold)
            {
                complianceFlags.Add(new ComplianceFlagDto
                {
                    Date = day.Date,
                    Type = ComplianceFlagType.DailyOvertime,
                    HoursWorked = day.WorkedHours,
                    Threshold = dailyThreshold,
                });
            }
        }

        // Weekly violations — group by ISO week start (Monday)
        var weekGroups = perDay
            .GroupBy(d => GetIsoWeekStart(d.Date))
            .OrderBy(g => g.Key);

        foreach (var week in weekGroups)
        {
            var weekWorkedHours = week.Sum(d => d.WorkedHours);
            var weekTargetHours = week.Sum(d => d.TargetHours);

            if (weekTargetHours <= 0) continue;

            var weeklyThreshold = weekTargetHours + weeklyAllowanceHours;
            if (weekWorkedHours > weeklyThreshold)
            {
                complianceFlags.Add(new ComplianceFlagDto
                {
                    Date = week.Key,
                    Type = ComplianceFlagType.WeeklyOvertime,
                    HoursWorked = weekWorkedHours,
                    Threshold = weeklyThreshold,
                });
            }
        }

        // ── Final result ──────────────────────────────────────────────────────────

        var adjustmentMinutes = (long)Math.Round(adjustmentHours * 60);
        var runningBalanceMinutes = totalBalanceMinutes + adjustmentMinutes;

        return new OvertimeResultDto
        {
            Year = year,
            Month = month,
            PerDay = perDay,
            RunningBalanceHours = Math.Round(runningBalanceMinutes / 60m, 2),
            ComplianceFlags = complianceFlags,
        };
    }

    private static DateOnly GetIsoWeekStart(DateOnly date)
    {
        var dayOfWeek = (int)date.DayOfWeek;
        var daysFromMonday = dayOfWeek == 0 ? 6 : dayOfWeek - 1;
        return date.AddDays(-daysFromMonday);
    }
}
