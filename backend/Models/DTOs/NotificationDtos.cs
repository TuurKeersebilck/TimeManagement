using TimeManagementBackend.Models;

namespace TimeManagementBackend.Models.DTOs;

public class NotificationDto
{
    public int Id { get; set; }
    public string Message { get; set; } = string.Empty;
    public NotificationType Type { get; set; }
    public bool IsRead { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class UnreadCountDto
{
    public int Count { get; set; }
}
