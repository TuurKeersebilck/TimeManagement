using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
/// Time adjustment requests rewrite an employee's recorded day, and one route to approving them
/// is an emailed link that carries no session at all. These tests cover the snapshot rules that
/// keep a rewritten day coherent and the single-use, expiring nature of that token.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class TimeAdjustmentRequestServiceTests(PostgresFixture fixture) : DatabaseTestBase(fixture)
{
    private static readonly DateOnly Date = new(2026, 3, 2);
    private const string BaseUrl = "https://logr.example.test";

    private readonly IEmailService _email = Substitute.For<IEmailService>();
    private readonly INotificationService _notifications = Substitute.For<INotificationService>();

    private TimeAdjustmentRequestService NewService()
    {
        var context = NewContext();
        return new TimeAdjustmentRequestService(
            context, CreateUserManager(context), _email, _notifications,
            NullLogger<TimeAdjustmentRequestService>.Instance);
    }

    private static DateTimeOffset At(int hour, int minute = 0) =>
        new(Date.ToDateTime(new TimeOnly(hour, minute)), TimeSpan.Zero);

    private static DesiredDaySnapshotDto Snapshot(params SnapshotSessionDto[] sessions)
        => new() { Sessions = [.. sessions] };

    private static SnapshotSessionDto Session(
        DateTimeOffset clockIn, DateTimeOffset clockOut, int? id = null, params SnapshotBreakDto[] breaks)
        => new() { WorkSessionId = id, ClockIn = clockIn, ClockOut = clockOut, Breaks = [.. breaks] };

    private static SnapshotBreakDto Break(DateTimeOffset start, DateTimeOffset end, int? id = null)
        => new() { BreakRecordId = id, BreakStart = start, BreakEnd = end };

    private static string ToJson(DesiredDaySnapshotDto snapshot) =>
        JsonSerializer.Serialize(snapshot, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });

    private static string HashToken(string rawToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken))).ToLower();

    private async Task<User> ArrangeUserAsync()
    {
        var user = Db.AddUser();
        Db.AddConfiguration(notificationEmail: "admin@example.test");
        await Db.SaveChangesAsync();
        return user;
    }

    private async Task<TimeAdjustmentRequest> ArrangePendingRequestAsync(
        string userId, DesiredDaySnapshotDto snapshot, DateOnly? date = null)
    {
        var request = new TimeAdjustmentRequest
        {
            UserId = userId,
            Date = date ?? Date,
            DesiredDaySnapshot = ToJson(snapshot),
            Reason = "Forgot to clock out",
            Status = AdjustmentRequestStatus.Pending,
            ApprovalTokenHash = HashToken(Guid.NewGuid().ToString()),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
            RequestedAt = DateTimeOffset.UtcNow,
        };
        Db.TimeAdjustmentRequests.Add(request);
        await Db.SaveChangesAsync();
        return request;
    }

    // ── Creating a request ────────────────────────────────────────────────────

    [Fact]
    public async Task Create_StoresThePendingRequestAndHashesTheToken()
    {
        var user = await ArrangeUserAsync();

        await NewService().CreateRequestAsync(user.Id, new CreateAdjustmentRequestDto
        {
            Date = Date,
            Reason = "Forgot to clock out",
            DesiredDaySnapshot = Snapshot(Session(At(9), At(17))),
        }, BaseUrl);

        var saved = await NewContext().TimeAdjustmentRequests.SingleAsync();
        Assert.Equal(AdjustmentRequestStatus.Pending, saved.Status);
        Assert.False(saved.TokenUsed);
        Assert.Equal(64, saved.ApprovalTokenHash.Length); // SHA-256 as lowercase hex
        Assert.True(saved.ExpiresAt > DateTimeOffset.UtcNow.AddDays(29));
    }

    [Fact]
    public async Task Create_EmailsTheAdminAnApprovalLinkContainingTheRawToken()
    {
        var user = await ArrangeUserAsync();

        await NewService().CreateRequestAsync(user.Id, new CreateAdjustmentRequestDto
        {
            Date = Date,
            Reason = "Forgot to clock out",
            DesiredDaySnapshot = Snapshot(Session(At(9), At(17))),
        }, BaseUrl);

        var call = _email.ReceivedCalls().Single(c => c.GetMethodInfo().Name == nameof(IEmailService.SendAdjustmentRequestEmailAsync));
        var link = (string)call.GetArguments()[6]!;
        var rawToken = link.Split('/').Last();

        Assert.StartsWith($"{BaseUrl}/api/timeadjustmentrequests/approve/", link);
        // The raw token is only ever in the email; the database keeps its hash.
        var saved = await NewContext().TimeAdjustmentRequests.SingleAsync();
        Assert.Equal(saved.ApprovalTokenHash, HashToken(rawToken));
        Assert.DoesNotContain(rawToken, saved.ApprovalTokenHash);
    }

    [Fact]
    public async Task Create_SkipsTheEmailWhenTheToggleIsOff()
    {
        var user = Db.AddUser();
        Db.AddConfiguration(notificationEmail: "admin@example.test", enableAdjustmentRequestEmails: false);
        await Db.SaveChangesAsync();

        await NewService().CreateRequestAsync(user.Id, new CreateAdjustmentRequestDto
        {
            Date = Date, Reason = "r", DesiredDaySnapshot = Snapshot(Session(At(9), At(17))),
        }, BaseUrl);

        await _email.DidNotReceiveWithAnyArgs().SendAdjustmentRequestEmailAsync(
            default!, default!, default!, default, default!, default!, default!);
        Assert.Equal(1, await NewContext().TimeAdjustmentRequests.CountAsync());
    }

    [Fact]
    public async Task Create_StillSucceedsWhenTheEmailFails()
    {
        // Losing the notification must not cost the employee their request.
        var user = await ArrangeUserAsync();
        _email.SendAdjustmentRequestEmailAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateOnly>(),
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Throws(new InvalidOperationException("SMTP down"));

        await NewService().CreateRequestAsync(user.Id, new CreateAdjustmentRequestDto
        {
            Date = Date, Reason = "r", DesiredDaySnapshot = Snapshot(Session(At(9), At(17))),
        }, BaseUrl);

        Assert.Equal(1, await NewContext().TimeAdjustmentRequests.CountAsync());
    }

    [Fact]
    public async Task Create_NotifiesAdminsInApp()
    {
        var user = await ArrangeUserAsync();

        await NewService().CreateRequestAsync(user.Id, new CreateAdjustmentRequestDto
        {
            Date = Date, Reason = "r", DesiredDaySnapshot = Snapshot(Session(At(9), At(17))),
        }, BaseUrl);

        await _notifications.Received(1).NotifyAdminsAsync(
            Arg.Any<string>(), NotificationType.AdjustmentRequest, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_RefusesAFutureDate()
    {
        var user = await ArrangeUserAsync();
        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            NewService().CreateRequestAsync(user.Id, new CreateAdjustmentRequestDto
            {
                Date = tomorrow,
                Reason = "r",
                DesiredDaySnapshot = Snapshot(Session(At(9), At(17))),
            }, BaseUrl));

        Assert.Contains("future date", ex.Message);
    }

    [Fact]
    public async Task Create_RefusesAMonthThatHasBeenSettled()
    {
        // Reopening a settled month would silently change a balance that has already been paid.
        var user = await ArrangeUserAsync();
        Db.AddSettlement(user.Id, Date.Year, Date.Month, SettlementStatus.Settled);
        await Db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            NewService().CreateRequestAsync(user.Id, new CreateAdjustmentRequestDto
            {
                Date = Date, Reason = "r", DesiredDaySnapshot = Snapshot(Session(At(9), At(17))),
            }, BaseUrl));

        Assert.Contains("already been settled", ex.Message);
    }

    [Fact]
    public async Task Create_RefusesASecondPendingRequestForTheSameDay()
    {
        var user = await ArrangeUserAsync();
        await ArrangePendingRequestAsync(user.Id, Snapshot(Session(At(9), At(17))));

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            NewService().CreateRequestAsync(user.Id, new CreateAdjustmentRequestDto
            {
                Date = Date, Reason = "r", DesiredDaySnapshot = Snapshot(Session(At(9), At(17))),
            }, BaseUrl));

        Assert.Contains("pending adjustment request already exists", ex.Message);
    }

    // ── Snapshot validation ───────────────────────────────────────────────────

    private async Task AssertSnapshotRejectedAsync(DesiredDaySnapshotDto snapshot, string expectedFragment)
    {
        var user = await ArrangeUserAsync();

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            NewService().CreateRequestAsync(user.Id, new CreateAdjustmentRequestDto
            {
                Date = Date, Reason = "r", DesiredDaySnapshot = snapshot,
            }, BaseUrl));

        Assert.Contains(expectedFragment, ex.Message);
    }

    [Fact]
    public Task Snapshot_MustContainASession()
        => AssertSnapshotRejectedAsync(Snapshot(), "at least one session");

    [Fact]
    public Task Snapshot_RejectsASessionThatEndsBeforeItStarts()
        => AssertSnapshotRejectedAsync(Snapshot(Session(At(17), At(9))), "ClockIn must be before ClockOut");

    [Fact]
    public Task Snapshot_RejectsAZeroLengthSession()
        => AssertSnapshotRejectedAsync(Snapshot(Session(At(9), At(9))), "ClockIn must be before ClockOut");

    [Fact]
    public Task Snapshot_RejectsOverlappingSessions()
        => AssertSnapshotRejectedAsync(
            Snapshot(Session(At(9), At(13)), Session(At(12), At(17))), "overlap");

    [Fact]
    public Task Snapshot_AcceptsBackToBackSessions()
        => AssertSnapshotAcceptedAsync(Snapshot(Session(At(9), At(13)), Session(At(13), At(17))));

    [Fact]
    public Task Snapshot_RejectsABreakStartingBeforeTheSession()
        => AssertSnapshotRejectedAsync(
            Snapshot(Session(At(9), At(17), null, Break(At(8), At(9, 30)))),
            "starts before session ClockIn");

    [Fact]
    public Task Snapshot_RejectsABreakEndingAfterTheSession()
        => AssertSnapshotRejectedAsync(
            Snapshot(Session(At(9), At(17), null, Break(At(16, 30), At(18)))),
            "ends after session ClockOut");

    [Fact]
    public Task Snapshot_RejectsOverlappingBreaks()
        => AssertSnapshotRejectedAsync(
            Snapshot(Session(At(9), At(17), null, Break(At(12), At(13)), Break(At(12, 30), At(14)))),
            "overlap");

    [Fact]
    public Task Snapshot_RejectsABreakThatEndsBeforeItStarts()
        => AssertSnapshotRejectedAsync(
            Snapshot(Session(At(9), At(17), null, Break(At(13), At(12)))),
            "BreakStart must be before BreakEnd");

    [Fact]
    public async Task Snapshot_RejectsTimesInTheFuture()
    {
        var user = await ArrangeUserAsync();
        var future = DateTimeOffset.UtcNow.AddHours(2);

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            NewService().CreateRequestAsync(user.Id, new CreateAdjustmentRequestDto
            {
                Date = DateOnly.FromDateTime(DateTime.UtcNow),
                Reason = "r",
                DesiredDaySnapshot = Snapshot(Session(future.AddHours(-1), future)),
            }, BaseUrl));

        Assert.Contains("cannot be in the future", ex.Message);
    }

    private async Task AssertSnapshotAcceptedAsync(DesiredDaySnapshotDto snapshot)
    {
        var user = await ArrangeUserAsync();

        await NewService().CreateRequestAsync(user.Id, new CreateAdjustmentRequestDto
        {
            Date = Date, Reason = "r", DesiredDaySnapshot = snapshot,
        }, BaseUrl);

        Assert.Equal(1, await NewContext().TimeAdjustmentRequests.CountAsync());
    }

    // ── Approval by emailed token ─────────────────────────────────────────────

    private async Task<string> CreateAndCaptureTokenAsync(User user, DesiredDaySnapshotDto snapshot)
    {
        await NewService().CreateRequestAsync(user.Id, new CreateAdjustmentRequestDto
        {
            Date = Date, Reason = "Forgot to clock out", DesiredDaySnapshot = snapshot,
        }, BaseUrl);

        var call = _email.ReceivedCalls()
            .Last(c => c.GetMethodInfo().Name == nameof(IEmailService.SendAdjustmentRequestEmailAsync));
        return ((string)call.GetArguments()[6]!).Split('/').Last();
    }

    [Fact]
    public async Task Approve_ByToken_AppliesTheSnapshotAndConsumesTheToken()
    {
        var user = await ArrangeUserAsync();
        var token = await CreateAndCaptureTokenAsync(user, Snapshot(Session(At(9), At(17))));

        var message = await NewService().ApproveAsync(token);

        Assert.StartsWith("Approved.", message);
        var saved = await NewContext().TimeAdjustmentRequests.SingleAsync();
        Assert.Equal(AdjustmentRequestStatus.Approved, saved.Status);
        Assert.True(saved.TokenUsed);
        Assert.NotNull(saved.ReviewedAt);

        var session = await NewContext().WorkSessions.SingleAsync();
        Assert.Equal(At(9), session.ClockIn);
        Assert.Equal(At(17), session.ClockOut);
        Assert.Equal(WorkSessionStatus.Closed, session.Status);
    }

    [Fact]
    public async Task Approve_ByToken_CannotBeReplayed()
    {
        var user = await ArrangeUserAsync();
        var token = await CreateAndCaptureTokenAsync(user, Snapshot(Session(At(9), At(17))));
        await NewService().ApproveAsync(token);

        var message = await NewService().ApproveAsync(token);

        Assert.Equal("This approval link has already been used.", message);
        Assert.Equal(1, await NewContext().WorkSessions.CountAsync()); // not applied twice
    }

    [Fact]
    public async Task Approve_RejectsAnUnknownToken()
        => Assert.Equal("Invalid or unknown approval link.",
            await NewService().ApproveAsync("not-a-real-token"));

    [Fact]
    public async Task Approve_RejectsAnExpiredToken()
    {
        var user = await ArrangeUserAsync();
        var rawToken = Guid.NewGuid().ToString();
        Db.TimeAdjustmentRequests.Add(new TimeAdjustmentRequest
        {
            UserId = user.Id,
            Date = Date,
            DesiredDaySnapshot = ToJson(Snapshot(Session(At(9), At(17)))),
            Reason = "r",
            Status = AdjustmentRequestStatus.Pending,
            ApprovalTokenHash = HashToken(rawToken),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(-1),
            RequestedAt = DateTimeOffset.UtcNow.AddDays(-31),
        });
        await Db.SaveChangesAsync();

        Assert.Equal("This approval link has expired.", await NewService().ApproveAsync(rawToken));
        Assert.Empty(await NewContext().WorkSessions.ToListAsync());
    }

    [Fact]
    public async Task Approve_ByToken_AutoRejectsWhenTheMonthWasSettledInTheMeantime()
    {
        var user = await ArrangeUserAsync();
        var token = await CreateAndCaptureTokenAsync(user, Snapshot(Session(At(9), At(17))));
        Db.AddSettlement(user.Id, Date.Year, Date.Month, SettlementStatus.Settled);
        await Db.SaveChangesAsync();

        var message = await NewService().ApproveAsync(token);

        Assert.Contains("automatically rejected", message);
        var saved = await NewContext().TimeAdjustmentRequests.SingleAsync();
        Assert.Equal(AdjustmentRequestStatus.Rejected, saved.Status);
        Assert.True(saved.TokenUsed);
        Assert.Empty(await NewContext().WorkSessions.ToListAsync());
    }

    [Fact]
    public async Task Approve_ByToken_AutoRejectsAnUnreadableSnapshot()
    {
        var user = await ArrangeUserAsync();
        var rawToken = Guid.NewGuid().ToString();
        Db.TimeAdjustmentRequests.Add(new TimeAdjustmentRequest
        {
            UserId = user.Id,
            Date = Date,
            DesiredDaySnapshot = "{ this is not json",
            Reason = "r",
            Status = AdjustmentRequestStatus.Pending,
            ApprovalTokenHash = HashToken(rawToken),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
            RequestedAt = DateTimeOffset.UtcNow,
        });
        await Db.SaveChangesAsync();

        Assert.Contains("invalid snapshot", await NewService().ApproveAsync(rawToken));
    }

    // ── Approval and rejection by an admin ────────────────────────────────────

    [Fact]
    public async Task ApproveById_RecordsTheReviewingAdminAndNotifies()
    {
        var user = await ArrangeUserAsync();
        var admin = Db.AddUser("Adam Admin", UserRole.Admin);
        await Db.SaveChangesAsync();
        var request = await ArrangePendingRequestAsync(user.Id, Snapshot(Session(At(9), At(17))));

        await NewService().ApproveByIdAsync(request.Id, admin.Id);

        var saved = await NewContext().TimeAdjustmentRequests.SingleAsync();
        Assert.Equal(AdjustmentRequestStatus.Approved, saved.Status);
        Assert.Equal(admin.Id, saved.ReviewedByUserId);
        await _notifications.Received(1).NotifyUserAsync(
            user.Id, Arg.Any<string>(), NotificationType.AdjustmentApproved, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ApproveById_RejectsAnUnknownRequest()
    {
        var admin = Db.AddUser("Adam Admin", UserRole.Admin);
        await Db.SaveChangesAsync();

        await Assert.ThrowsAsync<ResourceNotFoundException>(() => NewService().ApproveByIdAsync(999, admin.Id));
    }

    [Fact]
    public async Task ApproveById_RefusesARequestThatIsNoLongerPending()
    {
        var user = await ArrangeUserAsync();
        var admin = Db.AddUser("Adam Admin", UserRole.Admin);
        await Db.SaveChangesAsync();
        var request = await ArrangePendingRequestAsync(user.Id, Snapshot(Session(At(9), At(17))));
        await NewService().ApproveByIdAsync(request.Id, admin.Id);

        await Assert.ThrowsAsync<ValidationException>(() => NewService().ApproveByIdAsync(request.Id, admin.Id));
    }

    [Fact]
    public async Task Reject_MarksTheRequestAndNotifiesTheEmployee()
    {
        var user = await ArrangeUserAsync();
        var admin = Db.AddUser("Adam Admin", UserRole.Admin);
        await Db.SaveChangesAsync();
        var request = await ArrangePendingRequestAsync(user.Id, Snapshot(Session(At(9), At(17))));

        await NewService().RejectAsync(request.Id, admin.Id);

        var saved = await NewContext().TimeAdjustmentRequests.SingleAsync();
        Assert.Equal(AdjustmentRequestStatus.Rejected, saved.Status);
        Assert.True(saved.TokenUsed); // the emailed link is dead once rejected
        Assert.Equal(admin.Id, saved.ReviewedByUserId);
        await _notifications.Received(1).NotifyUserAsync(
            user.Id, Arg.Any<string>(), NotificationType.AdjustmentRejected, Arg.Any<CancellationToken>());
        Assert.Empty(await NewContext().WorkSessions.ToListAsync());
    }

    // ── Reconciling the day ───────────────────────────────────────────────────

    [Fact]
    public async Task Approve_UpdatesAnExistingSessionWithoutTouchingItsServerStamps()
    {
        var user = await ArrangeUserAsync();
        var admin = Db.AddUser("Adam Admin", UserRole.Admin);
        await Db.SaveChangesAsync();
        var existing = Db.AddClosedSession(user.Id, Date, TimeSpan.FromHours(9), TimeSpan.FromHours(16));
        await Db.SaveChangesAsync();
        var originalStamp = existing.ClockInServerStamp;

        var request = await ArrangePendingRequestAsync(
            user.Id, Snapshot(Session(At(9), At(17), existing.Id)));
        await NewService().ApproveByIdAsync(request.Id, admin.Id);

        var saved = await NewContext().WorkSessions.SingleAsync();
        Assert.Equal(At(17), saved.ClockOut);
        // The audit trail of when the server actually saw the clock-in is immutable.
        Assert.Equal(originalStamp, saved.ClockInServerStamp);
    }

    [Fact]
    public async Task Approve_DeletesSessionsTheSnapshotOmits()
    {
        // A leftover session would keep blocking settlement for that month.
        var user = await ArrangeUserAsync();
        var admin = Db.AddUser("Adam Admin", UserRole.Admin);
        await Db.SaveChangesAsync();
        var keep = Db.AddClosedSession(user.Id, Date, TimeSpan.FromHours(9), TimeSpan.FromHours(12));
        Db.AddClosedSession(user.Id, Date, TimeSpan.FromHours(13), TimeSpan.FromHours(17));
        await Db.SaveChangesAsync();

        var request = await ArrangePendingRequestAsync(user.Id, Snapshot(Session(At(9), At(12), keep.Id)));
        await NewService().ApproveByIdAsync(request.Id, admin.Id);

        var remaining = Assert.Single(await NewContext().WorkSessions.ToListAsync());
        Assert.Equal(keep.Id, remaining.Id);
    }

    [Fact]
    public async Task Approve_CascadesTheDeleteToTheOrphanedBreaks()
    {
        var user = await ArrangeUserAsync();
        var admin = Db.AddUser("Adam Admin", UserRole.Admin);
        await Db.SaveChangesAsync();
        Db.AddClosedSession(user.Id, Date, TimeSpan.FromHours(9), TimeSpan.FromHours(17),
            aBreak: (new TimeSpan(12, 0, 0), new TimeSpan(12, 30, 0)));
        await Db.SaveChangesAsync();

        var request = await ArrangePendingRequestAsync(user.Id, Snapshot(Session(At(10), At(16))));
        await NewService().ApproveByIdAsync(request.Id, admin.Id);

        Assert.Empty(await NewContext().BreakRecords.ToListAsync());
        Assert.Single(await NewContext().WorkSessions.ToListAsync());
    }

    [Fact]
    public async Task Approve_AddsSessionsAndBreaksTheSnapshotIntroduces()
    {
        var user = await ArrangeUserAsync();
        var admin = Db.AddUser("Adam Admin", UserRole.Admin);
        await Db.SaveChangesAsync();

        var request = await ArrangePendingRequestAsync(user.Id, Snapshot(
            Session(At(9), At(12)),
            Session(At(13), At(17), null, Break(At(15), At(15, 15)))));
        await NewService().ApproveByIdAsync(request.Id, admin.Id);

        var context = NewContext();
        Assert.Equal(2, await context.WorkSessions.CountAsync());
        var record = Assert.Single(await context.BreakRecords.ToListAsync());
        Assert.Equal(At(15), record.BreakStart);
        Assert.Equal(At(15, 15), record.BreakEnd);
    }

    [Fact]
    public async Task Approve_LeavesOtherDaysUntouched()
    {
        var user = await ArrangeUserAsync();
        var admin = Db.AddUser("Adam Admin", UserRole.Admin);
        await Db.SaveChangesAsync();
        Db.AddClosedSession(user.Id, Date.AddDays(1), TimeSpan.FromHours(9), TimeSpan.FromHours(17));
        await Db.SaveChangesAsync();

        var request = await ArrangePendingRequestAsync(user.Id, Snapshot(Session(At(9), At(12))));
        await NewService().ApproveByIdAsync(request.Id, admin.Id);

        var sessions = await NewContext().WorkSessions.ToListAsync();
        Assert.Equal(2, sessions.Count);
        Assert.Contains(sessions, s => s.Date == Date.AddDays(1));
    }

    // ── Listing ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetUserRequests_ReturnsOnlyTheCallersOwn()
    {
        var user = await ArrangeUserAsync();
        var colleague = Db.AddUser("Colin Colleague");
        await Db.SaveChangesAsync();
        await ArrangePendingRequestAsync(user.Id, Snapshot(Session(At(9), At(17))));
        await ArrangePendingRequestAsync(colleague.Id, Snapshot(Session(At(9), At(17))));

        var mine = await NewService().GetUserRequestsAsync(user.Id);

        Assert.Equal(user.Id, Assert.Single(mine).UserId);
    }

    [Fact]
    public async Task GetAllRequests_IncludesEveryEmployeeNewestFirst()
    {
        var user = await ArrangeUserAsync();
        var colleague = Db.AddUser("Colin Colleague");
        await Db.SaveChangesAsync();
        await ArrangePendingRequestAsync(user.Id, Snapshot(Session(At(9), At(17))), Date);
        await Task.Delay(10);
        await ArrangePendingRequestAsync(colleague.Id, Snapshot(Session(At(9), At(17))), Date.AddDays(1));

        var all = (await NewService().GetAllRequestsAsync()).ToList();

        Assert.Equal(2, all.Count);
        Assert.True(all[0].RequestedAt >= all[1].RequestedAt);
        Assert.All(all, r => Assert.NotEmpty(r.EmployeeName));
    }
}
