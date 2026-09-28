using Microsoft.EntityFrameworkCore;
using TimeManagementBackend.Services;
using TimeManagementBackend.Tests.Infrastructure;

namespace TimeManagementBackend.Tests.Services;

/// <summary>
/// The calendar feed is reachable without a session — the URL's token is the only credential —
/// so it must be stored hashed, honour its expiry, and expose nothing when it does not match.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class CalendarServiceTests(PostgresFixture fixture) : DatabaseTestBase(fixture)
{
    private CalendarService NewService() => new(NewContext());

    /// <summary>
    /// Postgres stores timestamptz to the microsecond while a DateTimeOffset counts 100ns ticks,
    /// so a value that has been through the database is never bit-identical to the one handed
    /// back in memory. Comparing at storage precision is the meaningful assertion.
    /// </summary>
    private static void AssertSameInstant(DateTimeOffset? expected, DateTimeOffset? actual)
    {
        Assert.NotNull(expected);
        Assert.NotNull(actual);
        Assert.True((expected.Value - actual.Value).Duration() < TimeSpan.FromMilliseconds(1),
            $"Expected {expected:O} and {actual:O} to be the same instant.");
    }

    [Fact]
    public async Task TokenInfo_ReportsNoTokenBeforeOneIsIssued()
    {
        var user = Db.AddUser();
        await Db.SaveChangesAsync();

        var (hasToken, expiresAt) = await NewService().GetTokenInfoAsync(user.Id);

        Assert.False(hasToken);
        Assert.Null(expiresAt);
    }

    [Fact]
    public async Task Regenerate_ReturnsARawTokenButStoresOnlyItsHash()
    {
        var user = Db.AddUser();
        await Db.SaveChangesAsync();

        var (rawToken, expiresAt) = await NewService().RegenerateTokenAsync(user.Id);

        var saved = await NewContext().Users.SingleAsync(u => u.Id == user.Id);
        Assert.NotEmpty(rawToken);
        Assert.NotNull(saved.CalendarTokenHash);
        // A database leak must not hand out working feed URLs.
        Assert.NotEqual(rawToken, saved.CalendarTokenHash);
        Assert.DoesNotContain(rawToken, saved.CalendarTokenHash!);
        AssertSameInstant(expiresAt, saved.CalendarTokenExpiresAt);
        Assert.True(expiresAt > DateTimeOffset.UtcNow.AddDays(364));
    }

    [Fact]
    public async Task Regenerate_InvalidatesThePreviousToken()
    {
        var user = Db.AddUser();
        await Db.SaveChangesAsync();
        var (firstToken, _) = await NewService().RegenerateTokenAsync(user.Id);

        await NewService().RegenerateTokenAsync(user.Id);

        Assert.Null(await NewService().GetIcsContentAsync(firstToken));
    }

    [Fact]
    public async Task Regenerate_ClearsThePendingExpiryReminder()
    {
        var user = Db.AddUser();
        user.CalendarTokenExpiryNotifiedAt = DateTimeOffset.UtcNow.AddDays(-1);
        await Db.SaveChangesAsync();

        await NewService().RegenerateTokenAsync(user.Id);

        Assert.Null((await NewContext().Users.SingleAsync(u => u.Id == user.Id)).CalendarTokenExpiryNotifiedAt);
    }

    [Fact]
    public async Task TokenInfo_ReportsAnIssuedTokenAndItsExpiry()
    {
        var user = Db.AddUser();
        await Db.SaveChangesAsync();
        var (_, expiresAt) = await NewService().RegenerateTokenAsync(user.Id);

        var info = await NewService().GetTokenInfoAsync(user.Id);

        Assert.True(info.HasToken);
        AssertSameInstant(expiresAt, info.ExpiresAt);
    }

    [Fact]
    public async Task TokenInfo_TreatsAnExpiredTokenAsAbsent()
    {
        var user = Db.AddUser();
        await Db.SaveChangesAsync();
        await NewService().RegenerateTokenAsync(user.Id);

        var stored = await Db.Users.SingleAsync(u => u.Id == user.Id);
        stored.CalendarTokenExpiresAt = DateTimeOffset.UtcNow.AddDays(-1);
        await Db.SaveChangesAsync();

        var (hasToken, expiresAt) = await NewService().GetTokenInfoAsync(user.Id);

        Assert.False(hasToken);
        Assert.Null(expiresAt);
    }

    [Fact]
    public async Task Feed_ReturnsNothingForAnUnknownToken()
        => Assert.Null(await NewService().GetIcsContentAsync("not-a-real-token"));

    [Fact]
    public async Task Feed_ReturnsNothingOnceTheTokenHasExpired()
    {
        var user = Db.AddUser();
        await Db.SaveChangesAsync();
        var (rawToken, _) = await NewService().RegenerateTokenAsync(user.Id);

        var stored = await Db.Users.SingleAsync(u => u.Id == user.Id);
        stored.CalendarTokenExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        await Db.SaveChangesAsync();

        Assert.Null(await NewService().GetIcsContentAsync(rawToken));
    }

    [Fact]
    public async Task Feed_PublishesTheEmployeesLeaveAsCalendarEvents()
    {
        var user = Db.AddUser("Emma Employee");
        var type = Db.AddVacationType("Annual Leave");
        await Db.SaveChangesAsync();
        Db.AddVacationDay(user.Id, type, new DateOnly(2026, 7, 1));
        Db.AddVacationDay(user.Id, type, new DateOnly(2026, 7, 2));
        await Db.SaveChangesAsync();
        var (rawToken, _) = await NewService().RegenerateTokenAsync(user.Id);

        var ics = await NewService().GetIcsContentAsync(rawToken);

        Assert.NotNull(ics);
        Assert.Contains("BEGIN:VCALENDAR", ics);
        Assert.Contains("Emma Employee's Vacations", ics);
        Assert.Equal(2, ics.Split("BEGIN:VEVENT").Length - 1);
        Assert.Contains("SUMMARY:Annual Leave", ics);
    }

    [Fact]
    public async Task Feed_LabelsHalfDaysDistinctly()
    {
        var user = Db.AddUser();
        var type = Db.AddVacationType("Annual Leave");
        await Db.SaveChangesAsync();
        Db.AddVacationDay(user.Id, type, new DateOnly(2026, 7, 1), 0.5m);
        await Db.SaveChangesAsync();
        var (rawToken, _) = await NewService().RegenerateTokenAsync(user.Id);

        var ics = await NewService().GetIcsContentAsync(rawToken);

        Assert.Contains("day", ics!);
        Assert.Contains("Annual Leave", ics);
    }

    [Fact]
    public async Task Feed_NeverExposesAnotherEmployeesLeave()
    {
        var user = Db.AddUser("Emma Employee");
        var colleague = Db.AddUser("Colin Colleague");
        var type = Db.AddVacationType("Annual Leave");
        await Db.SaveChangesAsync();
        Db.AddVacationDay(colleague.Id, type, new DateOnly(2026, 7, 1));
        await Db.SaveChangesAsync();
        var (rawToken, _) = await NewService().RegenerateTokenAsync(user.Id);

        var ics = await NewService().GetIcsContentAsync(rawToken);

        Assert.NotNull(ics);
        Assert.Equal(0, ics.Split("BEGIN:VEVENT").Length - 1);
    }

    [Fact]
    public async Task Feed_RejectsAnUnknownUserOnRegenerate()
        => await Assert.ThrowsAsync<InvalidOperationException>(() =>
            NewService().RegenerateTokenAsync(Guid.NewGuid().ToString()));
}
