using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TimeManagementBackend.Exceptions;
using TimeManagementBackend.Models;
using TimeManagementBackend.Models.DTOs;
using TimeManagementBackend.Services;
using TimeManagementBackend.Tests.Infrastructure;

namespace TimeManagementBackend.Tests.Services;

/// <summary>
/// Vacation requests. Employees plan drafts and submit them together, so the admin gets one email
/// per submit. Nothing is booked until an admin approves, yet drafts and pending requests must
/// already hold their days against the balance, or an employee could ask for the same days twice.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class VacationRequestServiceTests(PostgresFixture fixture) : DatabaseTestBase(fixture)
{
    private static readonly DateOnly Monday = new(2026, 3, 2);
    private static readonly DateOnly Friday = new(2026, 3, 6);
    private static readonly DateOnly Sunday = new(2026, 3, 8);
    private static readonly DateOnly Saturday = new(2026, 3, 7);

    private readonly IEmailService _email = Substitute.For<IEmailService>();
    private readonly INotificationService _notifications = Substitute.For<INotificationService>();

    private VacationRequestService NewService()
    {
        var context = NewContext();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["AppUrl"] = "https://logr.example.test" })
            .Build();
        return new VacationRequestService(
            context, new VacationService(context, Mapper), _email, _notifications, configuration,
            NullLogger<VacationRequestService>.Instance);
    }

    private async Task<(User Employee, User Admin, VacationType Type)> ArrangeAsync(
        decimal yearlyBalance = 20m, bool enableEmails = true)
    {
        var employee = Db.AddUser("Emma Employee");
        var admin = Db.AddUser("Alex Admin", UserRole.Admin);
        var type = Db.AddVacationType();
        var config = Db.AddConfiguration(notificationEmail: "admin@example.test");
        config.EnableVacationRequestEmails = enableEmails;
        await Db.SaveChangesAsync();
        Db.AddBalance(employee.Id, type, yearlyBalance);
        await Db.SaveChangesAsync();
        return (employee, admin, type);
    }

    /// <summary>Plans a draft and submits it straight away; returns the now-pending request.</summary>
    private async Task<VacationRequestDto> RequestAsync(string userId, CreateVacationRequestDto dto)
    {
        var draft = await NewService().CreateAsync(userId, dto);
        var submitted = await NewService().SubmitAsync(userId);
        return submitted.Single(r => r.Id == draft.Id);
    }

    private static CreateVacationRequestDto Request(VacationType type, DateOnly start, DateOnly end, decimal amount = 1.0m)
        => new() { VacationTypeId = type.Id, StartDate = start, EndDate = end, Amount = amount };

    // ── Drafts and submitting ─────────────────────────────────────────────────

    [Fact]
    public async Task Create_StoresADraftAndTellsNobody()
    {
        var (employee, _, type) = await ArrangeAsync();
        Db.AddHoliday(new DateOnly(2026, 3, 4));
        await Db.SaveChangesAsync();

        var dto = await NewService().CreateAsync(employee.Id, Request(type, Monday, Sunday));

        Assert.Equal(VacationRequestStatus.Draft, dto.Status);
        Assert.Equal(4m, dto.TotalDays); // Mon–Fri minus the Wednesday holiday; weekend skipped
        Assert.Equal("Emma Employee", dto.EmployeeName);
        Assert.False(await NewContext().VacationDays.AnyAsync());
        Assert.Empty(await NewService().GetAllAsync()); // the admin doesn't see drafts
        Assert.Empty(_email.ReceivedCalls());
        Assert.Empty(_notifications.ReceivedCalls());
    }

    [Fact]
    public async Task Submit_SendsEveryDraftInOneEmailWithALinkIntoTheApp()
    {
        var (employee, _, type) = await ArrangeAsync();
        await NewService().CreateAsync(employee.Id, Request(type, new DateOnly(2026, 3, 16), new DateOnly(2026, 3, 16)));
        await NewService().CreateAsync(employee.Id, Request(type, Monday, Friday));

        var submitted = (await NewService().SubmitAsync(employee.Id)).ToList();

        Assert.All(submitted, r => Assert.Equal(VacationRequestStatus.Pending, r.Status));
        Assert.Equal([Monday, new DateOnly(2026, 3, 16)], submitted.Select(r => r.StartDate));
        await _email.Received(1).SendVacationRequestEmailAsync(
            "admin@example.test", "Emma Employee",
            Arg.Is<IReadOnlyList<VacationRequestEmailItem>>(items =>
                items.Count == 2 && items[0].StartDate == Monday && items[0].TotalDays == 5m),
            "https://logr.example.test/admin/requests");
        await _notifications.Received(1).NotifyAdminsAsync(
            Arg.Is<string>(m => m.Contains("Emma Employee") && m.Contains("2 vacation periods")),
            NotificationType.VacationRequest, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Submit_WithoutDraftsFails()
    {
        var (employee, _, _) = await ArrangeAsync();

        await Assert.ThrowsAsync<ValidationException>(() => NewService().SubmitAsync(employee.Id));
    }

    [Fact]
    public async Task Submit_SendsNoEmailWhenTheToggleIsOff()
    {
        var (employee, _, type) = await ArrangeAsync(enableEmails: false);

        await RequestAsync(employee.Id, Request(type, Monday, Friday));

        await _email.DidNotReceiveWithAnyArgs().SendVacationRequestEmailAsync(default!, default!, default!, default!);
    }

    [Fact]
    public async Task Submit_ChecksTheBalanceAgainAndLeavesTheDraftsWhenItFails()
    {
        var (employee, _, type) = await ArrangeAsync(yearlyBalance: 5m);
        await NewService().CreateAsync(employee.Id, Request(type, Monday, Friday));

        // The admin lowered the allowance after the days were planned.
        var balance = await Db.EmployeeVacationBalances.SingleAsync();
        balance.YearlyBalance = 3m;
        await Db.SaveChangesAsync();

        await Assert.ThrowsAsync<InsufficientVacationBalanceException>(() => NewService().SubmitAsync(employee.Id));
        Assert.Equal(VacationRequestStatus.Draft, (await NewContext().VacationRequests.SingleAsync()).Status);
        Assert.Empty(_email.ReceivedCalls());
    }

    [Fact]
    public async Task DeleteDraft_RemovesItButNotASubmittedRequest()
    {
        var (employee, _, type) = await ArrangeAsync();
        var other = Db.AddUser("Olly Other");
        await Db.SaveChangesAsync();
        var draft = await NewService().CreateAsync(employee.Id, Request(type, Monday, Friday));

        await Assert.ThrowsAsync<ResourceNotFoundException>(() => NewService().DeleteDraftAsync(other.Id, draft.Id));
        await NewService().DeleteDraftAsync(employee.Id, draft.Id);
        Assert.False(await NewContext().VacationRequests.AnyAsync());

        var sent = await RequestAsync(employee.Id, Request(type, Monday, Friday));
        await Assert.ThrowsAsync<ValidationException>(() => NewService().DeleteDraftAsync(employee.Id, sent.Id));
    }

    [Fact]
    public async Task Approve_CannotReachADraft()
    {
        var (employee, admin, type) = await ArrangeAsync();
        var draft = await NewService().CreateAsync(employee.Id, Request(type, Monday, Friday));

        await Assert.ThrowsAsync<ResourceNotFoundException>(() => NewService().ApproveAsync(draft.Id, admin.Id));
    }

    [Fact]
    public async Task Create_DraftsHoldTheirDaysAgainstTheBalance()
    {
        var (employee, _, type) = await ArrangeAsync(yearlyBalance: 7m);
        var service = NewService();

        await service.CreateAsync(employee.Id, Request(type, Monday, Friday)); // 5 days, not sent

        var ex = await Assert.ThrowsAsync<InsufficientVacationBalanceException>(() =>
            service.CreateAsync(employee.Id, Request(type, new DateOnly(2026, 3, 9), new DateOnly(2026, 3, 11))));
        Assert.Contains("already planned or requested", ex.Message);
    }

    [Fact]
    public async Task Create_PendingRequestsHoldTheirDaysAgainstTheBalance()
    {
        var (employee, _, type) = await ArrangeAsync(yearlyBalance: 7m);

        await RequestAsync(employee.Id, Request(type, Monday, Friday)); // 5 days held

        var ex = await Assert.ThrowsAsync<InsufficientVacationBalanceException>(() =>
            NewService().CreateAsync(employee.Id, Request(type, new DateOnly(2026, 3, 9), new DateOnly(2026, 3, 11))));
        Assert.Contains("already planned or requested", ex.Message);
    }

    [Fact]
    public async Task Create_RejectsDatesAlreadyPlannedOrRequestedForTheSameType()
    {
        var (employee, _, type) = await ArrangeAsync();
        await RequestAsync(employee.Id, Request(type, Friday, Friday)); // submitting sends every draft,
        await NewService().CreateAsync(employee.Id, Request(type, Monday, Tuesday())); // so draft this after

        var draftOverlap = await Assert.ThrowsAsync<ValidationException>(() =>
            NewService().CreateAsync(employee.Id, Request(type, Tuesday(), Tuesday())));
        Assert.Contains("Remove that draft", draftOverlap.Message);

        var pendingOverlap = await Assert.ThrowsAsync<ValidationException>(() =>
            NewService().CreateAsync(employee.Id, Request(type, Friday, Friday)));
        Assert.Contains("pending request", pendingOverlap.Message);
    }

    private static DateOnly Tuesday() => Monday.AddDays(1);

    [Fact]
    public async Task Create_RejectsARangeWithoutWorkingDays()
    {
        var (employee, _, type) = await ArrangeAsync();

        await Assert.ThrowsAsync<ValidationException>(() =>
            NewService().CreateAsync(employee.Id, Request(type, Saturday, Sunday)));
    }

    [Fact]
    public async Task Create_RejectsAnAdminOnlyType()
    {
        var (employee, _, _) = await ArrangeAsync();
        var sick = Db.AddVacationType("Sick Leave", isUnlimited: true, isAdminOnly: true);
        await Db.SaveChangesAsync();

        await Assert.ThrowsAsync<ValidationException>(() =>
            NewService().CreateAsync(employee.Id, Request(sick, Monday, Monday)));
    }

    [Fact]
    public async Task Balances_ShowPendingAndDraftDaysSeparatelyFromUsedDays()
    {
        var (employee, _, type) = await ArrangeAsync();
        await RequestAsync(employee.Id, Request(type, Monday, Friday, amount: 0.5m));
        await NewService().CreateAsync(employee.Id, Request(type, new DateOnly(2026, 3, 16), new DateOnly(2026, 3, 17)));

        var balance = (await new VacationService(NewContext(), Mapper).GetMyBalancesAsync(employee.Id, 2026)).Single();

        Assert.Equal(0m, balance.UsedDays);
        Assert.Equal(2.5m, balance.PendingDays);
        Assert.Equal(2m, balance.DraftDays);
        Assert.Equal(20m, balance.RemainingDays);
    }

    // ── Reviewing ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Approve_BooksTheDaysAndTellsTheEmployee()
    {
        var (employee, admin, type) = await ArrangeAsync();
        var request = await RequestAsync(employee.Id, Request(type, Monday, Sunday));

        var result = await NewService().ApproveAsync(request.Id, admin.Id);

        Assert.Equal(5, result.Created.Count());
        var stored = await NewContext().VacationRequests.SingleAsync();
        Assert.Equal(VacationRequestStatus.Approved, stored.Status);
        Assert.Equal(admin.Id, stored.ReviewedByUserId);
        Assert.Equal(5, await NewContext().VacationDays.CountAsync(d => d.UserId == employee.Id));
        await _notifications.Received(1).NotifyUserAsync(
            employee.Id, Arg.Any<string>(), NotificationType.VacationApproved, Arg.Any<CancellationToken>());
        await _email.Received(1).SendVacationRequestOutcomeEmailAsync(
            employee.Email!, "Emma Employee", "Annual Leave", Monday, Sunday, true, null);
    }

    [Fact]
    public async Task Approve_ASecondTimeFails()
    {
        var (employee, admin, type) = await ArrangeAsync();
        var request = await RequestAsync(employee.Id, Request(type, Monday, Friday));
        await NewService().ApproveAsync(request.Id, admin.Id);

        await Assert.ThrowsAsync<ValidationException>(() => NewService().ApproveAsync(request.Id, admin.Id));
        Assert.Equal(5, await NewContext().VacationDays.CountAsync());
    }

    [Fact]
    public async Task Approve_StaysPendingWhenBookingFails()
    {
        var (employee, admin, type) = await ArrangeAsync(yearlyBalance: 5m);
        var request = await RequestAsync(employee.Id, Request(type, Monday, Friday));

        // The admin lowered the allowance after the request came in.
        var balance = await Db.EmployeeVacationBalances.SingleAsync();
        balance.YearlyBalance = 2m;
        await Db.SaveChangesAsync();

        await Assert.ThrowsAsync<InsufficientVacationBalanceException>(() =>
            NewService().ApproveAsync(request.Id, admin.Id));

        var stored = await NewContext().VacationRequests.SingleAsync();
        Assert.Equal(VacationRequestStatus.Pending, stored.Status);
        Assert.Null(stored.ReviewedByUserId);
        Assert.False(await NewContext().VacationDays.AnyAsync());
    }

    [Fact]
    public async Task Reject_KeepsTheReasonAndBooksNothing()
    {
        var (employee, admin, type) = await ArrangeAsync();
        var request = await RequestAsync(employee.Id, Request(type, Monday, Friday));

        await NewService().RejectAsync(request.Id, admin.Id, "  Busy week, sorry  ");

        var stored = await NewContext().VacationRequests.SingleAsync();
        Assert.Equal(VacationRequestStatus.Rejected, stored.Status);
        Assert.Equal("Busy week, sorry", stored.ReviewNote);
        Assert.False(await NewContext().VacationDays.AnyAsync());
        await _email.Received(1).SendVacationRequestOutcomeEmailAsync(
            employee.Email!, "Emma Employee", "Annual Leave", Monday, Friday, false, "Busy week, sorry");
    }

    [Fact]
    public async Task Cancel_WithdrawsAPendingRequestAndFreesTheBalance()
    {
        var (employee, _, type) = await ArrangeAsync(yearlyBalance: 5m);
        var request = await RequestAsync(employee.Id, Request(type, Monday, Friday));

        await NewService().CancelAsync(employee.Id, request.Id);

        Assert.Equal(VacationRequestStatus.Cancelled, (await NewContext().VacationRequests.SingleAsync()).Status);
        // The same five days can be planned again.
        await NewService().CreateAsync(employee.Id, Request(type, Monday, Friday));
    }

    [Fact]
    public async Task Cancel_IsOnlyForTheEmployeesOwnPendingRequests()
    {
        var (employee, admin, type) = await ArrangeAsync();
        var other = Db.AddUser("Olly Other");
        await Db.SaveChangesAsync();
        var request = await RequestAsync(employee.Id, Request(type, Monday, Friday));

        await Assert.ThrowsAsync<ResourceNotFoundException>(() => NewService().CancelAsync(other.Id, request.Id));

        await NewService().ApproveAsync(request.Id, admin.Id);
        await Assert.ThrowsAsync<ValidationException>(() => NewService().CancelAsync(employee.Id, request.Id));
    }

    [Fact]
    public async Task GetAll_ShowsTheBalanceAfterApprovalAndWhoElseIsOff()
    {
        var (employee, _, type) = await ArrangeAsync(yearlyBalance: 20m);
        var colleague = Db.AddUser("Chris Colleague");
        var asking = Db.AddUser("Pat Pending");
        await Db.SaveChangesAsync();
        Db.AddBalance(asking.Id, type, 20m);
        Db.AddVacationDay(colleague.Id, type, new DateOnly(2026, 3, 3));
        Db.AddVacationDay(employee.Id, type, new DateOnly(2026, 2, 2)); // 1 day already used
        await Db.SaveChangesAsync();

        var request = await RequestAsync(employee.Id, Request(type, Monday, Friday));
        await RequestAsync(asking.Id, Request(type, Friday, new DateOnly(2026, 3, 10)));

        var all = await NewService().GetAllAsync();
        var mine = all.Single(r => r.Id == request.Id);

        Assert.Equal(14m, mine.Balance!.RemainingAfterApproval); // 20 − 1 used − 5 requested
        Assert.Collection(mine.Overlaps!.OrderBy(o => o.EmployeeName),
            o =>
            {
                Assert.Equal("Chris Colleague", o.EmployeeName);
                Assert.False(o.IsPending);
                Assert.Equal([new DateOnly(2026, 3, 3)], o.Dates);
            },
            o =>
            {
                Assert.Equal("Pat Pending", o.EmployeeName);
                Assert.True(o.IsPending);
                Assert.Equal([Friday], o.Dates);
            });
    }

    // ── The admin side behaves as it did before requests existed ──────────────

    [Fact]
    public async Task AdminBooking_ForAnEmployee_IgnoresTheirDraftsAndPendingRequests()
    {
        var (employee, _, type) = await ArrangeAsync(yearlyBalance: 7m);
        await RequestAsync(employee.Id, Request(type, Monday, Friday)); // 5 days pending
        await NewService().CreateAsync(employee.Id, Request(type, new DateOnly(2026, 3, 9), new DateOnly(2026, 3, 9))); // 1 drafted

        // Same balance rule as always for an admin booking: only booked days count, so 3 more fit
        // in the 7-day balance even though 6 are held by the employee's own requests.
        var result = await new VacationService(NewContext(), Mapper).CreateVacationRangeAsync(employee.Id, new CreateVacationRangeDto
        {
            VacationTypeId = type.Id, StartDate = new DateOnly(2026, 3, 16), EndDate = new DateOnly(2026, 3, 18), Amount = 1m,
        }, asAdmin: true);

        Assert.Equal(3, result.Created.Count());
        Assert.Equal(2, await NewContext().VacationRequests.CountAsync(r => r.Status != VacationRequestStatus.Approved));
    }

    [Fact]
    public async Task Approve_SkipsADayTheAdminAlreadyBookedDirectly()
    {
        var (employee, admin, type) = await ArrangeAsync();
        var request = await RequestAsync(employee.Id, Request(type, Monday, Friday));
        Db.AddVacationDay(employee.Id, await Db.VacationTypes.SingleAsync(), new DateOnly(2026, 3, 4));
        await Db.SaveChangesAsync();

        var result = await NewService().ApproveAsync(request.Id, admin.Id);

        Assert.Equal(4, result.Created.Count());
        Assert.Equal(1, result.SkippedExisting);
        Assert.Equal(5, await NewContext().VacationDays.CountAsync(d => d.UserId == employee.Id));
    }

    [Fact]
    public async Task Balances_InTheAdminsViewOfAnEmployee_HaveNoRequestCounts()
    {
        var (employee, _, type) = await ArrangeAsync();
        await RequestAsync(employee.Id, Request(type, Monday, Friday));
        await NewService().CreateAsync(employee.Id, Request(type, new DateOnly(2026, 3, 16), new DateOnly(2026, 3, 16)));

        var balance = (await new VacationService(NewContext(), Mapper)
            .GetMyBalancesAsync(employee.Id, 2026, includeRequests: false)).Single();

        Assert.Equal(0m, balance.PendingDays);
        Assert.Equal(0m, balance.DraftDays);
        Assert.Equal(20m, balance.RemainingDays);
    }
}
