using TimeManagementBackend.Helpers;
using TimeManagementBackend.Models;

namespace TimeManagementBackend.Tests.Helpers;

public class TimeCalculationHelperTests
{
    // ── GetWeekStart ──────────────────────────────────────────────────────────

    [Theory]
    // A whole ISO week must collapse onto the same Monday, Sunday included — getting
    // Sunday wrong is the classic off-by-one that silently misgroups weekly overtime.
    [InlineData("2026-03-02", "2026-03-02")] // Monday itself
    [InlineData("2026-03-03", "2026-03-02")] // Tuesday
    [InlineData("2026-03-06", "2026-03-02")] // Friday
    [InlineData("2026-03-07", "2026-03-02")] // Saturday
    [InlineData("2026-03-08", "2026-03-02")] // Sunday belongs to the week that started Mar 2
    [InlineData("2026-03-09", "2026-03-09")] // next Monday starts a new week
    public void GetWeekStart_ReturnsMondayOfIsoWeek(string date, string expectedMonday)
    {
        var result = TimeCalculationHelper.GetWeekStart(DateOnly.Parse(date));

        Assert.Equal(DateOnly.Parse(expectedMonday), result);
        Assert.Equal(DayOfWeek.Monday, result.DayOfWeek);
    }

    [Fact]
    public void GetWeekStart_CrossesMonthAndYearBoundaries()
    {
        // 1 Jan 2027 is a Friday, so its week starts in the previous month and year.
        Assert.Equal(new DateOnly(2026, 12, 28), TimeCalculationHelper.GetWeekStart(new DateOnly(2027, 1, 1)));
    }

    [Fact]
    public void GetCurrentWeekBounds_SpansMondayToSunday()
    {
        var (start, end) = TimeCalculationHelper.GetCurrentWeekBounds();

        Assert.Equal(DayOfWeek.Monday, start.DayOfWeek);
        Assert.Equal(DayOfWeek.Sunday, end.DayOfWeek);
        Assert.Equal(6, end.DayNumber - start.DayNumber);
        Assert.Equal(start, TimeCalculationHelper.GetWeekStart(DateOnly.FromDateTime(DateTime.UtcNow)));
    }

    // ── IsWeekend ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("2026-03-07", true)]  // Saturday
    [InlineData("2026-03-08", true)]  // Sunday
    [InlineData("2026-03-06", false)] // Friday
    [InlineData("2026-03-09", false)] // Monday
    public void IsWeekend_IdentifiesSaturdayAndSunday(string date, bool expected)
        => Assert.Equal(expected, TimeCalculationHelper.IsWeekend(DateOnly.Parse(date)));

    // ── ResolveMinimumBreakMinutes ────────────────────────────────────────────

    [Fact]
    public void ResolveMinimumBreakMinutes_PrefersEmployeeOverrideOverGlobalDefault()
    {
        var target = new EmployeeTarget { MinimumBreakMinutes = 15 };
        var config = new AppConfiguration { MinimumBreakMinutes = 30 };

        Assert.Equal(15, TimeCalculationHelper.ResolveMinimumBreakMinutes(target, config));
    }

    [Fact]
    public void ResolveMinimumBreakMinutes_FallsBackToGlobalWhenEmployeeHasNoOverride()
    {
        var target = new EmployeeTarget { MinimumBreakMinutes = null };
        var config = new AppConfiguration { MinimumBreakMinutes = 30 };

        Assert.Equal(30, TimeCalculationHelper.ResolveMinimumBreakMinutes(target, config));
        Assert.Equal(30, TimeCalculationHelper.ResolveMinimumBreakMinutes(null, config));
    }

    [Fact]
    public void ResolveMinimumBreakMinutes_IsNullWhenNeitherIsConfigured()
    {
        Assert.Null(TimeCalculationHelper.ResolveMinimumBreakMinutes(null, null));
        Assert.Null(TimeCalculationHelper.ResolveMinimumBreakMinutes(
            new EmployeeTarget(), new AppConfiguration()));
    }

