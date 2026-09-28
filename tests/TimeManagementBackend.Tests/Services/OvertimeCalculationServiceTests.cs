using TimeManagementBackend.Models;
using TimeManagementBackend.Models.DTOs;
using TimeManagementBackend.Services;
using TimeManagementBackend.Tests.Infrastructure;

namespace TimeManagementBackend.Tests.Services;

/// <summary>
/// The flex balance these tests pin down is what settlements pay out on, so an error here
/// becomes an error on someone's payslip. March 2026 is used throughout: it is safely in the
/// past, so the "cap at today" rule never truncates the month and results stay deterministic.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class OvertimeCalculationServiceTests(PostgresFixture fixture) : DatabaseTestBase(fixture)
{
    private static readonly DateOnly Monday = new(2026, 3, 2);
    private static readonly DateOnly Tuesday = new(2026, 3, 3);
    private static readonly DateOnly Saturday = new(2026, 3, 7);
    private static readonly DateOnly NextMonday = new(2026, 3, 9);

    private OvertimeCalculationService NewService() => new(NewContext());

    private static PerDayOvertimeDto Day(OvertimeResultDto result, DateOnly date)
        => result.PerDay.Single(d => d.Date == date);

    /// <summary>One user whose only working day of the week is Monday at 8h.</summary>
    private async Task<User> ArrangeMondayOnlyScheduleAsync(int? minimumBreakMinutes = null)
    {
        var user = Db.AddUser();
        if (minimumBreakMinutes.HasValue)
            Db.AddConfiguration(minimumBreakMinutes: minimumBreakMinutes);
        await Db.SaveChangesAsync();
        await Db.SetGlobalScheduleAsync((DayOfWeek.Monday, 8m));
        return user;
    }

    // ── Worked hours ──────────────────────────────────────────────────────────

    [Fact]
    public async Task WorkedHours_ExcludeLoggedBreakTime()
    {
        var user = await ArrangeMondayOnlyScheduleAsync();
        // 09:00–17:30 is 8.5h gross, minus a 30 min break = 8h net, exactly on target.
        Db.AddClosedSession(user.Id, Monday, TimeSpan.FromHours(9), new TimeSpan(17, 30, 0),
            aBreak: (new TimeSpan(12, 0, 0), new TimeSpan(12, 30, 0)));
        await Db.SaveChangesAsync();

        var result = await NewService().CalculateAsync(user.Id, 2026, 3);

        Assert.Equal(8m, Day(result, Monday).WorkedHours);
        Assert.Equal(8m, Day(result, Monday).TargetHours);
        Assert.Equal(0m, Day(result, Monday).FlexDelta);
        // The worked Monday nets out; March 2026 has four further unworked Mondays at -8h.
        Assert.Equal(-32m, result.RunningBalanceHours);
    }

    [Fact]
    public async Task MultipleSessionsOnOneDay_AreSummed()
    {
        var user = await ArrangeMondayOnlyScheduleAsync();
        Db.AddClosedSession(user.Id, Monday, TimeSpan.FromHours(9), TimeSpan.FromHours(12));
        Db.AddClosedSession(user.Id, Monday, TimeSpan.FromHours(13), TimeSpan.FromHours(18));
        await Db.SaveChangesAsync();

        var result = await NewService().CalculateAsync(user.Id, 2026, 3);

        Assert.Equal(8m, Day(result, Monday).WorkedHours);
    }

    [Fact]
    public async Task SurplusAndDeficitAccumulateIntoTheRunningBalance()
    {
        var user = Db.AddUser();
        await Db.SaveChangesAsync();
        await Db.SetGlobalScheduleAsync((DayOfWeek.Monday, 8m), (DayOfWeek.Tuesday, 8m));

        Db.AddClosedSession(user.Id, Monday, TimeSpan.FromHours(9), TimeSpan.FromHours(19));   // +2
        Db.AddClosedSession(user.Id, Tuesday, TimeSpan.FromHours(9), TimeSpan.FromHours(15));  // -2
        Db.AddClosedSession(user.Id, NextMonday, TimeSpan.FromHours(9), TimeSpan.FromHours(18)); // +1
        await Db.SaveChangesAsync();

        var result = await NewService().CalculateAsync(user.Id, 2026, 3);

        Assert.Equal(2m, Day(result, Monday).FlexDelta);
        Assert.Equal(-2m, Day(result, Tuesday).FlexDelta);
        // Mondays 16, 23, 30 and Tuesdays 10, 17, 24, 31 are unworked 8h days: 7 x -8 = -56.
        Assert.Equal(2m - 2m + 1m - 56m, result.RunningBalanceHours);
    }

    [Fact]
    public async Task OtherEmployeesSessionsAreNeverCounted()
    {
        var user = await ArrangeMondayOnlyScheduleAsync();
        var colleague = Db.AddUser("Colin Colleague");
        await Db.SaveChangesAsync();

        Db.AddClosedSession(colleague.Id, Monday, TimeSpan.FromHours(9), TimeSpan.FromHours(17));
        await Db.SaveChangesAsync();

        var result = await NewService().CalculateAsync(user.Id, 2026, 3);

        Assert.Equal(0m, Day(result, Monday).WorkedHours);
    }

    // ── Automatic minimum-break deduction ─────────────────────────────────────

    [Fact]
    public async Task NoBreakLogged_DeductsTheResolvedMinimumBreak()
    {
        var user = await ArrangeMondayOnlyScheduleAsync(minimumBreakMinutes: 30);
        Db.AddClosedSession(user.Id, Monday, TimeSpan.FromHours(9), TimeSpan.FromHours(17));
        await Db.SaveChangesAsync();

        var day = Day(await NewService().CalculateAsync(user.Id, 2026, 3), Monday);

        Assert.Equal(7.5m, day.WorkedHours);
        Assert.Equal(30, day.BreakAutoDeductedMinutes);
    }

    [Fact]
    public async Task ABreakThatWasLogged_SuppressesTheAutomaticDeduction()
    {
        // Even a 5 minute logged break counts as "a break was recorded", so the full
        // 30 minute minimum must not be deducted on top of it.
        var user = await ArrangeMondayOnlyScheduleAsync(minimumBreakMinutes: 30);
        Db.AddClosedSession(user.Id, Monday, TimeSpan.FromHours(9), TimeSpan.FromHours(17),
            aBreak: (new TimeSpan(12, 0, 0), new TimeSpan(12, 5, 0)));
        await Db.SaveChangesAsync();

        var day = Day(await NewService().CalculateAsync(user.Id, 2026, 3), Monday);

        Assert.Equal(7.92m, day.WorkedHours); // 8h - 5min
        Assert.Null(day.BreakAutoDeductedMinutes);
    }

    [Fact]
    public async Task NoMinimumConfigured_DeductsNothing()
    {
        var user = await ArrangeMondayOnlyScheduleAsync();
        Db.AddClosedSession(user.Id, Monday, TimeSpan.FromHours(9), TimeSpan.FromHours(17));
        await Db.SaveChangesAsync();

        var day = Day(await NewService().CalculateAsync(user.Id, 2026, 3), Monday);

        Assert.Equal(8m, day.WorkedHours);
        Assert.Null(day.BreakAutoDeductedMinutes);
    }

    [Fact]
    public async Task PerEmployeeMinimumBreak_OverridesTheGlobalOne()
    {
        var user = Db.AddUser();
        Db.AddConfiguration(minimumBreakMinutes: 60);
        await Db.SaveChangesAsync();
        Db.EmployeeTargets.Add(new EmployeeTarget { UserId = user.Id, MinimumBreakMinutes = 15 });
        await Db.SaveChangesAsync();
        await Db.SetGlobalScheduleAsync((DayOfWeek.Monday, 8m));

        Db.AddClosedSession(user.Id, Monday, TimeSpan.FromHours(9), TimeSpan.FromHours(17));
        await Db.SaveChangesAsync();

        var day = Day(await NewService().CalculateAsync(user.Id, 2026, 3), Monday);

        Assert.Equal(15, day.BreakAutoDeductedMinutes);
        Assert.Equal(7.75m, day.WorkedHours);
    }

    // ── Session status ────────────────────────────────────────────────────────

    [Fact]
    public async Task ADayWithAnOpenSession_IsExcludedFromTheBalanceEntirely()
    {
        // Counting an in-progress day would show a phantom deficit all morning.
        var user = await ArrangeMondayOnlyScheduleAsync();
        Db.AddOpenSession(user.Id, Monday, TimeSpan.FromHours(9));
        await Db.SaveChangesAsync();

        var result = await NewService().CalculateAsync(user.Id, 2026, 3);
        var day = Day(result, Monday);

        Assert.Equal(0m, day.WorkedHours);
        Assert.Equal(8m, day.TargetHours); // the target is still reported for display
        Assert.Equal(0m, day.FlexDelta);
        // Only the four other Mondays count. Were the open day charged its target too,
        // the balance would be -40 and the employee would appear a day behind all morning.
        Assert.Equal(-32m, result.RunningBalanceHours);
    }

    [Fact]
    public async Task AnInvalidatedSession_ContributesNoHoursButStillOwesTheTarget()
    {
        var user = await ArrangeMondayOnlyScheduleAsync();
        var session = Db.AddClosedSession(user.Id, Monday, TimeSpan.FromHours(9), TimeSpan.FromHours(17));
        session.Status = WorkSessionStatus.Invalidated;
        await Db.SaveChangesAsync();

        var result = await NewService().CalculateAsync(user.Id, 2026, 3);

        Assert.Equal(0m, Day(result, Monday).WorkedHours);
        Assert.Equal(-8m, Day(result, Monday).FlexDelta);
    }

    // ── Effective target ──────────────────────────────────────────────────────

    [Fact]
    public async Task WeekendsCarryNoTarget_AndWeekendWorkIsAllSurplus()
    {
        var user = Db.AddUser();
        await Db.SaveChangesAsync();
        await Db.SetGlobalScheduleAsync((DayOfWeek.Monday, 8m));
        Db.AddClosedSession(user.Id, Saturday, TimeSpan.FromHours(10), TimeSpan.FromHours(14));
        await Db.SaveChangesAsync();

        var day = Day(await NewService().CalculateAsync(user.Id, 2026, 3), Saturday);

        Assert.Equal(0m, day.TargetHours);
        Assert.Equal(4m, day.FlexDelta);
    }

    [Fact]
    public async Task ANonWorkingPublicHoliday_ZeroesTheTarget()
    {
        var user = await ArrangeMondayOnlyScheduleAsync();
        Db.AddHoliday(Monday, "Easter Monday");
        await Db.SaveChangesAsync();

        var day = Day(await NewService().CalculateAsync(user.Id, 2026, 3), Monday);

        Assert.Equal(0m, day.TargetHours);
        Assert.Equal(0m, day.FlexDelta);
    }

    [Fact]
    public async Task AHolidayFlaggedAsAWorkingDay_KeepsItsTarget()
    {
        var user = await ArrangeMondayOnlyScheduleAsync();
        Db.AddHoliday(Monday, "Company works this one", isWorkingDay: true);
        await Db.SaveChangesAsync();

        var day = Day(await NewService().CalculateAsync(user.Id, 2026, 3), Monday);

        Assert.Equal(8m, day.TargetHours);
    }

    [Fact]
    public async Task AFullDayOfLeave_ZeroesTheTarget()
    {
        var user = await ArrangeMondayOnlyScheduleAsync();
        var type = Db.AddVacationType();
        Db.AddVacationDay(user.Id, type, Monday, 1.0m);
        await Db.SaveChangesAsync();

        var day = Day(await NewService().CalculateAsync(user.Id, 2026, 3), Monday);

        Assert.Equal(0m, day.TargetHours);
        Assert.Equal(0m, day.FlexDelta);
    }

    [Fact]
    public async Task AHalfDayOfLeave_HalvesTheTarget()
    {
        var user = await ArrangeMondayOnlyScheduleAsync();
        var type = Db.AddVacationType();
        Db.AddVacationDay(user.Id, type, Monday, 0.5m);
        await Db.SaveChangesAsync();
        // Working the remaining half exactly clears the day.
        Db.AddClosedSession(user.Id, Monday, TimeSpan.FromHours(9), TimeSpan.FromHours(13));
        await Db.SaveChangesAsync();

        var day = Day(await NewService().CalculateAsync(user.Id, 2026, 3), Monday);

        Assert.Equal(4m, day.TargetHours);
        Assert.Equal(0m, day.FlexDelta);
    }

    [Fact]
    public async Task LeaveTotallingMoreThanADay_IsCappedRatherThanGoingNegative()
    {
        // Two half days of different types plus a full day must not produce a negative target.
        var user = await ArrangeMondayOnlyScheduleAsync();
        var annual = Db.AddVacationType("Annual");
        var sick = Db.AddVacationType("Sick");
        Db.AddVacationDay(user.Id, annual, Monday, 1.0m);
        Db.AddVacationDay(user.Id, sick, Monday, 0.5m);
        await Db.SaveChangesAsync();

        var day = Day(await NewService().CalculateAsync(user.Id, 2026, 3), Monday);

        Assert.Equal(0m, day.TargetHours);
    }

    [Fact]
    public async Task APerEmployeeWorkdayTarget_BeatsTheGlobalOne()
    {
        var user = Db.AddUser();
        await Db.SaveChangesAsync();
        await Db.SetGlobalScheduleAsync((DayOfWeek.Monday, 8m));
        Db.AddWorkdayOverride(user.Id, DayOfWeek.Monday, 4m);
        await Db.SaveChangesAsync();

        Db.AddClosedSession(user.Id, Monday, TimeSpan.FromHours(9), TimeSpan.FromHours(13));
        await Db.SaveChangesAsync();

        var day = Day(await NewService().CalculateAsync(user.Id, 2026, 3), Monday);

        Assert.Equal(4m, day.TargetHours);
        Assert.Equal(0m, day.FlexDelta);
    }

    // ── Time bank adjustments ─────────────────────────────────────────────────

    [Fact]
    public async Task AdjustmentsDatedInTheMonth_MoveTheRunningBalance()
    {
        var user = await ArrangeMondayOnlyScheduleAsync();
        Db.AddClosedSession(user.Id, Monday, TimeSpan.FromHours(9), TimeSpan.FromHours(17));
        Db.AddAdjustment(user.Id, Monday, 2.5m, "carry-over");
        Db.AddAdjustment(user.Id, Tuesday, -0.5m, "correction");
        await Db.SaveChangesAsync();

        // Other Mondays in March are unworked: 2, 9, 16, 23, 30 -> 4 x -8 after the worked one.
        var result = await NewService().CalculateAsync(user.Id, 2026, 3);

        Assert.Equal(2.5m - 0.5m - 32m, result.RunningBalanceHours);
    }

    [Fact]
    public async Task AdjustmentsFromAnotherMonth_AreIgnored()
    {
        var user = await ArrangeMondayOnlyScheduleAsync();
        Db.AddClosedSession(user.Id, Monday, TimeSpan.FromHours(9), TimeSpan.FromHours(17));
        Db.AddAdjustment(user.Id, new DateOnly(2026, 2, 28), 10m);
        Db.AddAdjustment(user.Id, new DateOnly(2026, 4, 1), 10m);
        await Db.SaveChangesAsync();

        var result = await NewService().CalculateAsync(user.Id, 2026, 3);

        Assert.Equal(-32m, result.RunningBalanceHours);
    }

    // ── Compliance flags ──────────────────────────────────────────────────────

    [Fact]
    public async Task WorkingBeyondTargetPlusTheDailyAllowance_RaisesADailyFlag()
    {
        var user = Db.AddUser();
        Db.AddConfiguration(dailyAllowance: 1m);
        await Db.SaveChangesAsync();
        await Db.SetGlobalScheduleAsync((DayOfWeek.Monday, 8m));
        Db.AddClosedSession(user.Id, Monday, TimeSpan.FromHours(9), new TimeSpan(18, 30, 0)); // 9.5h vs 9h
        await Db.SaveChangesAsync();

        var result = await NewService().CalculateAsync(user.Id, 2026, 3);
        var flag = Assert.Single(result.ComplianceFlags, f => f.Type == ComplianceFlagType.DailyOvertime);

        Assert.Equal(Monday, flag.Date);
        Assert.Equal(9.5m, flag.HoursWorked);
        Assert.Equal(9m, flag.Threshold);
    }

    [Fact]
    public async Task WorkingExactlyToTheDailyThreshold_DoesNotFlag()
    {
        var user = Db.AddUser();
        Db.AddConfiguration(dailyAllowance: 1m);
        await Db.SaveChangesAsync();
        await Db.SetGlobalScheduleAsync((DayOfWeek.Monday, 8m));
        Db.AddClosedSession(user.Id, Monday, TimeSpan.FromHours(9), TimeSpan.FromHours(18)); // exactly 9h
        await Db.SaveChangesAsync();

        var result = await NewService().CalculateAsync(user.Id, 2026, 3);

        Assert.DoesNotContain(result.ComplianceFlags, f => f.Type == ComplianceFlagType.DailyOvertime);
    }

    [Fact]
    public async Task ADayWithNoTarget_NeverRaisesADailyFlag()
    {
        // Weekend and holiday work is surplus, not a working-time violation.
        var user = Db.AddUser();
        Db.AddConfiguration(dailyAllowance: 0m);
        await Db.SaveChangesAsync();
        await Db.SetGlobalScheduleAsync((DayOfWeek.Monday, 8m));
        Db.AddClosedSession(user.Id, Saturday, TimeSpan.FromHours(8), TimeSpan.FromHours(20));
        await Db.SaveChangesAsync();

        var result = await NewService().CalculateAsync(user.Id, 2026, 3);

        Assert.DoesNotContain(result.ComplianceFlags, f => f.Type == ComplianceFlagType.DailyOvertime);
    }

    [Fact]
    public async Task ExceedingTheWeeklyAllowance_RaisesAWeeklyFlagDatedOnTheIsoMonday()
    {
        var user = Db.AddUser();
        Db.AddConfiguration(dailyAllowance: 10m, weeklyAllowance: 1m);
        await Db.SaveChangesAsync();
        await Db.SetGlobalScheduleAsync((DayOfWeek.Monday, 8m), (DayOfWeek.Tuesday, 8m));

        // 16h target for the week, 18h worked, allowance 1h -> over the 17h threshold.
        Db.AddClosedSession(user.Id, Monday, TimeSpan.FromHours(8), TimeSpan.FromHours(17));   // 9h
        Db.AddClosedSession(user.Id, Tuesday, TimeSpan.FromHours(8), TimeSpan.FromHours(17));  // 9h
        await Db.SaveChangesAsync();

        var result = await NewService().CalculateAsync(user.Id, 2026, 3);
        var weekly = result.ComplianceFlags.Where(f => f.Type == ComplianceFlagType.WeeklyOvertime).ToList();
        var flag = Assert.Single(weekly);

        Assert.Equal(Monday, flag.Date); // the week's Monday, not the day of the breach
        Assert.Equal(18m, flag.HoursWorked);
        Assert.Equal(17m, flag.Threshold);
    }

    [Fact]
    public async Task WeeklyFlagsGroupByIsoWeek_NotByCalendarPosition()
    {
        var user = Db.AddUser();
        Db.AddConfiguration(dailyAllowance: 10m, weeklyAllowance: 0m);
        await Db.SaveChangesAsync();
        await Db.SetGlobalScheduleAsync((DayOfWeek.Monday, 8m));

        // Two different ISO weeks, each independently over its own 8h target.
        Db.AddClosedSession(user.Id, Monday, TimeSpan.FromHours(8), TimeSpan.FromHours(17));
        Db.AddClosedSession(user.Id, NextMonday, TimeSpan.FromHours(8), TimeSpan.FromHours(17));
        await Db.SaveChangesAsync();

        var result = await NewService().CalculateAsync(user.Id, 2026, 3);
        var weekly = result.ComplianceFlags
            .Where(f => f.Type == ComplianceFlagType.WeeklyOvertime)
            .OrderBy(f => f.Date)
            .ToList();

        Assert.Equal(2, weekly.Count);
        Assert.Equal(Monday, weekly[0].Date);
        Assert.Equal(NextMonday, weekly[1].Date);
    }

    // ── Shape of the result ───────────────────────────────────────────────────

    [Fact]
    public async Task APastMonth_ReportsEveryCalendarDay()
    {
        var user = await ArrangeMondayOnlyScheduleAsync();

        var result = await NewService().CalculateAsync(user.Id, 2026, 3);

        Assert.Equal(31, result.PerDay.Count);
        Assert.Equal(new DateOnly(2026, 3, 1), result.PerDay.First().Date);
        Assert.Equal(new DateOnly(2026, 3, 31), result.PerDay.Last().Date);
        Assert.Equal(2026, result.Year);
        Assert.Equal(3, result.Month);
    }

    [Fact]
    public async Task TheCurrentMonth_StopsAtTodayRatherThanProjectingForward()
    {
        // Future days must not be reported, or every month would open in deficit.
        var user = Db.AddUser();
        await Db.SaveChangesAsync();
        await Db.SetGlobalScheduleAsync((DayOfWeek.Monday, 8m));
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var result = await NewService().CalculateAsync(user.Id, today.Year, today.Month);

        Assert.Equal(today, result.PerDay.Last().Date);
        Assert.DoesNotContain(result.PerDay, d => d.Date > today);
    }

    [Fact]
    public async Task HoursAreRoundedToTwoDecimals()
    {
        var user = await ArrangeMondayOnlyScheduleAsync();
        // 20 minutes is 0.3333... hours and must not leak full precision into the payload.
        Db.AddClosedSession(user.Id, Monday, TimeSpan.FromHours(9), new TimeSpan(9, 20, 0));
        await Db.SaveChangesAsync();

        var day = Day(await NewService().CalculateAsync(user.Id, 2026, 3), Monday);

        Assert.Equal(0.33m, day.WorkedHours);
        Assert.Equal(-7.67m, day.FlexDelta);
    }
}
