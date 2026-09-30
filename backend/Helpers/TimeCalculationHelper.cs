using TimeManagementBackend.Models;

namespace TimeManagementBackend.Helpers;

public static class TimeCalculationHelper
{
    /// <summary>Returns the Monday–Sunday bounds of the current ISO week (UTC).</summary>
    public static (DateOnly Start, DateOnly End) GetCurrentWeekBounds()
    {
        var weekStart = GetWeekStart(DateOnly.FromDateTime(DateTime.UtcNow));
        return (weekStart, weekStart.AddDays(6));
    }

    /// <summary>Returns the Monday of the ISO week containing <paramref name="date"/>.</summary>
    public static DateOnly GetWeekStart(DateOnly date)
    {
        var dow = (int)date.DayOfWeek;
        var daysFromMonday = dow == 0 ? 6 : dow - 1;
        return date.AddDays(-daysFromMonday);
    }

    /// <summary>Whether <paramref name="date"/> falls on a Saturday or Sunday.</summary>
    public static bool IsWeekend(DateOnly date) =>
        date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

    /// <summary>Resolves the effective minimum break duration: the per-employee override, falling back to the global default.</summary>
    public static int? ResolveMinimumBreakMinutes(EmployeeTarget? employeeTarget, AppConfiguration? config) =>
        employeeTarget?.MinimumBreakMinutes ?? config?.MinimumBreakMinutes;

    /// <summary>Resolves the effective daily/weekly overtime allowance: the per-employee override, falling back to the global default. Null if neither is set — callers doing arithmetic should treat null as 0.</summary>
    public static (decimal? Daily, decimal? Weekly) ResolveOvertimeAllowances(EmployeeTarget? employeeTarget, AppConfiguration? config) => (
        employeeTarget?.DailyOvertimeAllowanceHours ?? config?.DefaultDailyOvertimeAllowanceHours,
        employeeTarget?.WeeklyOvertimeAllowanceHours ?? config?.DefaultWeeklyOvertimeAllowanceHours
    );

    /// <summary>
    /// Resolves the effective workday-target hours for <paramref name="dayOfWeek"/>: the row
    /// scoped to <paramref name="userId"/> if one exists in <paramref name="targets"/>, falling
    /// back to the global row (UserId == null), and finally to 0 if neither exists.
    /// </summary>
    public static decimal ResolveWorkdayTarget(IEnumerable<WorkdayTarget> targets, string userId, DayOfWeek dayOfWeek)
    {
        WorkdayTarget? userRow = null;
        WorkdayTarget? globalRow = null;
        foreach (var t in targets)
        {
            if (t.DayOfWeek != dayOfWeek) continue;
            if (t.UserId == userId) userRow = t;
            else if (t.UserId == null) globalRow = t;
        }
        return (userRow ?? globalRow)?.Hours ?? 0m;
    }
}
