using TimeManagementBackend.Models;
using TimeManagementBackend.Models.DTOs;

namespace TimeManagementBackend.Services;

public interface INotificationService
{
    Task NotifyAdminsAsync(string message, NotificationType type, CancellationToken ct = default);
    Task NotifyUserAsync(string userId, string message, NotificationType type, CancellationToken ct = default);
    Task<IList<NotificationDto>> GetNotificationsAsync(string adminUserId, CancellationToken ct = default);
    Task<int> GetUnreadCountAsync(string adminUserId, CancellationToken ct = default);
    Task MarkAsReadAsync(int id, string adminUserId, CancellationToken ct = default);
    Task MarkAllAsReadAsync(string adminUserId, CancellationToken ct = default);

    /// <summary>Removes one of the user's own notifications; someone else's or an unknown id is ignored.</summary>
    Task DeleteAsync(int id, string userId, CancellationToken ct = default);

    /// <summary>Deletes every notification created before <paramref name="cutoff"/>. Returns how many.</summary>
    Task<int> DeleteOlderThanAsync(DateTimeOffset cutoff, CancellationToken ct = default);
}
