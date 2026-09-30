using Microsoft.EntityFrameworkCore;
using TimeManagementBackend.Helpers;
using TimeManagementBackend.Models.DTOs;
using TimeManagementBackend.Tests.Infrastructure;

namespace TimeManagementBackend.Tests.Helpers;

/// <summary>
/// The submitted schedule is authoritative: a weekday left out of it means "no override for that
/// day", which is how an employee is put back onto the global default.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class WorkdayTargetHelperTests(PostgresFixture fixture) : DatabaseTestBase(fixture)
{
    private static WorkdayTargetDto Target(DayOfWeek day, decimal hours) => new() { DayOfWeek = day, Hours = hours };

    [Fact]
    public async Task InsertsTheSubmittedDaysForAnEmployee()
    {
        var user = Db.AddUser();
        await Db.SaveChangesAsync();

        var rows = await WorkdayTargetHelper.UpsertWorkdayTargetsAsync(NewContext(), user.Id,
            [Target(DayOfWeek.Monday, 8m), Target(DayOfWeek.Friday, 4m)]);

        Assert.Equal(2, rows.Count);
        Assert.Equal(8m, rows.Single(r => r.DayOfWeek == DayOfWeek.Monday).Hours);
        Assert.Equal(4m, rows.Single(r => r.DayOfWeek == DayOfWeek.Friday).Hours);
    }

    [Fact]
    public async Task UpdatesADayThatIsSubmittedAgain()
    {
        var user = Db.AddUser();
        await Db.SaveChangesAsync();
        await WorkdayTargetHelper.UpsertWorkdayTargetsAsync(NewContext(), user.Id, [Target(DayOfWeek.Monday, 8m)]);

        var rows = await WorkdayTargetHelper.UpsertWorkdayTargetsAsync(
            NewContext(), user.Id, [Target(DayOfWeek.Monday, 6m)]);

        Assert.Equal(6m, Assert.Single(rows).Hours);
        Assert.Equal(1, await NewContext().WorkdayTargets.CountAsync(t => t.UserId == user.Id));
    }

    [Fact]
    public async Task RemovesADayThatIsOmittedFromTheSubmission()
    {
        var user = Db.AddUser();
        await Db.SaveChangesAsync();
        await WorkdayTargetHelper.UpsertWorkdayTargetsAsync(NewContext(), user.Id,
            [Target(DayOfWeek.Monday, 8m), Target(DayOfWeek.Friday, 4m)]);

        var rows = await WorkdayTargetHelper.UpsertWorkdayTargetsAsync(
            NewContext(), user.Id, [Target(DayOfWeek.Monday, 8m)]);

        Assert.Equal(DayOfWeek.Monday, Assert.Single(rows).DayOfWeek);
        // Friday now inherits the global default again rather than keeping a stale 4h override.
        Assert.False(await NewContext().WorkdayTargets
            .AnyAsync(t => t.UserId == user.Id && t.DayOfWeek == DayOfWeek.Friday));
    }

    [Fact]
    public async Task AnEmptySubmissionClearsEveryOverride()
    {
        var user = Db.AddUser();
        await Db.SaveChangesAsync();
        await WorkdayTargetHelper.UpsertWorkdayTargetsAsync(NewContext(), user.Id, [Target(DayOfWeek.Monday, 8m)]);

        var rows = await WorkdayTargetHelper.UpsertWorkdayTargetsAsync(NewContext(), user.Id, []);

        Assert.Empty(rows);
    }

    [Fact]
    public async Task ReturnsTheScheduleOrderedByWeekday()
    {
        var user = Db.AddUser();
        await Db.SaveChangesAsync();

        var rows = await WorkdayTargetHelper.UpsertWorkdayTargetsAsync(NewContext(), user.Id,
            [Target(DayOfWeek.Friday, 4m), Target(DayOfWeek.Monday, 8m), Target(DayOfWeek.Wednesday, 6m)]);

        Assert.Equal(
            [DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday],
            rows.Select(r => r.DayOfWeek));
    }

    [Fact]
    public async Task TheEmployeeScopeDoesNotDisturbTheGlobalSchedule()
    {
        var user = Db.AddUser();
        await Db.SaveChangesAsync();

        await WorkdayTargetHelper.UpsertWorkdayTargetsAsync(NewContext(), user.Id, [Target(DayOfWeek.Monday, 6m)]);

        // All seven seeded global rows must survive an employee-scoped write.
        Assert.Equal(7, await NewContext().WorkdayTargets.CountAsync(t => t.UserId == null));
    }

    [Fact]
    public async Task TheGlobalScopeDoesNotDisturbEmployeeOverrides()
    {
        var user = Db.AddUser();
        await Db.SaveChangesAsync();
        await WorkdayTargetHelper.UpsertWorkdayTargetsAsync(NewContext(), user.Id, [Target(DayOfWeek.Monday, 6m)]);

        await WorkdayTargetHelper.UpsertWorkdayTargetsAsync(NewContext(), null,
            [Target(DayOfWeek.Monday, 7m), Target(DayOfWeek.Tuesday, 7m)]);

        var context = NewContext();
        Assert.Equal(6m, (await context.WorkdayTargets.SingleAsync(t => t.UserId == user.Id)).Hours);
        Assert.Equal(2, await context.WorkdayTargets.CountAsync(t => t.UserId == null));
    }
}
