using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TimeManagementBackend.Models;

public class TimeAdjustmentRequest
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;

    [Required]
    public DateOnly Date { get; set; }

    public DateTimeOffset? RequestedClockIn { get; set; }
    public DateTimeOffset? RequestedBreakStart { get; set; }
    public DateTimeOffset? RequestedBreakEnd { get; set; }
    public DateTimeOffset? RequestedClockOut { get; set; }

    [Required]
    [MaxLength(2000)]
    public string Reason { get; set; } = string.Empty;

    public AdjustmentRequestStatus Status { get; set; } = AdjustmentRequestStatus.Pending;

    /// <summary>SHA-256 hash of the raw approval token sent in the admin email.</summary>
    [Required]
    [MaxLength(64)]
    public string ApprovalTokenHash { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; set; }

    public bool TokenUsed { get; set; }

    public DateTimeOffset RequestedAt { get; set; }

    public DateTimeOffset? ReviewedAt { get; set; }

    public string? ReviewedByUserId { get; set; }

    [ForeignKey(nameof(ReviewedByUserId))]
    public User? ReviewedByUser { get; set; }
}