    [Fact]
    public void ResolveMinimumBreakMinutes_TreatsExplicitZeroOverrideAsMeaningful()
    {
        // 0 is "this employee has no minimum", which must win over a global 30 rather
        // than being coalesced away as if it were null.
        var target = new EmployeeTarget { MinimumBreakMinutes = 0 };
        var config = new AppConfiguration { MinimumBreakMinutes = 30 };

        Assert.Equal(0, TimeCalculationHelper.ResolveMinimumBreakMinutes(target, config));
    }

    // ── ResolveOvertimeAllowances ─────────────────────────────────────────────

    [Fact]
    public void ResolveOvertimeAllowances_ResolvesDailyAndWeeklyIndependently()
    {
        // Only the daily value is overridden — the weekly one must still fall back.
        var target = new EmployeeTarget { DailyOvertimeAllowanceHours = 1.5m, WeeklyOvertimeAllowanceHours = null };
        var config = new AppConfiguration
        {
            DefaultDailyOvertimeAllowanceHours = 2m,
            DefaultWeeklyOvertimeAllowanceHours = 5m,
        };

        var (daily, weekly) = TimeCalculationHelper.ResolveOvertimeAllowances(target, config);

        Assert.Equal(1.5m, daily);
        Assert.Equal(5m, weekly);
    }

    [Fact]
    public void ResolveOvertimeAllowances_AreNullWhenNothingIsConfigured()
    {
        var (daily, weekly) = TimeCalculationHelper.ResolveOvertimeAllowances(null, null);

        Assert.Null(daily);
        Assert.Null(weekly);
    }

    // ── ResolveWorkdayTarget ──────────────────────────────────────────────────

    [Fact]
    public void ResolveWorkdayTarget_PrefersTheRowScopedToTheUser()
    {
        var targets = new List<WorkdayTarget>
        {
            new() { UserId = null,   DayOfWeek = DayOfWeek.Monday, Hours = 8m },
            new() { UserId = "u1",   DayOfWeek = DayOfWeek.Monday, Hours = 6m },
        };

        Assert.Equal(6m, TimeCalculationHelper.ResolveWorkdayTarget(targets, "u1", DayOfWeek.Monday));
    }

    [Fact]
    public void ResolveWorkdayTarget_FallsBackToGlobalRowForAnotherUser()
    {
        var targets = new List<WorkdayTarget>
        {
            new() { UserId = null, DayOfWeek = DayOfWeek.Monday, Hours = 8m },
            new() { UserId = "u1", DayOfWeek = DayOfWeek.Monday, Hours = 6m },
        };

        // u2 has no override, so it must not inherit u1's.
        Assert.Equal(8m, TimeCalculationHelper.ResolveWorkdayTarget(targets, "u2", DayOfWeek.Monday));
    }

    [Fact]
    public void ResolveWorkdayTarget_IsZeroWhenNeitherRowExists()
        => Assert.Equal(0m, TimeCalculationHelper.ResolveWorkdayTarget([], "u1", DayOfWeek.Monday));

    [Fact]
    public void ResolveWorkdayTarget_IgnoresRowsForOtherWeekdays()
    {
        var targets = new List<WorkdayTarget>
        {
            new() { UserId = "u1", DayOfWeek = DayOfWeek.Tuesday, Hours = 6m },
        };

        Assert.Equal(0m, TimeCalculationHelper.ResolveWorkdayTarget(targets, "u1", DayOfWeek.Monday));
    }

    [Fact]
    public void ResolveWorkdayTarget_HonoursAnExplicitZeroHourOverride()
    {
        // A part-timer who does not work Fridays has an override of 0, which must beat
        // the global 8 rather than being treated as "no row".
        var targets = new List<WorkdayTarget>
        {
            new() { UserId = null, DayOfWeek = DayOfWeek.Friday, Hours = 8m },
            new() { UserId = "u1", DayOfWeek = DayOfWeek.Friday, Hours = 0m },
        };

        Assert.Equal(0m, TimeCalculationHelper.ResolveWorkdayTarget(targets, "u1", DayOfWeek.Friday));
    }
}
