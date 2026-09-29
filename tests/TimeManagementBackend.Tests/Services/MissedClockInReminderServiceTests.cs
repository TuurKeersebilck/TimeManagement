using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TimeManagementBackend.Data;
using TimeManagementBackend.Models;
using TimeManagementBackend.Services;
using TimeManagementBackend.Tests.Infrastructure;

namespace TimeManagementBackend.Tests.Services;

/// <summary>
/// The nightly background job. It auto-closes runaway sessions, chases missed clock-ins, warns
/// about expiring calendar tokens and generates month-end settlements.
///
/// Its steps are private and read <c>DateTime.UtcNow</c> directly, so there is no seam to drive
/// them through. These tests invoke the steps by reflection, which is the honest trade-off:
/// without it this job — the one piece of code that mutates data with nobody watching — would
/// have no coverage at all. Injecting a <c>TimeProvider</c> and making the steps internal would
/// let these become ordinary tests, and would also make the 08:00-and-once-a-day scheduling
/// itself testable, which it currently is not.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class MissedClockInReminderServiceTests(PostgresFixture fixture) : DatabaseTestBase(fixture)
{
    private readonly INotificationService _notifications = Substitute.For<INotificationService>();
    private readonly IEmailService _email = Substitute.For<IEmailService>();
    private readonly ISettlementService _settlements = Substitute.For<ISettlementService>();

    private ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(ConnectionString));
        services.AddScoped(_ => _notifications);
        services.AddScoped(_ => _email);
        services.AddScoped(_ => _settlements);
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["AppUrl"] = "https://app.test" })
            .Build());
        return services.BuildServiceProvider();
    }

    private (MissedClockInReminderService Service, ServiceProvider Provider) NewService()
    {
        var provider = BuildProvider();
        var service = new MissedClockInReminderService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<MissedClockInReminderService>.Instance);
        return (service, provider);
    }

    private static Task InvokeAsync(object target, string methodName, params object?[] args)
    {
        var method = target.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"{methodName} no longer exists — update this test.");
        return (Task)method.Invoke(target, args)!;
    }

    // ── Auto-invalidation of runaway sessions ─────────────────────────────────

    [Fact]
    public async Task AutoInvalidate_ClosesASessionOlderThanTheConfiguredMaximum()
    {
        var user = Db.AddUser();
        Db.AddConfiguration();
        await Db.SaveChangesAsync();
        // 20h ago is past the 13h default, so this one never got clocked out.
        var stale = Db.AddOpenSession(user.Id, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)), TimeSpan.Zero);
        stale.ClockIn = DateTimeOffset.UtcNow.AddHours(-20);
        stale.ClockInServerStamp = stale.ClockIn;
        await Db.SaveChangesAsync();

        var (service, provider) = NewService();
        await using (provider)
            await InvokeAsync(service, "AutoInvalidateExpiredSessionsAsync", CancellationToken.None);

        var saved = await NewContext().WorkSessions.SingleAsync();
        Assert.Equal(WorkSessionStatus.Invalidated, saved.Status);
    }

    [Fact]
    public async Task AutoInvalidate_LeavesASessionInsideTheWindowAlone()
    {
        var user = Db.AddUser();
        Db.AddConfiguration();
        await Db.SaveChangesAsync();
        var running = Db.AddOpenSession(user.Id, DateOnly.FromDateTime(DateTime.UtcNow), TimeSpan.Zero);
        running.ClockIn = DateTimeOffset.UtcNow.AddHours(-2);
        running.ClockInServerStamp = running.ClockIn;
        await Db.SaveChangesAsync();

        var (service, provider) = NewService();
        await using (provider)
            await InvokeAsync(service, "AutoInvalidateExpiredSessionsAsync", CancellationToken.None);

        Assert.Equal(WorkSessionStatus.Open, (await NewContext().WorkSessions.SingleAsync()).Status);
    }

    [Fact]
    public async Task AutoInvalidate_HonoursAShorterConfiguredMaximum()
    {
        var user = Db.AddUser();
        var config = Db.AddConfiguration();
        config.MaxSessionHours = 4m;
        await Db.SaveChangesAsync();
        var session = Db.AddOpenSession(user.Id, DateOnly.FromDateTime(DateTime.UtcNow), TimeSpan.Zero);
        session.ClockIn = DateTimeOffset.UtcNow.AddHours(-6);
        session.ClockInServerStamp = session.ClockIn;
        await Db.SaveChangesAsync();

        var (service, provider) = NewService();
        await using (provider)
            await InvokeAsync(service, "AutoInvalidateExpiredSessionsAsync", CancellationToken.None);

        Assert.Equal(WorkSessionStatus.Invalidated, (await NewContext().WorkSessions.SingleAsync()).Status);
    }

    [Fact]
    public async Task AutoInvalidate_ClosesAnOpenBreakSoTheDayIsNotLeftMidBreak()
    {
        var user = Db.AddUser();
        Db.AddConfiguration();
        await Db.SaveChangesAsync();
        var session = Db.AddOpenSession(user.Id, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)), TimeSpan.Zero);
        session.ClockIn = DateTimeOffset.UtcNow.AddHours(-20);
        session.ClockInServerStamp = session.ClockIn;
        await Db.SaveChangesAsync();
        Db.BreakRecords.Add(new BreakRecord
        {
            WorkSessionId = session.Id,
            BreakStart = DateTimeOffset.UtcNow.AddHours(-18),
            BreakStartServerStamp = DateTimeOffset.UtcNow.AddHours(-18),
        });
        await Db.SaveChangesAsync();

        var (service, provider) = NewService();
        await using (provider)
            await InvokeAsync(service, "AutoInvalidateExpiredSessionsAsync", CancellationToken.None);

        var record = await NewContext().BreakRecords.SingleAsync();
        Assert.NotNull(record.BreakEnd);
        Assert.NotNull(record.BreakEndServerStamp);
    }

    [Fact]
    public async Task AutoInvalidate_TellsTheEmployeeTheirSessionWasClosed()
    {
        // Silently invalidating would cost someone a day's hours with no way to notice.
        var user = Db.AddUser();
        Db.AddConfiguration();
        await Db.SaveChangesAsync();
        var session = Db.AddOpenSession(user.Id, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)), TimeSpan.Zero);
        session.ClockIn = DateTimeOffset.UtcNow.AddHours(-20);
        session.ClockInServerStamp = session.ClockIn;
        await Db.SaveChangesAsync();

        var (service, provider) = NewService();
        await using (provider)
            await InvokeAsync(service, "AutoInvalidateExpiredSessionsAsync", CancellationToken.None);

        await _notifications.Received(1).NotifyUserAsync(
            user.Id,
            Arg.Is<string>(m => m.Contains("adjustment request")),
            NotificationType.SessionInvalidated,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AutoInvalidate_StillClosesTheSessionWhenTheNotificationFails()
    {
        var user = Db.AddUser();
        Db.AddConfiguration();
        await Db.SaveChangesAsync();
        var session = Db.AddOpenSession(user.Id, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)), TimeSpan.Zero);
        session.ClockIn = DateTimeOffset.UtcNow.AddHours(-20);
        session.ClockInServerStamp = session.ClockIn;
        await Db.SaveChangesAsync();

        _notifications.NotifyUserAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<NotificationType>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("notification store down"));

        var (service, provider) = NewService();
        await using (provider)
            await InvokeAsync(service, "AutoInvalidateExpiredSessionsAsync", CancellationToken.None);

        Assert.Equal(WorkSessionStatus.Invalidated, (await NewContext().WorkSessions.SingleAsync()).Status);
    }

    [Fact]
    public async Task TheHostedServiceRunsAutoInvalidationWhenItStarts()
    {
        // The reflection tests above prove the step works; this one proves it is actually
        // reached by the loop, which is the part reflection cannot vouch for.
        var user = Db.AddUser();
        Db.AddConfiguration();
        await Db.SaveChangesAsync();
        var session = Db.AddOpenSession(user.Id, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)), TimeSpan.Zero);
        session.ClockIn = DateTimeOffset.UtcNow.AddHours(-20);
        session.ClockInServerStamp = session.ClockIn;
        await Db.SaveChangesAsync();

        var (service, provider) = NewService();
        await using (provider)
        {
            await service.StartAsync(CancellationToken.None);
            try
            {
                var deadline = DateTime.UtcNow.AddSeconds(10);
                while (DateTime.UtcNow < deadline)
                {
                    if ((await NewContext().WorkSessions.SingleAsync()).Status == WorkSessionStatus.Invalidated)
                        break;
                    await Task.Delay(100);
                }
            }
            finally
            {
                await service.StopAsync(CancellationToken.None);
            }
        }

        Assert.Equal(WorkSessionStatus.Invalidated, (await NewContext().WorkSessions.SingleAsync()).Status);
    }

    // ── Previous working day resolution ───────────────────────────────────────

    private async Task<DateOnly?> PreviousWorkingDayAsync(DateOnly today)
    {
        var method = typeof(MissedClockInReminderService)
            .GetMethod("GetPreviousWorkingDayAsync", BindingFlags.NonPublic | BindingFlags.Static)!;
        var task = (Task<DateOnly?>)method.Invoke(null, [today, NewContext(), CancellationToken.None])!;
        return await task;
    }

    [Fact]
    public async Task PreviousWorkingDay_IsSimplyYesterdayMidWeek()
        => Assert.Equal(new DateOnly(2026, 3, 3), await PreviousWorkingDayAsync(new DateOnly(2026, 3, 4)));

    [Fact]
    public async Task PreviousWorkingDay_SkipsBackOverTheWeekend()
    {
        // Monday's reminder must chase Friday, not Sunday.
        Assert.Equal(new DateOnly(2026, 3, 6), await PreviousWorkingDayAsync(new DateOnly(2026, 3, 9)));
    }

    [Fact]
    public async Task PreviousWorkingDay_SkipsNonWorkingPublicHolidays()
    {
        Db.AddHoliday(new DateOnly(2026, 3, 3), "Midweek holiday");
        await Db.SaveChangesAsync();

        Assert.Equal(new DateOnly(2026, 3, 2), await PreviousWorkingDayAsync(new DateOnly(2026, 3, 4)));
    }

    [Fact]
    public async Task PreviousWorkingDay_CountsAHolidayTheCompanyWorksThrough()
    {
        Db.AddHoliday(new DateOnly(2026, 3, 3), "Worked holiday", isWorkingDay: true);
        await Db.SaveChangesAsync();

        Assert.Equal(new DateOnly(2026, 3, 3), await PreviousWorkingDayAsync(new DateOnly(2026, 3, 4)));
    }

    [Fact]
    public async Task PreviousWorkingDay_GivesUpAfterALongShutdown()
    {
        // Ten consecutive non-working days and it stops looking rather than scanning forever.
        for (var d = new DateOnly(2026, 2, 20); d <= new DateOnly(2026, 3, 3); d = d.AddDays(1))
            Db.AddHoliday(d, "Shutdown");
        await Db.SaveChangesAsync();

        Assert.Null(await PreviousWorkingDayAsync(new DateOnly(2026, 3, 4)));
    }

    // ── Missed clock-in reminders ─────────────────────────────────────────────

    [Fact]
    public async Task MissedClockInReminders_AreSkippedWhenTurnedOff()
    {
        var user = Db.AddUser();
        Db.AddConfiguration();
        await Db.SaveChangesAsync();
        var config = await Db.AppConfigurations.SingleAsync();
        config.EnableMissedClockInEmails = false;
        await Db.SaveChangesAsync();

        var (service, provider) = NewService();
        await using (provider)
            await InvokeAsync(service, "SendRemindersAsync", CancellationToken.None);

        await _email.DidNotReceiveWithAnyArgs()
            .SendMissedClockInReminderAsync(default!, default!, default);
        Assert.NotNull(user);
    }

    // ── Calendar token expiry warnings ────────────────────────────────────────

    private async Task<User> ArrangeUserWithTokenExpiringInAsync(TimeSpan window, bool alreadyNotified = false)
    {
        var user = Db.AddUser();
        user.CalendarTokenHash = new string('a', 64);
        user.CalendarTokenExpiresAt = DateTimeOffset.UtcNow.Add(window);
        user.CalendarTokenExpiryNotifiedAt = alreadyNotified ? DateTimeOffset.UtcNow.AddDays(-1) : null;
        await Db.SaveChangesAsync();
        return user;
    }

    [Fact]
    public async Task CalendarExpiry_WarnsInsideTheFourteenDayWindowAndRecordsThat()
    {
        var user = await ArrangeUserWithTokenExpiringInAsync(TimeSpan.FromDays(7));

        var (service, provider) = NewService();
        await using (provider)
            await InvokeAsync(service, "SendCalendarTokenExpiryRemindersAsync", CancellationToken.None);

        await _email.Received(1).SendCalendarTokenExpiringEmailAsync(
            user.Email!, user.FullName, Arg.Any<DateTimeOffset>());
        Assert.NotNull((await NewContext().Users.SingleAsync(u => u.Id == user.Id)).CalendarTokenExpiryNotifiedAt);
    }

    [Fact]
    public async Task CalendarExpiry_DoesNotWarnTwiceForTheSameToken()
    {
        await ArrangeUserWithTokenExpiringInAsync(TimeSpan.FromDays(7), alreadyNotified: true);

        var (service, provider) = NewService();
        await using (provider)
            await InvokeAsync(service, "SendCalendarTokenExpiryRemindersAsync", CancellationToken.None);

        await _email.DidNotReceiveWithAnyArgs()
            .SendCalendarTokenExpiringEmailAsync(default!, default!, default);
    }

    [Fact]
    public async Task CalendarExpiry_StaysQuietWhileTheTokenIsStillFarFromExpiring()
    {
        await ArrangeUserWithTokenExpiringInAsync(TimeSpan.FromDays(60));

        var (service, provider) = NewService();
        await using (provider)
            await InvokeAsync(service, "SendCalendarTokenExpiryRemindersAsync", CancellationToken.None);

        await _email.DidNotReceiveWithAnyArgs()
            .SendCalendarTokenExpiringEmailAsync(default!, default!, default);
    }

    [Fact]
    public async Task CalendarExpiry_IgnoresAnAlreadyExpiredToken()
    {
        // Warning that something expired last week helps nobody; they just regenerate.
        await ArrangeUserWithTokenExpiringInAsync(TimeSpan.FromDays(-1));

        var (service, provider) = NewService();
        await using (provider)
            await InvokeAsync(service, "SendCalendarTokenExpiryRemindersAsync", CancellationToken.None);

        await _email.DidNotReceiveWithAnyArgs()
            .SendCalendarTokenExpiringEmailAsync(default!, default!, default);
    }

    [Fact]
    public async Task CalendarExpiry_LeavesTheFlagUnsetWhenTheEmailFails()
    {
        // The flag is what suppresses the retry, so setting it on a failed send would
        // mean the warning is lost for good.
        var user = await ArrangeUserWithTokenExpiringInAsync(TimeSpan.FromDays(3));
        _email.SendCalendarTokenExpiringEmailAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateTimeOffset>())
            .Throws(new InvalidOperationException("SMTP down"));

        var (service, provider) = NewService();
        await using (provider)
            await InvokeAsync(service, "SendCalendarTokenExpiryRemindersAsync", CancellationToken.None);

        Assert.Null((await NewContext().Users.SingleAsync(u => u.Id == user.Id)).CalendarTokenExpiryNotifiedAt);
    }

    // ── Month-end settlement generation ───────────────────────────────────────

    [Fact]
    public async Task MonthEnd_GeneratesSettlementsForThePriorMonth()
    {
        var (service, provider) = NewService();
        await using (provider)
            await InvokeAsync(service, "GenerateMonthlySettlementsAsync", CancellationToken.None);

        var priorMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1).AddMonths(-1);
        await _settlements.Received(1).GenerateForAllEmployeesAsync(
            priorMonth.Year, priorMonth.Month, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MonthEnd_SwallowsAFailureSoTheLoopKeepsRunning()
    {
        _settlements.GenerateForAllEmployeesAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("database unreachable"));

        var (service, provider) = NewService();
        await using (provider)
            await InvokeAsync(service, "GenerateMonthlySettlementsAsync", CancellationToken.None);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SettlementEmail_LinksToTheAppAndPassesTheReminderFlag(bool isReminder)
    {
        var (service, provider) = NewService();
        await using (provider)
            await InvokeAsync(service, "SendSettlementReviewEmailAsync", isReminder, CancellationToken.None);

        await _settlements.Received(1).SendReviewEmailAsync("https://app.test", isReminder, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SettlementEmail_SwallowsAFailureSoTheLoopKeepsRunning()
    {
        _settlements.SendReviewEmailAsync(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("database unreachable"));

        var (service, provider) = NewService();
        await using (provider)
            await InvokeAsync(service, "SendSettlementReviewEmailAsync", true, CancellationToken.None);
    }
}
