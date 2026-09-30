using TimeManagementBackend.Helpers;
using TimeManagementBackend.Models;
using TimeManagementBackend.Tests.Infrastructure;

namespace TimeManagementBackend.Tests.Helpers;

/// <summary>
/// One predicate guards every write into a closed month, so each way it could wrongly return
/// false is a way for a settled balance to change after the fact.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class SettlementLockHelperTests(PostgresFixture fixture) : DatabaseTestBase(fixture)
{
    private static readonly DateOnly InMarch = new(2026, 3, 15);

    [Fact]
    public async Task AMonthWithNoSettlementIsOpen()
    {
        var user = Db.AddUser();
        await Db.SaveChangesAsync();

        Assert.False(await SettlementLockHelper.IsMonthSettledAsync(NewContext(), user.Id, InMarch));
    }

    [Fact]
    public async Task ASettledMonthIsLockedOnEveryDayOfThatMonth()
    {
        var user = Db.AddUser();
        await Db.SaveChangesAsync();
        Db.AddSettlement(user.Id, 2026, 3, SettlementStatus.Settled);
        await Db.SaveChangesAsync();

        var context = NewContext();
        Assert.True(await SettlementLockHelper.IsMonthSettledAsync(context, user.Id, new DateOnly(2026, 3, 1)));
        Assert.True(await SettlementLockHelper.IsMonthSettledAsync(context, user.Id, InMarch));
        Assert.True(await SettlementLockHelper.IsMonthSettledAsync(context, user.Id, new DateOnly(2026, 3, 31)));
    }

    [Fact]
    public async Task AMonthAwaitingReviewIsStillOpen()
    {
        // Only a confirmed settlement locks the month; a generated one is still being reviewed.
        var user = Db.AddUser();
        await Db.SaveChangesAsync();
        Db.AddSettlement(user.Id, 2026, 3, SettlementStatus.PendingReview);
        await Db.SaveChangesAsync();

        Assert.False(await SettlementLockHelper.IsMonthSettledAsync(NewContext(), user.Id, InMarch));
    }

    [Fact]
    public async Task NeighbouringMonthsStayOpen()
    {
        var user = Db.AddUser();
        await Db.SaveChangesAsync();
        Db.AddSettlement(user.Id, 2026, 3, SettlementStatus.Settled);
        await Db.SaveChangesAsync();

        var context = NewContext();
        Assert.False(await SettlementLockHelper.IsMonthSettledAsync(context, user.Id, new DateOnly(2026, 2, 28)));
        Assert.False(await SettlementLockHelper.IsMonthSettledAsync(context, user.Id, new DateOnly(2026, 4, 1)));
    }

    [Fact]
    public async Task TheSameMonthInAnotherYearStaysOpen()
    {
        var user = Db.AddUser();
        await Db.SaveChangesAsync();
        Db.AddSettlement(user.Id, 2026, 3, SettlementStatus.Settled);
        await Db.SaveChangesAsync();

        Assert.False(await SettlementLockHelper.IsMonthSettledAsync(NewContext(), user.Id, new DateOnly(2025, 3, 15)));
    }

    [Fact]
    public async Task OneEmployeesSettlementDoesNotLockAnother()
    {
        var user = Db.AddUser();
        var colleague = Db.AddUser("Colin Colleague");
        await Db.SaveChangesAsync();
        Db.AddSettlement(user.Id, 2026, 3, SettlementStatus.Settled);
        await Db.SaveChangesAsync();

        Assert.False(await SettlementLockHelper.IsMonthSettledAsync(NewContext(), colleague.Id, InMarch));
    }
}
