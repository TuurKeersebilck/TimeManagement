using TimeManagementBackend.Models;
using TimeManagementBackend.Models.DTOs;

namespace TimeManagementBackend.Services;

public interface INotificationService
{
    Task NotifyAdminsAsync(string message, NotificationType type, CancellationToken ct = default);
    Task<IList<NotificationDto>> GetNotificationsAsync(string adminUserId, CancellationToken ct = default);
    Task<int> GetUnreadCountAsync(string adminUserId, CancellationToken ct = default);
    Task MarkAsReadAsync(int id, string adminUserId, CancellationToken ct = default);
    Task MarkAllAsReadAsync(string adminUserId, CancellationToken ct = default);
}
