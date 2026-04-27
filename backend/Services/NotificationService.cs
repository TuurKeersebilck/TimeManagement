using Microsoft.EntityFrameworkCore;
using TimeManagementBackend.Data;
using TimeManagementBackend.Models;
using TimeManagementBackend.Models.DTOs;

namespace TimeManagementBackend.Services;

public class NotificationService(AppDbContext db) : INotificationService
{
    private readonly AppDbContext _db = db;

    public async Task NotifyAdminsAsync(string message, NotificationType type, CancellationToken ct = default)
    {
        var adminIds = await _db.Users
            .Where(u => u.Role == UserRole.Admin)
            .Select(u => u.Id)
            .ToListAsync(ct);

        var notifications = adminIds.Select(id => new Notification
        {
            RecipientUserId = id,
            Message = message,
            Type = type,
            IsRead = false,
            CreatedAt = DateTimeOffset.UtcNow,
        }).ToList();

        _db.Notifications.AddRange(notifications);
        await _db.SaveChangesAsync(ct);
    }

    public async Task NotifyUserAsync(string userId, string message, NotificationType type, CancellationToken ct = default)
    {
        _db.Notifications.Add(new Notification
        {
            RecipientUserId = userId,
            Message = message,
            Type = type,
            IsRead = false,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IList<NotificationDto>> GetNotificationsAsync(string adminUserId, CancellationToken ct = default)
    {
        return await _db.Notifications
            .Where(n => n.RecipientUserId == adminUserId)
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => new NotificationDto
            {
                Id = n.Id,
                Message = n.Message,
                Type = n.Type,
                IsRead = n.IsRead,
                CreatedAt = n.CreatedAt,
            })
            .ToListAsync(ct);
    }

    public Task<int> GetUnreadCountAsync(string adminUserId, CancellationToken ct = default)
        => _db.Notifications.CountAsync(n => n.RecipientUserId == adminUserId && !n.IsRead, ct);

    public async Task MarkAsReadAsync(int id, string adminUserId, CancellationToken ct = default)
    {
        var notification = await _db.Notifications
            .FirstOrDefaultAsync(n => n.Id == id && n.RecipientUserId == adminUserId, ct);

        if (notification is null) return;

        notification.IsRead = true;
        await _db.SaveChangesAsync(ct);
    }

    public async Task MarkAllAsReadAsync(string adminUserId, CancellationToken ct = default)
    {
        await _db.Notifications
            .Where(n => n.RecipientUserId == adminUserId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true), ct);
    }
}
