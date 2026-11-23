using Microsoft.EntityFrameworkCore;
using TimeManagementBackend.Models;
using TimeManagementBackend.Services;
using TimeManagementBackend.Tests.Infrastructure;

namespace TimeManagementBackend.Tests.Services;

[Collection(DatabaseCollection.Name)]
public class NotificationServiceTests(PostgresFixture fixture) : DatabaseTestBase(fixture)
{
    private NotificationService NewService() => new(NewContext());

    [Fact]
    public async Task NotifyAdmins_ReachesEveryAdminAndNoEmployee()
    {
        var firstAdmin = Db.AddUser("Adam Admin", UserRole.Admin);
        var secondAdmin = Db.AddUser("Ada Admin", UserRole.Admin);
        var employee = Db.AddUser("Emma Employee");
        await Db.SaveChangesAsync();

        await NewService().NotifyAdminsAsync("A request needs review", NotificationType.AdjustmentRequest);

        var recipients = await NewContext().Notifications.Select(n => n.RecipientUserId).ToListAsync();
        Assert.Equal(2, recipients.Count);
        Assert.Contains(firstAdmin.Id, recipients);
        Assert.Contains(secondAdmin.Id, recipients);
        Assert.DoesNotContain(employee.Id, recipients);
    }

    [Fact]
    public async Task NotifyAdmins_WithNoAdminsConfiguredIsHarmless()
    {
        Db.AddUser("Emma Employee");
        await Db.SaveChangesAsync();

        await NewService().NotifyAdminsAsync("nobody is listening", NotificationType.AdjustmentRequest);

        Assert.Empty(await NewContext().Notifications.ToListAsync());
    }

    [Fact]
    public async Task NotifyUser_CreatesAnUnreadNotification()
    {
        var user = Db.AddUser();
        await Db.SaveChangesAsync();

        await NewService().NotifyUserAsync(user.Id, "Your request was approved", NotificationType.AdjustmentApproved);

        var saved = await NewContext().Notifications.SingleAsync();
        Assert.Equal(user.Id, saved.RecipientUserId);
        Assert.Equal(NotificationType.AdjustmentApproved, saved.Type);
        Assert.False(saved.IsRead);
    }

    [Fact]
    public async Task GetNotifications_ReturnsOnlyTheRecipientsOwnNewestFirst()
    {
        var user = Db.AddUser();
        var colleague = Db.AddUser("Colin Colleague");
        await Db.SaveChangesAsync();
        var service = NewService();
        await service.NotifyUserAsync(user.Id, "older", NotificationType.Vacation);
        await Task.Delay(10);
        await service.NotifyUserAsync(user.Id, "newer", NotificationType.Vacation);
        await service.NotifyUserAsync(colleague.Id, "not yours", NotificationType.Vacation);

        var mine = await NewService().GetNotificationsAsync(user.Id);

        Assert.Equal(["newer", "older"], mine.Select(n => n.Message));
    }

    [Fact]
    public async Task UnreadCount_CountsOnlyUnreadOnesForThatUser()
    {
        var user = Db.AddUser();
        var colleague = Db.AddUser("Colin Colleague");
        await Db.SaveChangesAsync();
        var service = NewService();
        await service.NotifyUserAsync(user.Id, "one", NotificationType.Vacation);
        await service.NotifyUserAsync(user.Id, "two", NotificationType.Vacation);
        await service.NotifyUserAsync(colleague.Id, "theirs", NotificationType.Vacation);

        var first = (await NewService().GetNotificationsAsync(user.Id)).First();
        await NewService().MarkAsReadAsync(first.Id, user.Id);

        Assert.Equal(1, await NewService().GetUnreadCountAsync(user.Id));
        Assert.Equal(1, await NewService().GetUnreadCountAsync(colleague.Id));
    }

    [Fact]
    public async Task MarkAsRead_CannotTouchSomeoneElsesNotification()
    {
        var user = Db.AddUser();
        var intruder = Db.AddUser("Ivan Intruder");
        await Db.SaveChangesAsync();
        await NewService().NotifyUserAsync(user.Id, "private", NotificationType.Vacation);
        var notification = (await NewService().GetNotificationsAsync(user.Id)).Single();

        await NewService().MarkAsReadAsync(notification.Id, intruder.Id);

        Assert.False((await NewContext().Notifications.SingleAsync()).IsRead);
    }

