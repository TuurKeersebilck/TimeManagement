using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TimeManagementBackend.Exceptions;
using TimeManagementBackend.Models;
using TimeManagementBackend.Models.DTOs;
using TimeManagementBackend.Services;
using TimeManagementBackend.Tests.Infrastructure;

namespace TimeManagementBackend.Tests.Services;

/// <summary>
/// Confirming a settlement is the point where a flex balance turns into money paid and into the
/// opening balance of the next month, and it is irreversible — Settled rows are read-only. These
/// tests cover both what it writes and what it refuses to write.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class SettlementServiceTests(PostgresFixture fixture) : DatabaseTestBase(fixture)
{
    private readonly INotificationService _notifications = Substitute.For<INotificationService>();
    private readonly IEmailService _email = Substitute.For<IEmailService>();

    private SettlementService NewService()
    {
        var context = NewContext();
        return new SettlementService(
            context,
            new OvertimeCalculationService(context),
            _notifications,
            _email,
            Mapper,
            NullLogger<SettlementService>.Instance);
    }

    // ── Generation ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Generate_CreatesOnePendingSettlementPerActiveEmployee()
    {
        var employee = Db.AddUser("Emma Employee");
        var other = Db.AddUser("Oscar Other");
        Db.AddUser("Adam Admin", UserRole.Admin);
        await Db.SaveChangesAsync();
        await Db.SetGlobalScheduleAsync((DayOfWeek.Monday, 8m));

        await NewService().GenerateForAllEmployeesAsync(2026, 3);

        var settlements = await NewContext().MonthlySettlements.ToListAsync();

        // Admins do not accrue a flex balance, so they get no settlement row.
        Assert.Equal(2, settlements.Count);
        Assert.Contains(settlements, s => s.UserId == employee.Id);
        Assert.Contains(settlements, s => s.UserId == other.Id);
        Assert.All(settlements, s => Assert.Equal(SettlementStatus.PendingReview, s.Status));
    }

    [Fact]
    public async Task Generate_SkipsDisabledEmployees()
    {
        Db.AddUser("Dana Departed", isDisabled: true);
        await Db.SaveChangesAsync();

        await NewService().GenerateForAllEmployeesAsync(2026, 3);

        Assert.Empty(await NewContext().MonthlySettlements.ToListAsync());
    }

    [Fact]
    public async Task Generate_SplitsTheNetBalanceIntoOvertimeAndDeficit()
    {
        var surplus = Db.AddUser("Sam Surplus");
        var deficit = Db.AddUser("Dean Deficit");
        await Db.SaveChangesAsync();
        await Db.SetGlobalScheduleAsync((DayOfWeek.Monday, 8m));

        // Five Mondays in March 2026; work four of them and add a flat adjustment to control
        // the sign of each employee's month without arranging 40 sessions.
        Db.AddAdjustment(surplus.Id, new DateOnly(2026, 3, 1), 45m);   // -40 + 45 = +5
        Db.AddAdjustment(deficit.Id, new DateOnly(2026, 3, 1), 36.5m); // -40 + 36.5 = -3.5
        // Both employed since February, so all five March Mondays count as missed.
        Db.AddClosedSession(surplus.Id, new DateOnly(2026, 2, 2), TimeSpan.FromHours(9), TimeSpan.FromHours(17));
        Db.AddClosedSession(deficit.Id, new DateOnly(2026, 2, 2), TimeSpan.FromHours(9), TimeSpan.FromHours(17));
        await Db.SaveChangesAsync();

        await NewService().GenerateForAllEmployeesAsync(2026, 3);

        var rows = await NewContext().MonthlySettlements.ToDictionaryAsync(s => s.UserId);

        Assert.Equal(5m, rows[surplus.Id].NetBalanceHours);
        Assert.Equal(5m, rows[surplus.Id].OvertimeHours);
        Assert.Equal(0m, rows[surplus.Id].DeficitHours);

        Assert.Equal(-3.5m, rows[deficit.Id].NetBalanceHours);
        Assert.Equal(0m, rows[deficit.Id].OvertimeHours);
        Assert.Equal(3.5m, rows[deficit.Id].DeficitHours);
    }

    [Fact]
    public async Task Generate_IsIdempotent_AndNeverOverwritesASettledMonth()
    {
        var employee = Db.AddUser();
        await Db.SaveChangesAsync();
        Db.AddSettlement(employee.Id, 2026, 3, SettlementStatus.Settled, netBalanceHours: 12m);
        await Db.SaveChangesAsync();

        await NewService().GenerateForAllEmployeesAsync(2026, 3);

        var settlement = Assert.Single(await NewContext().MonthlySettlements.ToListAsync());
        Assert.Equal(SettlementStatus.Settled, settlement.Status);
        Assert.Equal(12m, settlement.NetBalanceHours); // untouched by the re-run
    }

    [Fact]
    public async Task Generate_ContinuesWithOtherEmployeesWhenOneFails()
    {
        // A single employee's calculation blowing up must not cost everyone else their row.
        var failing = Db.AddUser("Fay Failing");
        var healthy = Db.AddUser("Hank Healthy");
        await Db.SaveChangesAsync();

        var context = NewContext();
        var overtime = Substitute.For<IOvertimeCalculationService>();
        overtime.CalculateAsync(failing.Id, 2026, 3, Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("boom"));
        overtime.CalculateAsync(healthy.Id, 2026, 3, Arg.Any<CancellationToken>())
            .Returns(new OvertimeResultDto { Year = 2026, Month = 3, RunningBalanceHours = 4m });

        var service = new SettlementService(
            context, overtime, _notifications, _email, Mapper, NullLogger<SettlementService>.Instance);

        await service.GenerateForAllEmployeesAsync(2026, 3);

        var settlement = Assert.Single(await NewContext().MonthlySettlements.ToListAsync());
        Assert.Equal(healthy.Id, settlement.UserId);
        Assert.Equal(4m, settlement.NetBalanceHours);
    }

    // ── Confirmation ──────────────────────────────────────────────────────────

    private async Task<MonthlySettlement> ArrangePendingSettlementAsync(decimal netBalanceHours = 6m)
    {
        var employee = Db.AddUser();
        await Db.SaveChangesAsync();
        var settlement = Db.AddSettlement(
            employee.Id, 2026, 3, SettlementStatus.PendingReview, netBalanceHours);
        await Db.SaveChangesAsync();
        return settlement;
    }

    [Fact]
    public async Task Confirm_RecordsTheAllocationAndLocksTheRow()
    {
        var settlement = await ArrangePendingSettlementAsync(netBalanceHours: 6m);
        var admin = Db.AddUser("Adam Admin", UserRole.Admin);
        await Db.SaveChangesAsync();

        await NewService().ConfirmAsync(settlement.Id,
            new ConfirmSettlementDto { PaidOutHours = 4m, CarryForwardHours = 2m, Notes = "Paid with March payroll" },
            admin.Id);

        var saved = await NewContext().MonthlySettlements.SingleAsync();

        Assert.Equal(SettlementStatus.Settled, saved.Status);
        Assert.Equal(SettlementOutcome.Paid, saved.Outcome);
        Assert.Equal(4m, saved.PaidOutHours);
        Assert.Equal(2m, saved.CarriedForwardHours);
        Assert.Equal("Paid with March payroll", saved.Notes);
        Assert.Equal(admin.Id, saved.ReviewedByUserId);
        Assert.NotNull(saved.ReviewedAt);
    }

    [Fact]
    public async Task Confirm_WithNothingPaidOut_IsRecordedAsUnpaid()
    {
        var settlement = await ArrangePendingSettlementAsync();
        var admin = Db.AddUser("Adam Admin", UserRole.Admin);
        await Db.SaveChangesAsync();

        await NewService().ConfirmAsync(settlement.Id,
            new ConfirmSettlementDto { PaidOutHours = 0m, CarryForwardHours = 6m }, admin.Id);

        Assert.Equal(SettlementOutcome.Unpaid, (await NewContext().MonthlySettlements.SingleAsync()).Outcome);
    }

    [Fact]
    public async Task Confirm_MaterialisesTheCarryForwardOnNextMonthsBalance()
    {
        var settlement = await ArrangePendingSettlementAsync();
        var admin = Db.AddUser("Adam Admin", UserRole.Admin);
        await Db.SaveChangesAsync();

        await NewService().ConfirmAsync(settlement.Id,
            new ConfirmSettlementDto { PaidOutHours = 0m, CarryForwardHours = 2.5m }, admin.Id);

        var adjustment = Assert.Single(await NewContext().TimeBankAdjustments.ToListAsync());

        Assert.Equal(2.5m, adjustment.Hours);
        Assert.Equal(new DateOnly(2026, 4, 1), adjustment.EffectiveDate);
        Assert.Equal(settlement.Id, adjustment.SourceSettlementId);
        Assert.Equal(settlement.UserId, adjustment.UserId);
    }

    [Fact]
    public async Task Confirm_CarriesADeficitForwardAsANegativeAdjustment()
    {
        var settlement = await ArrangePendingSettlementAsync(netBalanceHours: -3m);
        var admin = Db.AddUser("Adam Admin", UserRole.Admin);
        await Db.SaveChangesAsync();

        await NewService().ConfirmAsync(settlement.Id,
            new ConfirmSettlementDto { PaidOutHours = 0m, CarryForwardHours = -3m }, admin.Id);

        var adjustment = Assert.Single(await NewContext().TimeBankAdjustments.ToListAsync());
        Assert.Equal(-3m, adjustment.Hours); // April opens three hours down
    }

    [Fact]
    public async Task Confirm_CarryForwardFromDecember_LandsInJanuaryOfTheNextYear()
    {
        var employee = Db.AddUser();
        await Db.SaveChangesAsync();
        var settlement = Db.AddSettlement(employee.Id, 2026, 12, SettlementStatus.PendingReview, 5m);
        var admin = Db.AddUser("Adam Admin", UserRole.Admin);
        await Db.SaveChangesAsync();

        await NewService().ConfirmAsync(settlement.Id,
            new ConfirmSettlementDto { PaidOutHours = 0m, CarryForwardHours = 5m }, admin.Id);

        var adjustment = Assert.Single(await NewContext().TimeBankAdjustments.ToListAsync());
        Assert.Equal(new DateOnly(2027, 1, 1), adjustment.EffectiveDate);
    }

    [Fact]
    public async Task Confirm_WithNothingCarried_CreatesNoAdjustment()
    {
        var settlement = await ArrangePendingSettlementAsync();
        var admin = Db.AddUser("Adam Admin", UserRole.Admin);
        await Db.SaveChangesAsync();

        await NewService().ConfirmAsync(settlement.Id,
            new ConfirmSettlementDto { PaidOutHours = 6m, CarryForwardHours = 0m }, admin.Id);

        Assert.Empty(await NewContext().TimeBankAdjustments.ToListAsync());
    }

    [Fact]
    public async Task Confirm_RoundsTheAllocationToTwoDecimals()
    {
        var settlement = await ArrangePendingSettlementAsync();
        var admin = Db.AddUser("Adam Admin", UserRole.Admin);
        await Db.SaveChangesAsync();

        await NewService().ConfirmAsync(settlement.Id,
            new ConfirmSettlementDto { PaidOutHours = 1.005m, CarryForwardHours = 2.344m }, admin.Id);

        var saved = await NewContext().MonthlySettlements.SingleAsync();
        Assert.Equal(1.00m, saved.PaidOutHours);
        Assert.Equal(2.34m, saved.CarriedForwardHours);
    }

    [Fact]
    public async Task Confirm_NotifiesTheEmployee()
    {
        var settlement = await ArrangePendingSettlementAsync();
        var admin = Db.AddUser("Adam Admin", UserRole.Admin);
        await Db.SaveChangesAsync();

        await NewService().ConfirmAsync(settlement.Id,
            new ConfirmSettlementDto { PaidOutHours = 6m, CarryForwardHours = 0m }, admin.Id);

        await _notifications.Received(1).NotifyUserAsync(
            settlement.UserId,
            Arg.Is<string>(m => m.Contains("6") && m.Contains("paid out")),
            NotificationType.MonthlySettlement,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Confirm_WritesHoursInTheAppsNotationWhateverTheServerCulture()
    {
        // Decimal hours with a dot, as everywhere else in the app — not "2,5h" on a Belgian server.
        var original = System.Globalization.CultureInfo.CurrentCulture;
        System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("nl-BE");
        try
        {
            var settlement = await ArrangePendingSettlementAsync();
            var admin = Db.AddUser("Adam Admin", UserRole.Admin);
            await Db.SaveChangesAsync();

            await NewService().ConfirmAsync(settlement.Id,
                new ConfirmSettlementDto { PaidOutHours = 2.5m, CarryForwardHours = 1.25m }, admin.Id);

            await _notifications.Received(1).NotifyUserAsync(
                settlement.UserId,
                Arg.Is<string>(m => m.Contains("— 2.5h paid out, 1.25h carried over")),
                NotificationType.MonthlySettlement,
                Arg.Any<CancellationToken>());
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public async Task Confirm_SurvivesAFailingNotification()
    {
        // The settlement is already committed by this point; losing the notification must
        // not surface as a failed confirm that tempts the admin to retry an irreversible action.
        var settlement = await ArrangePendingSettlementAsync();
        var admin = Db.AddUser("Adam Admin", UserRole.Admin);
        await Db.SaveChangesAsync();

        _notifications.NotifyUserAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<NotificationType>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("SMTP down"));

        await NewService().ConfirmAsync(settlement.Id,
            new ConfirmSettlementDto { PaidOutHours = 6m, CarryForwardHours = 0m }, admin.Id);

        Assert.Equal(SettlementStatus.Settled, (await NewContext().MonthlySettlements.SingleAsync()).Status);
    }

    // ── Refusals ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Confirm_RejectsAnUnknownSettlement()
    {
        var admin = Db.AddUser("Adam Admin", UserRole.Admin);
        await Db.SaveChangesAsync();

        await Assert.ThrowsAsync<ResourceNotFoundException>(() =>
            NewService().ConfirmAsync(4242, new ConfirmSettlementDto(), admin.Id));
    }

    [Fact]
    public async Task Confirm_RefusesToReopenASettledMonth()
    {
        var employee = Db.AddUser();
        await Db.SaveChangesAsync();
        var settlement = Db.AddSettlement(employee.Id, 2026, 3, SettlementStatus.Settled);
        var admin = Db.AddUser("Adam Admin", UserRole.Admin);
        await Db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            NewService().ConfirmAsync(settlement.Id,
                new ConfirmSettlementDto { PaidOutHours = 1m }, admin.Id));

        Assert.Contains("already been confirmed", ex.Message);
    }

    [Fact]
    public async Task Confirm_RejectsNegativePaidOutHours()
    {
        var settlement = await ArrangePendingSettlementAsync();
        var admin = Db.AddUser("Adam Admin", UserRole.Admin);
        await Db.SaveChangesAsync();

        await Assert.ThrowsAsync<ValidationException>(() =>
            NewService().ConfirmAsync(settlement.Id,
                new ConfirmSettlementDto { PaidOutHours = -1m }, admin.Id));

        Assert.Equal(SettlementStatus.PendingReview,
            (await NewContext().MonthlySettlements.SingleAsync()).Status);
    }

    [Fact]
    public async Task Confirm_IsBlockedByAnOpenSessionInTheMonth()
    {
        var settlement = await ArrangePendingSettlementAsync();
        var admin = Db.AddUser("Adam Admin", UserRole.Admin);
        Db.AddOpenSession(settlement.UserId, new DateOnly(2026, 3, 10), TimeSpan.FromHours(9));
        await Db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<SettlementBlockedException>(() =>
            NewService().ConfirmAsync(settlement.Id, new ConfirmSettlementDto(), admin.Id));

        var blocker = Assert.Single(ex.Blockers);
        Assert.Equal("OpenSession", blocker.Type);
        Assert.Contains("2026-03-10", blocker.Description);
    }

    [Fact]
    public async Task Confirm_IsBlockedByAnInvalidatedSession()
    {
        var settlement = await ArrangePendingSettlementAsync();
        var admin = Db.AddUser("Adam Admin", UserRole.Admin);
        var session = Db.AddClosedSession(
            settlement.UserId, new DateOnly(2026, 3, 11), TimeSpan.FromHours(9), TimeSpan.FromHours(17));
        session.Status = WorkSessionStatus.Invalidated;
        await Db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<SettlementBlockedException>(() =>
            NewService().ConfirmAsync(settlement.Id, new ConfirmSettlementDto(), admin.Id));

        Assert.Equal("InvalidatedSession", Assert.Single(ex.Blockers).Type);
    }

    [Fact]
    public async Task Confirm_IsBlockedByAPendingAdjustmentRequest()
    {
        var settlement = await ArrangePendingSettlementAsync();
        var admin = Db.AddUser("Adam Admin", UserRole.Admin);
        Db.TimeAdjustmentRequests.Add(new TimeAdjustmentRequest
        {
            UserId = settlement.UserId,
            Date = new DateOnly(2026, 3, 12),
            DesiredDaySnapshot = "{}",
            Reason = "forgot to clock out",
            Status = AdjustmentRequestStatus.Pending,
            ApprovalTokenHash = new string('a', 64),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
            RequestedAt = DateTimeOffset.UtcNow,
        });
        await Db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<SettlementBlockedException>(() =>
            NewService().ConfirmAsync(settlement.Id, new ConfirmSettlementDto(), admin.Id));

        Assert.Equal("PendingAdjustmentRequest", Assert.Single(ex.Blockers).Type);
    }

    [Fact]
    public async Task Confirm_ReportsEveryBlockerAtOnce()
    {
        // Surfacing them one at a time would make clearing a month a guessing game.
        var settlement = await ArrangePendingSettlementAsync();
        var admin = Db.AddUser("Adam Admin", UserRole.Admin);
        Db.AddOpenSession(settlement.UserId, new DateOnly(2026, 3, 10), TimeSpan.FromHours(9));
        Db.TimeAdjustmentRequests.Add(new TimeAdjustmentRequest
        {
            UserId = settlement.UserId,
            Date = new DateOnly(2026, 3, 12),
            DesiredDaySnapshot = "{}",
            Reason = "forgot to clock out",
            Status = AdjustmentRequestStatus.Pending,
            ApprovalTokenHash = new string('b', 64),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
            RequestedAt = DateTimeOffset.UtcNow,
        });
        await Db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<SettlementBlockedException>(() =>
            NewService().ConfirmAsync(settlement.Id, new ConfirmSettlementDto(), admin.Id));

        Assert.Equal(2, ex.Blockers.Count);
    }

    [Fact]
    public async Task Confirm_IgnoresUnresolvedSessionsOutsideTheSettlementMonth()
    {
        var settlement = await ArrangePendingSettlementAsync();
        var admin = Db.AddUser("Adam Admin", UserRole.Admin);
        Db.AddOpenSession(settlement.UserId, new DateOnly(2026, 4, 1), TimeSpan.FromHours(9));
        await Db.SaveChangesAsync();

        await NewService().ConfirmAsync(settlement.Id,
            new ConfirmSettlementDto { PaidOutHours = 1m }, admin.Id);

        Assert.Equal(SettlementStatus.Settled, (await NewContext().MonthlySettlements.SingleAsync()).Status);
    }

    [Fact]
    public async Task Confirm_IsNotBlockedByAnotherEmployeesOpenSession()
    {
        var settlement = await ArrangePendingSettlementAsync();
        var admin = Db.AddUser("Adam Admin", UserRole.Admin);
        var colleague = Db.AddUser("Colin Colleague");
        await Db.SaveChangesAsync();
        Db.AddOpenSession(colleague.Id, new DateOnly(2026, 3, 10), TimeSpan.FromHours(9));
        await Db.SaveChangesAsync();

        await NewService().ConfirmAsync(settlement.Id,
            new ConfirmSettlementDto { PaidOutHours = 1m }, admin.Id);

        Assert.Equal(SettlementStatus.Settled, (await NewContext().MonthlySettlements.SingleAsync()).Status);
    }

    // ── Reads ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetSettlements_ReturnsTheMonthOrderedByEmployeeName()
    {
        var zoe = Db.AddUser("Zoe Zulu");
        var amy = Db.AddUser("Amy Alpha");
        await Db.SaveChangesAsync();
        Db.AddSettlement(zoe.Id, 2026, 3);
        Db.AddSettlement(amy.Id, 2026, 3);
        Db.AddSettlement(amy.Id, 2026, 2); // a different month must not appear
        await Db.SaveChangesAsync();

        var result = (await NewService().GetSettlementsAsync(2026, 3)).ToList();

        Assert.Equal(["Amy Alpha", "Zoe Zulu"], result.Select(s => s.EmployeeName));
    }

    [Fact]
    public async Task GetEmployeeHistory_IsNewestFirst()
    {
        var employee = Db.AddUser();
        await Db.SaveChangesAsync();
        Db.AddSettlement(employee.Id, 2025, 12);
        Db.AddSettlement(employee.Id, 2026, 3);
        Db.AddSettlement(employee.Id, 2026, 1);
        await Db.SaveChangesAsync();

        var result = (await NewService().GetEmployeeHistoryAsync(employee.Id)).ToList();

        Assert.Equal([(2026, 3), (2026, 1), (2025, 12)], result.Select(s => (s.Year, s.Month)));
    }

    [Fact]
    public async Task GetSettlementDetail_RejectsAnUnknownId()
        => await Assert.ThrowsAsync<ResourceNotFoundException>(() => NewService().GetSettlementDetailAsync(999));

    // ── Review emails ─────────────────────────────────────────────────────────
    // The admin asked to be chased by email until settlements are confirmed; these pin down
    // who gets it, what it lists, and when it stays quiet.

    private async Task<(User A, User B)> ArrangeTwoEmployeesWithNotificationEmailAsync()
    {
        var a = Db.AddUser("Emma Employee");
        var b = Db.AddUser("Oscar Other");
        Db.AddConfiguration(notificationEmail: "admin@company.test");
        await Db.SaveChangesAsync();
        return (a, b);
    }

    [Fact]
    public async Task ReviewEmail_ListsPendingSettlementsPerMonthOldestFirst()
    {
        var (a, b) = await ArrangeTwoEmployeesWithNotificationEmailAsync();
        Db.AddSettlement(a.Id, 2026, 8, SettlementStatus.PendingReview);
        Db.AddSettlement(b.Id, 2026, 8, SettlementStatus.PendingReview);
        Db.AddSettlement(a.Id, 2026, 7, SettlementStatus.PendingReview);
        Db.AddSettlement(b.Id, 2026, 7, SettlementStatus.Settled); // confirmed — not listed
        await Db.SaveChangesAsync();

        IReadOnlyList<(DateOnly Month, int PendingCount)>? listed = null;
        await _email.SendSettlementReviewEmailAsync(
            Arg.Any<string>(), Arg.Do<IReadOnlyList<(DateOnly, int)>>(l => listed = l), Arg.Any<string>(), Arg.Any<bool>());

        await NewService().SendReviewEmailAsync("https://app.test/", isReminder: false);

        await _email.Received(1).SendSettlementReviewEmailAsync(
            "admin@company.test", Arg.Any<IReadOnlyList<(DateOnly, int)>>(), "https://app.test/admin/settlements", false);
        Assert.Equal([(new DateOnly(2026, 7, 1), 1), (new DateOnly(2026, 8, 1), 2)], listed);
    }

    [Fact]
    public async Task ReviewEmail_PassesTheReminderFlagThrough()
    {
        var (a, _) = await ArrangeTwoEmployeesWithNotificationEmailAsync();
        Db.AddSettlement(a.Id, 2026, 8, SettlementStatus.PendingReview);
        await Db.SaveChangesAsync();

        await NewService().SendReviewEmailAsync("https://app.test", isReminder: true);

        await _email.Received(1).SendSettlementReviewEmailAsync(
            Arg.Any<string>(), Arg.Any<IReadOnlyList<(DateOnly, int)>>(), Arg.Any<string>(), true);
    }

    [Fact]
    public async Task ReviewEmail_StaysQuietOnceEverythingIsConfirmed()
    {
        // This is what ends the weekly reminder.
        var (a, _) = await ArrangeTwoEmployeesWithNotificationEmailAsync();
        Db.AddSettlement(a.Id, 2026, 8, SettlementStatus.Settled);
        await Db.SaveChangesAsync();

        await NewService().SendReviewEmailAsync("https://app.test", isReminder: true);

        await _email.DidNotReceiveWithAnyArgs().SendSettlementReviewEmailAsync(default!, default!, default!, default);
    }

    [Fact]
    public async Task ReviewEmail_RespectsTheAppSettingsToggle()
    {
        var (a, _) = await ArrangeTwoEmployeesWithNotificationEmailAsync();
        Db.AddSettlement(a.Id, 2026, 8, SettlementStatus.PendingReview);
        (await Db.AppConfigurations.SingleAsync()).EnableSettlementEmails = false;
        await Db.SaveChangesAsync();

        await NewService().SendReviewEmailAsync("https://app.test", isReminder: false);

        await _email.DidNotReceiveWithAnyArgs().SendSettlementReviewEmailAsync(default!, default!, default!, default);
    }

    [Fact]
    public async Task ReviewEmail_NeedsANotificationAddress()
    {
        var a = Db.AddUser("Emma Employee");
        Db.AddConfiguration(notificationEmail: null);
        await Db.SaveChangesAsync();
        Db.AddSettlement(a.Id, 2026, 8, SettlementStatus.PendingReview);
        await Db.SaveChangesAsync();

        await NewService().SendReviewEmailAsync("https://app.test", isReminder: false);

        await _email.DidNotReceiveWithAnyArgs().SendSettlementReviewEmailAsync(default!, default!, default!, default);
    }

    [Fact]
    public async Task ReviewEmail_SwallowsSmtpFailures()
    {
        // A mail outage must not take the nightly job down with it.
        var (a, _) = await ArrangeTwoEmployeesWithNotificationEmailAsync();
        Db.AddSettlement(a.Id, 2026, 8, SettlementStatus.PendingReview);
        await Db.SaveChangesAsync();
        _email.SendSettlementReviewEmailAsync(default!, default!, default!, default)
            .ReturnsForAnyArgs(Task.FromException(new InvalidOperationException("SMTP down")));

        await NewService().SendReviewEmailAsync("https://app.test", isReminder: false);
    }
}
