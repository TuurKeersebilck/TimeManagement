using Microsoft.EntityFrameworkCore;
using TimeManagementBackend.Data;
using TimeManagementBackend.Helpers;
using TimeManagementBackend.Models;
using TimeManagementBackend.Models.DTOs;

namespace TimeManagementBackend.Services;

public class OvertimeCalculationService(AppDbContext db, TimeProvider? clock = null) : IOvertimeCalculationService
{
    public async Task<OvertimeResultDto> CalculateAsync(
        string userId, int year, int month, CancellationToken ct = default)
    {
        var monthStart = new DateOnly(year, month, 1);
        var monthEnd = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
        // The business day, not the UTC one — in Belgium the UTC date only rolls over at 01:00/02:00.
        var today = TimeCalculationHelper.BusinessDate((clock ?? TimeProvider.System).GetUtcNow());

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

        var (dailyAllowanceHoursOrNull, weeklyAllowanceHoursOrNull) =
            TimeCalculationHelper.ResolveOvertimeAllowances(employeeTarget, config);
        var dailyAllowanceHours = dailyAllowanceHoursOrNull ?? 0m;
        var weeklyAllowanceHours = weeklyAllowanceHoursOrNull ?? 0m;

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

        // There is no start or leave date per employee yet, so the first logged session stands in
        // for the start date and, for a disabled employee, the last one for the leave date. Days
        // outside that span get no target — otherwise someone who starts mid-month would carry a
        // deficit for every working day before they joined. Before the very first clock-in the
        // span starts today, so a new hire still shows as "not clocked in" on their first day.
        var activity = await db.WorkSessions
            .Where(s => s.UserId == userId)
            .GroupBy(s => s.UserId)
            .Select(g => new { First = g.Min(s => s.Date), Last = g.Max(s => s.Date) })
            .FirstOrDefaultAsync(ct);
        var isDisabled = await db.Users
            .Where(u => u.Id == userId)
            .Select(u => u.IsDisabled)
            .FirstOrDefaultAsync(ct);
        var employmentStart = activity?.First ?? today;
        DateOnly? employmentEnd = isDisabled && activity != null ? activity.Last : null;

        decimal GetEffectiveTarget(DateOnly d)
        {
            if (d < employmentStart || d > employmentEnd) return 0;
            if (TimeCalculationHelper.IsWeekend(d)) return 0;
            if (nonWorkingHolidayDates.Contains(d)) return 0;

            var leaveTotal = vacationTotalByDate.TryGetValue(d, out var amt) ? amt : 0m;
            var leaveFraction = Math.Min(1.0m, leaveTotal);
            if (leaveFraction >= 1.0m) return 0;

            var targetHours = TimeCalculationHelper.ResolveWorkdayTarget(workdayTargets, userId, d.DayOfWeek);
            if (targetHours == 0) return 0;
            return targetHours * (1 - leaveFraction);
        }

        var sessionsByDate = sessions.GroupBy(s => s.Date).ToDictionary(g => g.Key, g => g.ToList());

        // ── Per-day calculation ───────────────────────────────────────────────────

        var minimumBreakMinutes = TimeCalculationHelper.ResolveMinimumBreakMinutes(employeeTarget, config);

        var perDay = new List<PerDayOvertimeDto>();
        long totalBalanceMinutes = 0;

        for (var date = monthStart; date <= endDate; date = date.AddDays(1))
        {
            var targetHours = GetEffectiveTarget(date);
            var targetMinutes = (long)Math.Round(targetHours * 60);

            // Today isn't over yet, so it never counts against the employee: a deficit only lands
            // once the day ends, while hours beyond the target show up straight away. Without this
            // everyone would open each day a full target behind, and drop back mid-day whenever
            // they clock out for lunch.
            var isToday = date == today;

            var dayWorkedMinutes = 0L;
            int? breakAutoDeductedMinutes = null;
            if (sessionsByDate.TryGetValue(date, out var daySessions))
            {
                // An earlier day still clocked in (e.g. a shift past midnight) — exclude it from the
                // balance until it is closed or invalidated, to avoid a false deficit.
                if (!isToday && daySessions.Any(s => s.Status == WorkSessionStatus.Open))
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
                // A break taken in today's still-open session counts as "a break was logged".
                var breakLoggedInOpenSession = daySessions
                    .Any(s => s.Status == WorkSessionStatus.Open && s.Breaks.Any(b => b.BreakEnd != null));
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
                if (closedSessions.Count > 0 && dayBreakMinutes == 0 && !breakLoggedInOpenSession
                    && minimumBreakMinutes is > 0)
                {
                    breakAutoDeductedMinutes = minimumBreakMinutes.Value;
                    dayWorkedMinutes = Math.Max(0, dayWorkedMinutes - minimumBreakMinutes.Value);
                }
            }

            var flexDeltaMinutes = dayWorkedMinutes - targetMinutes;
            if (isToday) flexDeltaMinutes = Math.Max(0, flexDeltaMinutes);
            totalBalanceMinutes += flexDeltaMinutes;

            perDay.Add(new PerDayOvertimeDto
            {
                Date = date,
                WorkedHours = Math.Round(dayWorkedMinutes / 60m, 2),
                TargetHours = targetHours,
                FlexDelta = Math.Round(flexDeltaMinutes / 60m, 2),
                BreakAutoDeductedMinutes = breakAutoDeductedMinutes,
                IsProvisional = isToday,
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
            .GroupBy(d => TimeCalculationHelper.GetWeekStart(d.Date))
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
}
