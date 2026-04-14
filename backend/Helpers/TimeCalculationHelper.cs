namespace TimeManagementBackend.Helpers;

public static class TimeCalculationHelper
{
    public static double CalculateWorkedHours(
        DateTimeOffset start, DateTimeOffset end, DateTimeOffset? breakStart, DateTimeOffset? breakEnd)
    {
        var worked = end - start;
        if (breakStart.HasValue && breakEnd.HasValue)
            worked -= breakEnd.Value - breakStart.Value;
        return worked.TotalHours;
    }

    /// <summary>Returns the Monday–Sunday bounds of the current ISO week (UTC).</summary>
    public static (DateOnly Start, DateOnly End) GetCurrentWeekBounds()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var dow = (int)today.DayOfWeek;
        var daysFromMonday = dow == 0 ? 6 : dow - 1;
        var weekStart = today.AddDays(-daysFromMonday);
        return (weekStart, weekStart.AddDays(6));
    }
}