    [Fact]
    public async Task MarkAsRead_IgnoresAnUnknownId()
    {
        var user = Db.AddUser();
        await Db.SaveChangesAsync();

        await NewService().MarkAsReadAsync(999, user.Id); // must not throw
    }

    [Fact]
    public async Task MarkAllAsRead_ClearsOnlyTheCallersNotifications()
    {
        var user = Db.AddUser();
        var colleague = Db.AddUser("Colin Colleague");
        await Db.SaveChangesAsync();
        var service = NewService();
        await service.NotifyUserAsync(user.Id, "one", NotificationType.Vacation);
        await service.NotifyUserAsync(user.Id, "two", NotificationType.Vacation);
        await service.NotifyUserAsync(colleague.Id, "theirs", NotificationType.Vacation);

        await NewService().MarkAllAsReadAsync(user.Id);

        Assert.Equal(0, await NewService().GetUnreadCountAsync(user.Id));
        Assert.Equal(1, await NewService().GetUnreadCountAsync(colleague.Id));
    }

    // ── Removing ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_RemovesOnlyTheCallersOwnNotification()
    {
        var user = Db.AddUser();
        var intruder = Db.AddUser("Ivan Intruder");
        await Db.SaveChangesAsync();
        await NewService().NotifyUserAsync(user.Id, "keep me", NotificationType.Vacation);
        await NewService().NotifyUserAsync(user.Id, "remove me", NotificationType.Vacation);
        var mine = await NewService().GetNotificationsAsync(user.Id);
        var target = mine.Single(n => n.Message == "remove me");

        await NewService().DeleteAsync(target.Id, intruder.Id); // someone else's: ignored
        Assert.Equal(2, await NewContext().Notifications.CountAsync());

        await NewService().DeleteAsync(target.Id, user.Id);
        await NewService().DeleteAsync(999, user.Id); // unknown: must not throw

        Assert.Equal(["keep me"], await NewContext().Notifications.Select(n => n.Message).ToListAsync());
    }

    [Fact]
    public async Task DeleteOlderThan_RemovesOnlyOldOnesForEveryone()
    {
        var user = Db.AddUser();
        var colleague = Db.AddUser("Colin Colleague");
        await Db.SaveChangesAsync();
        var now = DateTimeOffset.UtcNow;
        Db.Notifications.AddRange(
            new Notification { RecipientUserId = user.Id, Message = "ancient", Type = NotificationType.Vacation, CreatedAt = now.AddDays(-120) },
            new Notification { RecipientUserId = colleague.Id, Message = "ancient too", Type = NotificationType.Vacation, CreatedAt = now.AddDays(-91), IsRead = false },
            new Notification { RecipientUserId = user.Id, Message = "recent", Type = NotificationType.Vacation, CreatedAt = now.AddDays(-89) });
        await Db.SaveChangesAsync();

        var removed = await NewService().DeleteOlderThanAsync(now - NotificationService.RetentionPeriod);

        Assert.Equal(2, removed);
        Assert.Equal(["recent"], await NewContext().Notifications.Select(n => n.Message).ToListAsync());
    }

    [Fact]
    public async Task GetNotifications_ListsAtMostTheNewestHundred()
    {
        var user = Db.AddUser();
        await Db.SaveChangesAsync();
        var start = DateTimeOffset.UtcNow.AddDays(-10);
        Db.Notifications.AddRange(Enumerable.Range(1, NotificationService.MaxListed + 5).Select(i => new Notification
        {
            RecipientUserId = user.Id, Message = $"n{i}", Type = NotificationType.Vacation, CreatedAt = start.AddMinutes(i),
        }));
        await Db.SaveChangesAsync();

        var listed = await NewService().GetNotificationsAsync(user.Id);

        Assert.Equal(NotificationService.MaxListed, listed.Count);
        Assert.Equal($"n{NotificationService.MaxListed + 5}", listed[0].Message); // newest first
        Assert.DoesNotContain(listed, n => n.Message == "n5"); // the oldest ones are left out
    }
}
