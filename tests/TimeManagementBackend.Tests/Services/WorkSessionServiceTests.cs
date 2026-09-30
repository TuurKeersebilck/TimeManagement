using Microsoft.EntityFrameworkCore;
using TimeManagementBackend.Exceptions;
using TimeManagementBackend.Models;
using TimeManagementBackend.Models.DTOs;
using TimeManagementBackend.Services;
using TimeManagementBackend.Tests.Infrastructure;

namespace TimeManagementBackend.Tests.Services;

/// <summary>
/// The clock-in/out state machine. Its invariants (one open session, one open break, times that
/// never run backwards) are what stop an employee's day from being silently unreconstructable,
/// and several of them are enforced by Postgres partial indexes rather than by C#.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class WorkSessionServiceTests(PostgresFixture fixture) : DatabaseTestBase(fixture)
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    private WorkSessionService NewService() => new(NewContext(), Mapper);

    /// <summary>Server stamps are truncated to the minute, so tests compare against the same.</summary>
    private static DateTimeOffset NowToMinute()
    {
        var now = DateTimeOffset.UtcNow;
        return new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, TimeSpan.Zero);
    }

    private async Task<User> ArrangeUserAsync()
    {
        var user = Db.AddUser();
        await Db.SaveChangesAsync();
        return user;
    }

    // ── Clock in ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task ClockIn_OpensASessionStampedByTheServer()
    {
        var user = await ArrangeUserAsync();

        var dto = await NewService().ClockInAsync(user.Id, new ClockInDto());

        var saved = await NewContext().WorkSessions.SingleAsync();
        Assert.Equal(WorkSessionStatus.Open, saved.Status);
        Assert.Equal(Today, saved.Date);
        Assert.Null(saved.ClockOut);
        Assert.Equal(NowToMinute(), saved.ClockInServerStamp);
        Assert.Equal(saved.Id, dto.Id);
    }

    [Fact]
    public async Task ClockIn_RecordsWorkedFromHomeOnTheDay()
    {
        var user = await ArrangeUserAsync();

        await NewService().ClockInAsync(user.Id, new ClockInDto { WorkedFromHome = true });

        var workDay = await NewContext().WorkDays.SingleAsync();
        Assert.True(workDay.WorkedFromHome);
        Assert.Equal(Today, workDay.Date);
    }

    [Fact]
    public async Task ClockIn_AcceptsAClientTimeWithinTheAllowedDrift()
    {
        // Phones are a minute or two out; the client's own stamp is honoured within ±5 min
        // so the recorded time matches what the employee saw when they tapped the button.
        var user = await ArrangeUserAsync();
        var clientTime = NowToMinute().AddMinutes(-3);

        await NewService().ClockInAsync(user.Id, new ClockInDto { RecordedAt = clientTime });

        var saved = await NewContext().WorkSessions.SingleAsync();
        Assert.Equal(clientTime, saved.ClockIn);
        // The immutable audit stamp still records when the server actually saw it.
        Assert.Equal(NowToMinute(), saved.ClockInServerStamp);
    }

    [Fact]
    public async Task ClockIn_IgnoresAClientTimeBeyondTheAllowedDrift()
    {
        // Otherwise a device with a wrong clock — or a tampered payload — could backdate work.
        var user = await ArrangeUserAsync();

        await NewService().ClockInAsync(user.Id,
            new ClockInDto { RecordedAt = NowToMinute().AddHours(-4) });

        var saved = await NewContext().WorkSessions.SingleAsync();
        Assert.Equal(NowToMinute(), saved.ClockIn);
    }

    [Fact]
    public async Task ClockIn_RefusesASecondOpenSession()
    {
        var user = await ArrangeUserAsync();
        await NewService().ClockInAsync(user.Id, new ClockInDto());

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            NewService().ClockInAsync(user.Id, new ClockInDto()));

        Assert.Equal("You are already clocked in.", ex.Message);
        Assert.Equal(1, await NewContext().WorkSessions.CountAsync());
    }

    [Fact]
    public async Task ClockIn_IsAllowedAfterClockingOut()
    {
        var user = await ArrangeUserAsync();
        await NewService().ClockInAsync(user.Id, new ClockInDto());
        await NewService().ClockOutAsync(user.Id, new ClockOutDto());

        await NewService().ClockInAsync(user.Id, new ClockInDto());

        var sessions = await NewContext().WorkSessions.ToListAsync();
        Assert.Equal(2, sessions.Count);
        Assert.Single(sessions, s => s.Status == WorkSessionStatus.Open);
    }

    [Fact]
    public async Task ClockIn_IsRefusedOnAFullDayOfLeave()
    {
        var user = await ArrangeUserAsync();
        var type = Db.AddVacationType();
        Db.AddVacationDay(user.Id, type, Today, 1.0m);
        await Db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            NewService().ClockInAsync(user.Id, new ClockInDto()));

        Assert.Contains("full-day vacation", ex.Message);
        Assert.Empty(await NewContext().WorkSessions.ToListAsync());
    }

    [Fact]
    public async Task ClockIn_IsAllowedOnAHalfDayOfLeave()
    {
        var user = await ArrangeUserAsync();
        var type = Db.AddVacationType();
        Db.AddVacationDay(user.Id, type, Today, 0.5m);
        await Db.SaveChangesAsync();

        await NewService().ClockInAsync(user.Id, new ClockInDto());

        Assert.Single(await NewContext().WorkSessions.ToListAsync());
    }

    [Fact]
    public async Task ClockIn_IsNotBlockedByAnotherEmployeesOpenSession()
    {
        var user = await ArrangeUserAsync();
        var colleague = Db.AddUser("Colin Colleague");
        await Db.SaveChangesAsync();
        await NewService().ClockInAsync(colleague.Id, new ClockInDto());

        await NewService().ClockInAsync(user.Id, new ClockInDto());

        Assert.Equal(2, await NewContext().WorkSessions.CountAsync());
    }

    [Fact]
    public async Task ClockIn_DerivesTheDateFromTheSuppliedTimeZone()
    {
        // At 23:30 UTC it is already tomorrow in Auckland; the day the work belongs to is
        // the employee's local day, not the server's.
        var user = await ArrangeUserAsync();
        var utcNow = DateTimeOffset.UtcNow;

        await NewService().ClockInAsync(user.Id, new ClockInDto { TimeZoneId = "Pacific/Auckland" });

        var expected = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(
            utcNow.UtcDateTime, TimeZoneInfo.FindSystemTimeZoneById("Pacific/Auckland")));
        Assert.Equal(expected, (await NewContext().WorkSessions.SingleAsync()).Date);
    }

    [Fact]
    public async Task ClockIn_FallsBackToUtcForAnUnknownTimeZone()
    {
        var user = await ArrangeUserAsync();

        await NewService().ClockInAsync(user.Id, new ClockInDto { TimeZoneId = "Mars/Olympus_Mons" });

        Assert.Equal(Today, (await NewContext().WorkSessions.SingleAsync()).Date);
    }

    // ── Clock out ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task ClockOut_ClosesTheSessionAndStoresTheDescription()
    {
        var user = await ArrangeUserAsync();
        await NewService().ClockInAsync(user.Id, new ClockInDto());

        await NewService().ClockOutAsync(user.Id, new ClockOutDto { Description = "Shipped the invoice report" });

        var context = NewContext();
        var saved = await context.WorkSessions.SingleAsync();
        Assert.Equal(WorkSessionStatus.Closed, saved.Status);
        Assert.NotNull(saved.ClockOut);
        Assert.Equal(NowToMinute(), saved.ClockOutServerStamp);
        Assert.Equal("Shipped the invoice report", (await context.WorkDays.SingleAsync()).Description);
    }

    [Fact]
    public async Task ClockOut_RequiresAnOpenSession()
    {
        var user = await ArrangeUserAsync();

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            NewService().ClockOutAsync(user.Id, new ClockOutDto()));

        Assert.Equal("You are not clocked in.", ex.Message);
    }

    [Fact]
    public async Task ClockOut_RequiresTheBreakToBeEndedFirst()
    {
        // A session closed mid-break would record the break as never having finished.
        var user = await ArrangeUserAsync();
        await NewService().ClockInAsync(user.Id, new ClockInDto());
        await NewService().StartBreakAsync(user.Id, new StartBreakDto());

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            NewService().ClockOutAsync(user.Id, new ClockOutDto()));

        Assert.Contains("end your break", ex.Message);
        Assert.Equal(WorkSessionStatus.Open, (await NewContext().WorkSessions.SingleAsync()).Status);
    }

    [Fact]
    public async Task ClockOut_RefusesATimeBeforeClockIn()
    {
        var user = await ArrangeUserAsync();
        // An open session stamped an hour into the future makes "now" an invalid clock-out.
        Db.AddOpenSession(user.Id, Today, DateTimeOffset.UtcNow.TimeOfDay.Add(TimeSpan.FromHours(1)));
        await Db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            NewService().ClockOutAsync(user.Id, new ClockOutDto()));

        Assert.Contains("cannot be before clock-in", ex.Message);
    }

    // ── Breaks ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task StartBreak_OpensABreakOnTheCurrentSession()
    {
        var user = await ArrangeUserAsync();
        await NewService().ClockInAsync(user.Id, new ClockInDto());

        var dto = await NewService().StartBreakAsync(user.Id, new StartBreakDto());

        var saved = await NewContext().BreakRecords.SingleAsync();
        Assert.Equal(dto.Id, saved.Id);
        Assert.Null(saved.BreakEnd);
        Assert.Equal(NowToMinute(), saved.BreakStartServerStamp);
    }

    [Fact]
    public async Task StartBreak_RequiresAnOpenSession()
    {
        var user = await ArrangeUserAsync();

        await Assert.ThrowsAsync<ValidationException>(() =>
            NewService().StartBreakAsync(user.Id, new StartBreakDto()));
    }

    [Fact]
    public async Task StartBreak_RefusesASecondConcurrentBreak()
    {
        var user = await ArrangeUserAsync();
        await NewService().ClockInAsync(user.Id, new ClockInDto());
        await NewService().StartBreakAsync(user.Id, new StartBreakDto());

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            NewService().StartBreakAsync(user.Id, new StartBreakDto()));

        Assert.Equal("You are already on a break.", ex.Message);
        Assert.Equal(1, await NewContext().BreakRecords.CountAsync());
    }

    [Fact]
    public async Task StartBreak_IsRefusedOnAHalfDayOfLeave()
    {
        var user = await ArrangeUserAsync();
        var type = Db.AddVacationType();
        Db.AddVacationDay(user.Id, type, Today, 0.5m);
        await Db.SaveChangesAsync();
        await NewService().ClockInAsync(user.Id, new ClockInDto());

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            NewService().StartBreakAsync(user.Id, new StartBreakDto()));

        Assert.Contains("half-day vacation", ex.Message);
    }

    [Fact]
    public async Task EndBreak_ClosesTheOpenBreak()
    {
        var user = await ArrangeUserAsync();
        await NewService().ClockInAsync(user.Id, new ClockInDto());
        await NewService().StartBreakAsync(user.Id, new StartBreakDto());

        await NewService().EndBreakAsync(user.Id, new EndBreakDto());

        var saved = await NewContext().BreakRecords.SingleAsync();
        Assert.NotNull(saved.BreakEnd);
        Assert.NotNull(saved.BreakEndServerStamp);
    }

    [Fact]
    public async Task EndBreak_RequiresABreakToBeRunning()
    {
        var user = await ArrangeUserAsync();
        await NewService().ClockInAsync(user.Id, new ClockInDto());

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            NewService().EndBreakAsync(user.Id, new EndBreakDto()));

        Assert.Equal("You are not on a break.", ex.Message);
    }

    [Fact]
    public async Task EndBreak_EnforcesTheMinimumBreakDuration()
    {
        var user = await ArrangeUserAsync();
        Db.AddConfiguration(minimumBreakMinutes: 30);
        await Db.SaveChangesAsync();
        await NewService().ClockInAsync(user.Id, new ClockInDto());
        await NewService().StartBreakAsync(user.Id, new StartBreakDto());

        var ex = await Assert.ThrowsAsync<BreakTooShortException>(() =>
            NewService().EndBreakAsync(user.Id, new EndBreakDto()));

        Assert.Equal(30, ex.RequiredMinutes);
        Assert.Equal(0, ex.ElapsedMinutes);
        Assert.Null((await NewContext().BreakRecords.SingleAsync()).BreakEnd); // still running
    }

    [Fact]
    public async Task EndBreak_NeverProducesANegativeDuration()
    {
        // BreakStart keeps seconds while end stamps are truncated to the minute, so a break
        // ended in its own starting minute would otherwise compute as negative time.
        var user = await ArrangeUserAsync();
        var session = Db.AddOpenSession(user.Id, Today, DateTimeOffset.UtcNow.TimeOfDay.Subtract(TimeSpan.FromHours(1)));
        await Db.SaveChangesAsync();

        var breakStart = NowToMinute().AddSeconds(30);
        Db.BreakRecords.Add(new BreakRecord
        {
            WorkSessionId = session.Id,
            BreakStart = breakStart,
            BreakStartServerStamp = breakStart,
        });
        await Db.SaveChangesAsync();

        await NewService().EndBreakAsync(user.Id, new EndBreakDto());

        var saved = await NewContext().BreakRecords.SingleAsync();
        Assert.Equal(breakStart, saved.BreakEnd);
        Assert.True(saved.BreakEnd >= saved.BreakStart);
    }

    // ── Reads ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetToday_SeparatesTheOpenSessionFromClosedOnes()
    {
        var user = await ArrangeUserAsync();
        await NewService().ClockInAsync(user.Id, new ClockInDto());
        await NewService().ClockOutAsync(user.Id, new ClockOutDto());
        await NewService().ClockInAsync(user.Id, new ClockInDto());

        var status = await NewService().GetTodayAsync(user.Id);

        Assert.NotNull(status.OpenSession);
        Assert.Single(status.ClosedSessions);
    }

    [Fact]
    public async Task GetToday_ShowsNothingForAnotherUsersDay()
    {
        var user = await ArrangeUserAsync();
        var colleague = Db.AddUser("Colin Colleague");
        await Db.SaveChangesAsync();
        await NewService().ClockInAsync(colleague.Id, new ClockInDto());

        var status = await NewService().GetTodayAsync(user.Id);

        Assert.Null(status.OpenSession);
        Assert.Empty(status.ClosedSessions);
    }

    [Fact]
    public async Task GetTodayLive_IsNullWhenNotClockedIn()
        => Assert.Null(await NewService().GetTodayLiveAsync((await ArrangeUserAsync()).Id));

    [Fact]
    public async Task GetTodayLive_ReportsElapsedTimeNetOfBreaks()
    {
        var user = await ArrangeUserAsync();
        var session = Db.AddOpenSession(user.Id, Today, DateTimeOffset.UtcNow.TimeOfDay.Subtract(TimeSpan.FromHours(2)));
        await Db.SaveChangesAsync();
        Db.BreakRecords.Add(new BreakRecord
        {
            WorkSessionId = session.Id,
            BreakStart = DateTimeOffset.UtcNow.AddMinutes(-60),
            BreakStartServerStamp = DateTimeOffset.UtcNow.AddMinutes(-60),
            BreakEnd = DateTimeOffset.UtcNow.AddMinutes(-30),
            BreakEndServerStamp = DateTimeOffset.UtcNow.AddMinutes(-30),
        });
        await Db.SaveChangesAsync();

        var live = await NewService().GetTodayLiveAsync(user.Id);

        Assert.NotNull(live);
        Assert.False(live.IsOnBreak);
        Assert.InRange(live.ElapsedMinutes, 89, 91); // 120 worked minus a 30 minute break
    }

    [Fact]
    public async Task GetTodayLive_ExcludesAnOngoingBreakFromElapsedTime()
    {
        var user = await ArrangeUserAsync();
        var session = Db.AddOpenSession(user.Id, Today, DateTimeOffset.UtcNow.TimeOfDay.Subtract(TimeSpan.FromHours(2)));
        await Db.SaveChangesAsync();
        Db.BreakRecords.Add(new BreakRecord
        {
            WorkSessionId = session.Id,
            BreakStart = DateTimeOffset.UtcNow.AddMinutes(-15),
            BreakStartServerStamp = DateTimeOffset.UtcNow.AddMinutes(-15),
        });
        await Db.SaveChangesAsync();

        var live = await NewService().GetTodayLiveAsync(user.Id);

        Assert.NotNull(live);
        Assert.True(live.IsOnBreak);
        Assert.NotNull(live.BreakStartedAt);
        Assert.InRange(live.ElapsedMinutes, 104, 106); // the running break is already discounted
    }

    [Fact]
    public async Task GetSummaries_GroupsByDayAndNetsOffBreaks()
    {
        var user = await ArrangeUserAsync();
        var date = new DateOnly(2026, 3, 2);
        Db.AddClosedSession(user.Id, date, TimeSpan.FromHours(9), new TimeSpan(17, 30, 0),
            aBreak: (new TimeSpan(12, 0, 0), new TimeSpan(12, 30, 0)));
        await Db.SaveChangesAsync();

        var summary = Assert.Single(await NewService().GetSummariesAsync(user.Id, date, date));

        Assert.Equal(date, summary.Date);
        Assert.Equal(8.0, summary.TotalWorkedHours, 3);
        Assert.False(summary.HasOpenSession);
    }

    [Fact]
    public async Task GetSummaries_OmitsInvalidatedSessions()
    {
        var user = await ArrangeUserAsync();
        var date = new DateOnly(2026, 3, 2);
        var session = Db.AddClosedSession(user.Id, date, TimeSpan.FromHours(9), TimeSpan.FromHours(17));
        session.Status = WorkSessionStatus.Invalidated;
        await Db.SaveChangesAsync();

        Assert.Empty(await NewService().GetSummariesAsync(user.Id, date, date));
    }

    [Fact]
    public async Task GetSummaries_HonoursTheDateRange()
    {
        var user = await ArrangeUserAsync();
        Db.AddClosedSession(user.Id, new DateOnly(2026, 3, 2), TimeSpan.FromHours(9), TimeSpan.FromHours(17));
        Db.AddClosedSession(user.Id, new DateOnly(2026, 4, 2), TimeSpan.FromHours(9), TimeSpan.FromHours(17));
        await Db.SaveChangesAsync();

        var result = await NewService().GetSummariesAsync(
            user.Id, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));

        Assert.Equal(new DateOnly(2026, 3, 2), Assert.Single(result).Date);
    }

    [Fact]
    public async Task GetSummaries_SurfacesLeaveAlongsideTheHours()
    {
        var user = await ArrangeUserAsync();
        var date = new DateOnly(2026, 3, 2);
        var type = Db.AddVacationType("Annual Leave");
        Db.AddVacationDay(user.Id, type, date, 0.5m);
        Db.AddClosedSession(user.Id, date, TimeSpan.FromHours(9), TimeSpan.FromHours(13));
        await Db.SaveChangesAsync();

        var summary = Assert.Single(await NewService().GetSummariesAsync(user.Id, date, date));

        Assert.Equal(0.5m, summary.VacationAmount);
        Assert.Equal("Annual Leave", summary.VacationTypeName);
    }

    [Fact]
    public async Task GetSummaries_ShowsOnlyTheCallersOwnDays()
    {
        var user = await ArrangeUserAsync();
        var colleague = Db.AddUser("Colin Colleague");
        await Db.SaveChangesAsync();
        Db.AddClosedSession(colleague.Id, new DateOnly(2026, 3, 2), TimeSpan.FromHours(9), TimeSpan.FromHours(17));
        await Db.SaveChangesAsync();

        Assert.Empty(await NewService().GetSummariesAsync(user.Id, null, null));
    }

    // ── Schedule and day metadata ─────────────────────────────────────────────

    [Fact]
    public async Task GetMySchedule_ReturnsMondayToSundayWithSundayLast()
    {
        var user = await ArrangeUserAsync();

        var schedule = await NewService().GetMyWorkScheduleAsync(user.Id);

        Assert.Equal(7, schedule.WorkdayTargets.Count);
        Assert.Equal(DayOfWeek.Monday, schedule.WorkdayTargets.First().DayOfWeek);
        Assert.Equal(DayOfWeek.Sunday, schedule.WorkdayTargets.Last().DayOfWeek);
        Assert.Equal(8m, schedule.WorkdayTargets.First().Hours); // the seeded global default
    }

    [Fact]
    public async Task GetMySchedule_PrefersThePerEmployeeOverride()
    {
        var user = await ArrangeUserAsync();
        Db.AddWorkdayOverride(user.Id, DayOfWeek.Friday, 4m);
        Db.AddConfiguration(minimumBreakMinutes: 45, dailyAllowance: 1m, weeklyAllowance: 3m);
        await Db.SaveChangesAsync();

        var schedule = await NewService().GetMyWorkScheduleAsync(user.Id);

        Assert.Equal(4m, schedule.WorkdayTargets.Single(t => t.DayOfWeek == DayOfWeek.Friday).Hours);
        Assert.Equal(45, schedule.MinimumBreakMinutes);
        Assert.Equal(1m, schedule.DailyOvertimeAllowanceHours);
        Assert.Equal(3m, schedule.WeeklyOvertimeAllowanceHours);
    }

    [Fact]
    public async Task GetMySchedule_RejectsAnUnknownUser()
        => await Assert.ThrowsAsync<ResourceNotFoundException>(() =>
            NewService().GetMyWorkScheduleAsync(Guid.NewGuid().ToString()));

    [Fact]
    public async Task DefaultWfhWeekdays_RoundTripThroughTheBitmask()
    {
        var user = await ArrangeUserAsync();

        await NewService().SetDefaultWfhWeekdaysAsync(
            user.Id, [DayOfWeek.Tuesday, DayOfWeek.Thursday, DayOfWeek.Tuesday]);

        var schedule = await NewService().GetMyWorkScheduleAsync(user.Id);
        Assert.Equal([DayOfWeek.Tuesday, DayOfWeek.Thursday], schedule.DefaultWfhWeekdays);
    }

    [Fact]
    public async Task DefaultWfhWeekdays_CanBeClearedCompletely()
    {
        var user = await ArrangeUserAsync();
        await NewService().SetDefaultWfhWeekdaysAsync(user.Id, [DayOfWeek.Monday]);

        await NewService().SetDefaultWfhWeekdaysAsync(user.Id, []);

        Assert.Empty((await NewService().GetMyWorkScheduleAsync(user.Id)).DefaultWfhWeekdays);
    }

    [Fact]
    public async Task UpdateDay_CreatesTheRowWhenTheDayHasNoneYet()
    {
        var user = await ArrangeUserAsync();
        var date = new DateOnly(2026, 3, 2);

        var dto = await NewService().UpdateDayAsync(user.Id, date,
            new UpdateWorkDayDto { WorkedFromHome = true, Description = "Remote" });

        Assert.True(dto.WorkedFromHome);
        var saved = await NewContext().WorkDays.SingleAsync();
        Assert.Equal("Remote", saved.Description);
        Assert.Equal(date, saved.Date);
    }

    [Fact]
    public async Task UpdateDay_LeavesOmittedFieldsAlone()
    {
        // The form patches one field at a time, so a null must mean "unchanged", not "clear".
        var user = await ArrangeUserAsync();
        var date = new DateOnly(2026, 3, 2);
        await NewService().UpdateDayAsync(user.Id, date,
            new UpdateWorkDayDto { WorkedFromHome = true, Description = "Remote" });

        await NewService().UpdateDayAsync(user.Id, date, new UpdateWorkDayDto { WorkedFromHome = false });

        var saved = await NewContext().WorkDays.SingleAsync();
        Assert.False(saved.WorkedFromHome);
        Assert.Equal("Remote", saved.Description);
    }
}
