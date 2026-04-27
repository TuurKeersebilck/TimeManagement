namespace TimeManagementBackend.Models;

public enum NotificationType
{
    Vacation,
    AdjustmentRequest,
    AdjustmentApproved,
    AdjustmentRejected,
}

public class Notification
{
    public int Id { get; set; }
    public string RecipientUserId { get; set; } = string.Empty;
    public User RecipientUser { get; set; } = null!;
    public string Message { get; set; } = string.Empty;
    public NotificationType Type { get; set; }
    public bool IsRead { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
