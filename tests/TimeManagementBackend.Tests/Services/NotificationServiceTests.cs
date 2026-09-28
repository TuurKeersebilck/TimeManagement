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
}
