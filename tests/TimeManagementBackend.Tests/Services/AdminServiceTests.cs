using Microsoft.EntityFrameworkCore;
using TimeManagementBackend.Exceptions;
using TimeManagementBackend.Models;
using TimeManagementBackend.Models.DTOs;
using TimeManagementBackend.Services;
using TimeManagementBackend.Tests.Infrastructure;

namespace TimeManagementBackend.Tests.Services;

/// <summary>
/// Admin-side reads and the payroll export. The CSV is handed to whoever runs payroll, so its
/// shape, its numbers and its handling of untrusted text are all load-bearing.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class AdminServiceTests(PostgresFixture fixture) : DatabaseTestBase(fixture)
{
    private static readonly DateOnly Monday = new(2026, 3, 2);
    private static readonly DateOnly Tuesday = new(2026, 3, 3);

    private AdminService NewService()
    {
        var context = NewContext();
        return new AdminService(context, CreateUserManager(context), Mapper);
    }

    /// <summary>A single employee whose only working day is Monday, which keeps the payroll
    /// export down to the five Mondays in March 2026 instead of 22 weekday rows.</summary>
    private async Task<User> ArrangeMondayOnlyEmployeeAsync(string name = "Emma Employee")
    {
        var user = Db.AddUser(name);
        await Db.SaveChangesAsync();
        await Db.SetGlobalScheduleAsync((DayOfWeek.Monday, 8m));
        return user;
    }

    // ── Day summaries ─────────────────────────────────────────────────────────

    [Fact]
    public async Task DaySummaries_GroupPerEmployeePerDayAndNetOffBreaks()
    {
        var user = await ArrangeMondayOnlyEmployeeAsync();
        Db.AddClosedSession(user.Id, Monday, TimeSpan.FromHours(9), new TimeSpan(17, 30, 0),
            aBreak: (new TimeSpan(12, 0, 0), new TimeSpan(12, 30, 0)));
        Db.AddClosedSession(user.Id, Monday, TimeSpan.FromHours(18), TimeSpan.FromHours(19));
        await Db.SaveChangesAsync();

        var summary = Assert.Single(await NewService().GetAllDaySummariesAsync());

        Assert.Equal(user.Id, summary.UserId);
        Assert.Equal("Emma Employee", summary.EmployeeName);
        Assert.Equal(9.0, summary.TotalHours, 3); // 8h net + 1h
        Assert.Equal(2, summary.Sessions.Count);
    }

    [Fact]
    public async Task DaySummaries_FlagUnresolvedSessions()
    {
        var user = await ArrangeMondayOnlyEmployeeAsync();
        Db.AddOpenSession(user.Id, Monday, TimeSpan.FromHours(9));
        var invalid = Db.AddClosedSession(user.Id, Tuesday, TimeSpan.FromHours(9), TimeSpan.FromHours(17));
        invalid.Status = WorkSessionStatus.Invalidated;
        await Db.SaveChangesAsync();

        var summaries = (await NewService().GetAllDaySummariesAsync()).ToList();

        Assert.True(summaries.Single(s => s.Date == Monday).HasOpenSession);
        Assert.True(summaries.Single(s => s.Date == Tuesday).HasInvalidatedSession);
        // An unfinished session contributes no hours.
        Assert.Equal(0.0, summaries.Single(s => s.Date == Monday).TotalHours);
        Assert.Equal(0.0, summaries.Single(s => s.Date == Tuesday).TotalHours);
    }

    [Fact]
    public async Task DaySummaries_CanBeFilteredByEmployeeAndDateRange()
    {
        var user = await ArrangeMondayOnlyEmployeeAsync();
        var colleague = Db.AddUser("Colin Colleague");
        await Db.SaveChangesAsync();
        Db.AddClosedSession(user.Id, Monday, TimeSpan.FromHours(9), TimeSpan.FromHours(17));
        Db.AddClosedSession(user.Id, new DateOnly(2026, 4, 6), TimeSpan.FromHours(9), TimeSpan.FromHours(17));
        Db.AddClosedSession(colleague.Id, Monday, TimeSpan.FromHours(9), TimeSpan.FromHours(17));
        await Db.SaveChangesAsync();

        var result = await NewService().GetAllDaySummariesAsync(
            user.Id, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));

        var summary = Assert.Single(result);
        Assert.Equal(user.Id, summary.UserId);
        Assert.Equal(Monday, summary.Date);
    }

    [Fact]
    public async Task DaySummaries_CarryTheDaysHomeWorkingFlagAndDescription()
    {
        var user = await ArrangeMondayOnlyEmployeeAsync();
        Db.AddClosedSession(user.Id, Monday, TimeSpan.FromHours(9), TimeSpan.FromHours(17));
        Db.WorkDays.Add(new WorkDay
        {
            UserId = user.Id, Date = Monday, WorkedFromHome = true, Description = "Remote day",
        });
        await Db.SaveChangesAsync();

        var summary = Assert.Single(await NewService().GetAllDaySummariesAsync());

        Assert.True(summary.WorkedFromHome);
        Assert.Equal("Remote day", summary.Description);
    }

    // ── Employees ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetEmployees_ResolvesTheWeeklyTargetFromTheWorkdaySchedule()
    {
        var user = await ArrangeMondayOnlyEmployeeAsync();
        Db.AddWorkdayOverride(user.Id, DayOfWeek.Tuesday, 4m);
        await Db.SaveChangesAsync();

        var employee = Assert.Single(await NewService().GetEmployeesAsync(UserRole.Employee));

        // Monday 8h globally plus this employee's 4h Tuesday override.
        Assert.Equal(12m, employee.ResolvedWeeklyTarget);
    }

    [Fact]
    public async Task GetEmployees_CanBeFilteredByRole()
    {
        await ArrangeMondayOnlyEmployeeAsync();
        Db.AddUser("Adam Admin", UserRole.Admin);
        await Db.SaveChangesAsync();

        Assert.Single(await NewService().GetEmployeesAsync(UserRole.Employee));
        Assert.Single(await NewService().GetEmployeesAsync(UserRole.Admin));
        Assert.Equal(2, (await NewService().GetEmployeesAsync()).Count());
    }

    [Fact]
    public async Task GetEmployees_ReportsHoursLoggedInTheCurrentWeekOnly()
    {
        var user = await ArrangeMondayOnlyEmployeeAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        Db.AddClosedSession(user.Id, today, TimeSpan.FromHours(9), TimeSpan.FromHours(14));
        Db.AddClosedSession(user.Id, today.AddDays(-30), TimeSpan.FromHours(9), TimeSpan.FromHours(17));
        await Db.SaveChangesAsync();

        var employee = Assert.Single(await NewService().GetEmployeesAsync(UserRole.Employee));

        Assert.Equal(5m, employee.WeeklyHoursLogged);
    }

    [Fact]
    public async Task DisableAndEnable_ToggleTheAccount()
    {
        var user = await ArrangeMondayOnlyEmployeeAsync();

        await NewService().DisableEmployeeAsync(user.Id);
        Assert.True((await NewContext().Users.SingleAsync(u => u.Id == user.Id)).IsDisabled);

        await NewService().EnableEmployeeAsync(user.Id);
        Assert.False((await NewContext().Users.SingleAsync(u => u.Id == user.Id)).IsDisabled);
    }

    [Fact]
    public async Task Disable_RejectsAnUnknownUser()
        => await Assert.ThrowsAsync<ResourceNotFoundException>(() =>
            NewService().DisableEmployeeAsync(Guid.NewGuid().ToString()));

    [Fact]
    public async Task Delete_RefusesAnEmployeeWhoIsStillActive()
    {
        // Disabling first is the deliberate speed bump in front of an irreversible delete.
        var user = await ArrangeMondayOnlyEmployeeAsync();

        var ex = await Assert.ThrowsAsync<ValidationException>(() => NewService().DeleteEmployeeAsync(user.Id));

        Assert.Contains("must be disabled", ex.Message);
        Assert.NotNull(await NewContext().Users.FirstOrDefaultAsync(u => u.Id == user.Id));
    }

    [Fact]
    public async Task Delete_RemovesTheEmployeeAndEverythingHangingOffThem()
    {
        var user = Db.AddUser(isDisabled: true);
        var type = Db.AddVacationType();
        await Db.SaveChangesAsync();

        Db.AddClosedSession(user.Id, Monday, TimeSpan.FromHours(9), TimeSpan.FromHours(17),
            aBreak: (new TimeSpan(12, 0, 0), new TimeSpan(12, 30, 0)));
        Db.WorkDays.Add(new WorkDay { UserId = user.Id, Date = Monday });
        Db.AddBalance(user.Id, type, 20m);
        Db.AddVacationDay(user.Id, type, Monday);
        Db.AddAdjustment(user.Id, Monday, 1m);
        Db.AddSettlement(user.Id, 2026, 3);
        Db.AddWorkdayOverride(user.Id, DayOfWeek.Monday, 4m);
        Db.EmployeeTargets.Add(new EmployeeTarget { UserId = user.Id, MinimumBreakMinutes = 15 });
        Db.Notifications.Add(new Notification { RecipientUserId = user.Id, Message = "hi" });
        await Db.SaveChangesAsync();

        await NewService().DeleteEmployeeAsync(user.Id);

        var context = NewContext();
        Assert.Null(await context.Users.FirstOrDefaultAsync(u => u.Id == user.Id));
        Assert.Empty(await context.WorkSessions.ToListAsync());
        Assert.Empty(await context.BreakRecords.ToListAsync());
        Assert.Empty(await context.WorkDays.ToListAsync());
        Assert.Empty(await context.VacationDays.ToListAsync());
        Assert.Empty(await context.EmployeeVacationBalances.ToListAsync());
        Assert.Empty(await context.TimeBankAdjustments.ToListAsync());
        Assert.Empty(await context.MonthlySettlements.ToListAsync());
        Assert.Empty(await context.EmployeeTargets.ToListAsync());
        Assert.Empty(await context.Notifications.ToListAsync());
        Assert.Empty(await context.WorkdayTargets.Where(t => t.UserId != null).ToListAsync());
        // The shared global schedule must survive deleting an employee.
        Assert.Equal(7, await context.WorkdayTargets.CountAsync(t => t.UserId == null));
    }

    // ── Vacation types ────────────────────────────────────────────────────────

    [Fact]
    public async Task VacationTypes_AreSoftDeletedAndDisappearFromReads()
    {
        var service = NewService();
        var created = await service.CreateVacationTypeAsync(new VacationTypeFormDto
        {
            Name = "Study Leave", Color = "#00FF00", Description = "Courses",
        });

        await service.DeleteVacationTypeAsync(created.Id);

        Assert.Empty(await NewService().GetVacationTypesAsync());
        // The row is retained so historical bookings still resolve their type.
        var row = await NewContext().VacationTypes.IgnoreQueryFilters().SingleAsync();
        Assert.True(row.IsDeleted);
        Assert.NotNull(row.DeletedAt);
    }

    [Fact]
    public async Task UpdateVacationType_ReportsTheRealAssignedEmployeeCount()
    {
        // The count comes from a separate query because the navigation isn't loaded here.
        var user = Db.AddUser();
        var other = Db.AddUser("Olive Other");
        var type = Db.AddVacationType();
        await Db.SaveChangesAsync();
        Db.AddBalance(user.Id, type, 20m);
        Db.AddBalance(other.Id, type, 20m);
        await Db.SaveChangesAsync();

        var updated = await NewService().UpdateVacationTypeAsync(type.Id, new VacationTypeFormDto
        {
            Name = "Renamed Leave", Color = "#123456",
        });

        Assert.Equal("Renamed Leave", updated.Name);
        Assert.Equal(2, updated.AssignedEmployeeCount);
    }

    [Fact]
    public async Task UpdateVacationType_RejectsAnUnknownId()
        => await Assert.ThrowsAsync<ResourceNotFoundException>(() =>
            NewService().UpdateVacationTypeAsync(999, new VacationTypeFormDto { Name = "x" }));

    [Fact]
    public async Task EmployeeBalances_CanBeAssignedUpdatedAndRemoved()
    {
        var user = Db.AddUser();
        var type = Db.AddVacationType();
        await Db.SaveChangesAsync();
        var service = NewService();

        var assigned = await service.AssignVacationTypeAsync(user.Id,
            new AssignVacationTypeDto { VacationTypeId = type.Id, YearlyBalance = 20m });
        Assert.Equal("Annual Leave", assigned.VacationTypeName);

        var updated = await service.UpdateEmployeeBalanceAsync(assigned.Id,
            new UpdateVacationBalanceDto { YearlyBalance = 25m });
        Assert.Equal(25m, updated.YearlyBalance);

        await service.RemoveEmployeeVacationTypeAsync(assigned.Id);
        Assert.Empty(await NewService().GetEmployeeBalancesAsync(user.Id));
    }

    // ── Employee targets ──────────────────────────────────────────────────────

    [Fact]
    public async Task EmployeeTarget_ReportsWhetherItOverridesTheGlobalDefault()
    {
        var user = Db.AddUser();
        Db.AddConfiguration(minimumBreakMinutes: 30);
        await Db.SaveChangesAsync();

        var inherited = await NewService().GetEmployeeTargetAsync(user.Id);
        Assert.False(inherited.HasOverride);
        Assert.Null(inherited.MinimumBreakMinutes);
        Assert.Equal(30, inherited.ResolvedMinimumBreakMinutes);

        var overridden = await NewService().SetEmployeeTargetAsync(user.Id,
            new SetEmployeeTargetDto { MinimumBreakMinutes = 15 });
        Assert.True(overridden.HasOverride);
        Assert.Equal(15, overridden.ResolvedMinimumBreakMinutes);
    }

    [Fact]
    public async Task EmployeeTarget_CanFallBackToTheGlobalDefaultAgain()
    {
        var user = Db.AddUser();
        Db.AddConfiguration(minimumBreakMinutes: 30);
        await Db.SaveChangesAsync();
        await NewService().SetEmployeeTargetAsync(user.Id, new SetEmployeeTargetDto { MinimumBreakMinutes = 15 });

        var cleared = await NewService().SetEmployeeTargetAsync(user.Id, new SetEmployeeTargetDto());

        Assert.False(cleared.HasOverride);
        Assert.Equal(30, cleared.ResolvedMinimumBreakMinutes);
    }

    [Fact]
    public async Task WeeklySummary_ReturnsTheRequestedNumberOfWeeksOldestFirst()
    {
        var user = await ArrangeMondayOnlyEmployeeAsync();

        var weeks = (await NewService().GetEmployeeWeeklySummaryAsync(user.Id, 4)).ToList();

        Assert.Equal(4, weeks.Count);
        Assert.Equal(8m, weeks[0].Target); // one 8h Monday per week
        var starts = weeks.Select(w => DateOnly.Parse(w.WeekStart)).ToList();
        Assert.Equal(starts.OrderBy(d => d), starts);
        Assert.All(starts, d => Assert.Equal(DayOfWeek.Monday, d.DayOfWeek));
    }

    // ── Time bank adjustments ─────────────────────────────────────────────────

    [Fact]
    public async Task CreateAdjustment_RecordsTheAuthoringAdmin()
    {
        var user = Db.AddUser();
        var admin = Db.AddUser("Adam Admin", UserRole.Admin);
        await Db.SaveChangesAsync();

        var dto = await NewService().CreateTimeBankAdjustmentAsync(user.Id, new CreateTimeBankAdjustmentDto
        {
            EffectiveDate = Monday, Hours = -2.5m, Reason = "Corrected double entry",
        }, admin.Id);

        Assert.Equal(-2.5m, dto.Hours);
        Assert.Equal("Adam Admin", dto.CreatedByName);
        Assert.Equal(admin.Id, (await NewContext().TimeBankAdjustments.SingleAsync()).CreatedByUserId);
    }

    [Fact]
    public async Task CreateAdjustment_RejectsAnUnknownEmployee()
    {
        var admin = Db.AddUser("Adam Admin", UserRole.Admin);
        await Db.SaveChangesAsync();

        await Assert.ThrowsAsync<ResourceNotFoundException>(() =>
            NewService().CreateTimeBankAdjustmentAsync(Guid.NewGuid().ToString(),
                new CreateTimeBankAdjustmentDto { EffectiveDate = Monday, Hours = 1m, Reason = "r" }, admin.Id));
    }

    [Fact]
    public async Task CreateAdjustment_IsBlockedInASettledMonth()
    {
        var user = Db.AddUser();
        var admin = Db.AddUser("Adam Admin", UserRole.Admin);
        await Db.SaveChangesAsync();
        Db.AddSettlement(user.Id, 2026, 3, SettlementStatus.Settled);
        await Db.SaveChangesAsync();

        await Assert.ThrowsAsync<ValidationException>(() =>
            NewService().CreateTimeBankAdjustmentAsync(user.Id,
                new CreateTimeBankAdjustmentDto { EffectiveDate = Monday, Hours = 1m, Reason = "r" }, admin.Id));
    }

    [Fact]
    public async Task DeleteAdjustment_RefusesOneCreatedByASettlement()
    {
        // Carry-forward rows are owned by their settlement; deleting one by hand would
        // desynchronise the next month's opening balance from the settled month.
        var user = Db.AddUser();
        await Db.SaveChangesAsync();
        var settlement = Db.AddSettlement(user.Id, 2026, 2, SettlementStatus.Settled);
        await Db.SaveChangesAsync();
        var adjustment = Db.AddAdjustment(user.Id, Monday, 3m, "Carry-over");
        adjustment.SourceSettlementId = settlement.Id;
        await Db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            NewService().DeleteTimeBankAdjustmentAsync(adjustment.Id));

        Assert.Contains("created automatically", ex.Message);
        Assert.Equal(1, await NewContext().TimeBankAdjustments.CountAsync());
    }

    [Fact]
    public async Task DeleteAdjustment_RemovesAManualOne()
    {
        var user = Db.AddUser();
        await Db.SaveChangesAsync();
        var adjustment = Db.AddAdjustment(user.Id, Monday, 3m);
        await Db.SaveChangesAsync();

        await NewService().DeleteTimeBankAdjustmentAsync(adjustment.Id);

        Assert.Empty(await NewContext().TimeBankAdjustments.ToListAsync());
    }

    [Fact]
    public async Task GetAdjustments_CanBeScopedToAMonthOrAYear()
    {
        var user = Db.AddUser();
        await Db.SaveChangesAsync();
        Db.AddAdjustment(user.Id, new DateOnly(2026, 3, 5), 1m);
        Db.AddAdjustment(user.Id, new DateOnly(2026, 4, 5), 2m);
        Db.AddAdjustment(user.Id, new DateOnly(2025, 3, 5), 3m);
        await Db.SaveChangesAsync();

        Assert.Single(await NewService().GetTimeBankAdjustmentsAsync(user.Id, 2026, 3));
        Assert.Equal(2, (await NewService().GetTimeBankAdjustmentsAsync(user.Id, 2026, null)).Count());
        Assert.Equal(3, (await NewService().GetTimeBankAdjustmentsAsync(user.Id, null, null)).Count());
    }

    // ── Payroll export ────────────────────────────────────────────────────────

    private static List<string> Lines(string csv) =>
        csv.Split('\n').Select(l => l.TrimEnd('\r')).ToList();

    [Fact]
    public async Task Payroll_StartsWithTheOvertimeSummaryThenTheDailyRows()
    {
        var user = await ArrangeMondayOnlyEmployeeAsync();
        Db.AddClosedSession(user.Id, Monday, TimeSpan.FromHours(9), TimeSpan.FromHours(17));
        await Db.SaveChangesAsync();

        var lines = Lines(await NewService().GeneratePayrollCsvAsync(2026, 3));

        Assert.Equal("OVERTIME SUMMARY", lines[0]);
        Assert.Equal("Employee,Approved Overtime Hours,Outcome,Notes", lines[1]);
        Assert.Contains("Date,Day,Employee,Hours Worked,Vacation Type,Description", lines);
    }

    [Fact]
    public async Task Payroll_CarriesTheSettlementsPaidOutHoursOutcomeAndNotes()
    {
        // The settlement screen tells admins their notes reach payroll — this is that promise.
        var user = await ArrangeMondayOnlyEmployeeAsync();
        var settlement = Db.AddSettlement(user.Id, 2026, 3, SettlementStatus.Settled, netBalanceHours: 6m);
        settlement.PaidOutHours = 6m;
        settlement.Outcome = SettlementOutcome.Paid;
        settlement.Notes = "Approved by finance";
        await Db.SaveChangesAsync();

        var summaryRow = Lines(await NewService().GeneratePayrollCsvAsync(2026, 3))[2];

        Assert.Equal("Emma Employee,6.00,Paid,Approved by finance", summaryRow);
    }

    [Fact]
    public async Task Payroll_LeavesTheSummaryBlankWhileAMonthIsStillPendingReview()
    {
        var user = await ArrangeMondayOnlyEmployeeAsync();
        Db.AddSettlement(user.Id, 2026, 3, SettlementStatus.PendingReview, netBalanceHours: 6m);
        await Db.SaveChangesAsync();

        var summaryRow = Lines(await NewService().GeneratePayrollCsvAsync(2026, 3))[2];

        Assert.Equal("Emma Employee,,,", summaryRow);
    }

    [Fact]
    public async Task Payroll_ReportsWorkedHoursToTwoDecimals()
    {
        var user = await ArrangeMondayOnlyEmployeeAsync();
        Db.AddClosedSession(user.Id, Monday, TimeSpan.FromHours(9), new TimeSpan(16, 45, 0));
        await Db.SaveChangesAsync();

        var row = Lines(await NewService().GeneratePayrollCsvAsync(2026, 3))
            .Single(l => l.StartsWith("2026-03-02,"));

        Assert.Equal("2026-03-02,Monday,Emma Employee,7.75,,", row);
    }

    [Fact]
    public async Task Payroll_MarksAWorkingDayWithNoHoursAsMissingLog()
    {
        var user = await ArrangeMondayOnlyEmployeeAsync();

        var row = Lines(await NewService().GeneratePayrollCsvAsync(2026, 3))
            .Single(l => l.StartsWith("2026-03-02,"));

        Assert.Contains("Missing Log", row);
    }

    [Fact]
    public async Task Payroll_NamesTheHolidayAndTheLeaveType()
    {
        var user = await ArrangeMondayOnlyEmployeeAsync();
        var type = Db.AddVacationType("Annual Leave");
        Db.AddHoliday(Monday, "Easter Monday");
        await Db.SaveChangesAsync();
        Db.AddVacationDay(user.Id, type, new DateOnly(2026, 3, 9), 1.0m, "Family trip");
        await Db.SaveChangesAsync();

        var lines = Lines(await NewService().GeneratePayrollCsvAsync(2026, 3));

        Assert.Contains("Holiday: Easter Monday", lines.Single(l => l.StartsWith("2026-03-02,")));
        var leaveRow = lines.Single(l => l.StartsWith("2026-03-09,"));
        Assert.Contains("Annual Leave", leaveRow);
        Assert.Contains("Family trip", leaveRow); // the leave note wins over the day description
    }

    [Fact]
    public async Task Payroll_OmitsNonWorkingDaysWithNothingToReport()
    {
        var user = await ArrangeMondayOnlyEmployeeAsync();

        var lines = Lines(await NewService().GeneratePayrollCsvAsync(2026, 3));

        // Only the five Mondays of March 2026 qualify; Tuesday has no target and no activity.
        Assert.DoesNotContain(lines, l => l.StartsWith("2026-03-03,"));
        Assert.Equal(5, lines.Count(l => l.Contains(",Monday,")));
    }

    [Fact]
    public async Task Payroll_IncludesAWeekendDayThatWasActuallyWorked()
    {
        var user = await ArrangeMondayOnlyEmployeeAsync();
        Db.AddClosedSession(user.Id, new DateOnly(2026, 3, 7), TimeSpan.FromHours(10), TimeSpan.FromHours(14));
        await Db.SaveChangesAsync();

        var row = Lines(await NewService().GeneratePayrollCsvAsync(2026, 3))
            .Single(l => l.StartsWith("2026-03-07,"));

        Assert.Equal("2026-03-07,Saturday,Emma Employee,4.00,,", row);
    }

    [Fact]
    public async Task Payroll_CanBeScopedToOneEmployee()
    {
        var user = await ArrangeMondayOnlyEmployeeAsync();
        var colleague = Db.AddUser("Colin Colleague");
        await Db.SaveChangesAsync();
        Db.AddClosedSession(colleague.Id, Monday, TimeSpan.FromHours(9), TimeSpan.FromHours(17));
        await Db.SaveChangesAsync();

        var csv = await NewService().GeneratePayrollCsvAsync(2026, 3, user.Id);

        Assert.DoesNotContain("Colin Colleague", csv);
        Assert.Contains("Emma Employee", csv);
    }

    [Fact]
    public async Task Payroll_QuotesFieldsContainingCommasAndQuotes()
    {
        var user = await ArrangeMondayOnlyEmployeeAsync("Doe, Jane \"JD\"");
        Db.AddClosedSession(user.Id, Monday, TimeSpan.FromHours(9), TimeSpan.FromHours(17));
        await Db.SaveChangesAsync();

        var row = Lines(await NewService().GeneratePayrollCsvAsync(2026, 3))
            .Single(l => l.StartsWith("2026-03-02,"));

        // RFC 4180: wrap in quotes and double any embedded quote.
        Assert.Contains("\"Doe, Jane \"\"JD\"\"\"", row);
    }

    [Fact]
    public async Task Payroll_NeutralisesSpreadsheetFormulaInjection()
    {
        // A note starting with = + - or @ is executed as a formula by Excel and Sheets, so the
        // export prefixes an apostrophe to keep an employee's free text from becoming code.
        var user = await ArrangeMondayOnlyEmployeeAsync();
        var type = Db.AddVacationType("Annual Leave");
        await Db.SaveChangesAsync();
        Db.AddVacationDay(user.Id, type, Monday, 1.0m, "=HYPERLINK(\"http://evil.test\")");
        await Db.SaveChangesAsync();

        var row = Lines(await NewService().GeneratePayrollCsvAsync(2026, 3))
            .Single(l => l.StartsWith("2026-03-02,"));

        Assert.Contains("'=HYPERLINK", row);
        Assert.DoesNotContain(",=HYPERLINK", row);
    }

    [Theory]
    [InlineData("+1234")]
    [InlineData("-cmd")]
    [InlineData("@SUM(A1)")]
    public async Task Payroll_NeutralisesEveryFormulaPrefix(string note)
    {
        var user = await ArrangeMondayOnlyEmployeeAsync();
        var type = Db.AddVacationType("Annual Leave");
        await Db.SaveChangesAsync();
        Db.AddVacationDay(user.Id, type, Monday, 1.0m, note);
        await Db.SaveChangesAsync();

        var row = Lines(await NewService().GeneratePayrollCsvAsync(2026, 3))
            .Single(l => l.StartsWith("2026-03-02,"));

        Assert.Contains($"'{note}", row);
    }
}
