using Microsoft.EntityFrameworkCore;
using Npgsql;
using TimeManagementBackend.Models;

namespace TimeManagementBackend.Tests.Infrastructure;

[Collection(DatabaseCollection.Name)]
public class FixtureSmokeTests(PostgresFixture fixture) : DatabaseTestBase(fixture)
{
    [Fact]
    public async Task Database_HasMigrationsApplied_AndSeededWorkdayTargets()
    {
        var targets = await Db.WorkdayTargets.Where(t => t.UserId == null).ToListAsync();

        Assert.Equal(7, targets.Count);
        Assert.Equal(8m, targets.Single(t => t.DayOfWeek == DayOfWeek.Monday).Hours);
        Assert.Equal(0m, targets.Single(t => t.DayOfWeek == DayOfWeek.Sunday).Hours);
    }

    [Fact]
    public async Task EachTest_GetsAnIsolatedDatabase()
    {
        // If databases leaked between tests, the twin of this test would see two users.
        Db.AddUser();
        await Db.SaveChangesAsync();

        Assert.Equal(1, await NewContext().Users.CountAsync());
    }

    [Fact]
    public async Task EachTest_GetsAnIsolatedDatabase_Twin()
    {
        Db.AddUser();
        await Db.SaveChangesAsync();

        Assert.Equal(1, await NewContext().Users.CountAsync());
    }

    [Fact]
    public async Task OpenSessionIndex_IsEnforcedByPostgres()
    {
        // The one-open-session-per-user rule is a filtered unique index, not app code.
        // EF InMemory could never fail this test; Postgres can.
        var user = Db.AddUser();
        await Db.SaveChangesAsync();

        Db.AddOpenSession(user.Id, new DateOnly(2026, 3, 2), TimeSpan.FromHours(9));
        Db.AddOpenSession(user.Id, new DateOnly(2026, 3, 2), TimeSpan.FromHours(11));

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => Db.SaveChangesAsync());
        Assert.Equal("23505", Assert.IsType<PostgresException>(ex.InnerException).SqlState);
    }
}
