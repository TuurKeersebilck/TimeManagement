using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TimeManagementBackend.Models;

/// <summary>
/// Admin-created adjustment to an employee's flex balance.
/// Positive Hours = add time, negative Hours = deduct time.
/// </summary>
public class TimeBankAdjustment
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;

    /// <summary>The month this adjustment belongs to — determines which month's running balance is affected.</summary>
    [Required]
    public DateOnly EffectiveDate { get; set; }

    /// <summary>Hours to add (positive) or deduct (negative) from the flex balance.</summary>
    [Required]
    public decimal Hours { get; set; }

    [Required]
    [MaxLength(2000)]
    public string Reason { get; set; } = string.Empty;

    /// <summary>Admin who created the adjustment. Nullable so deleting the admin doesn't cascade-delete adjustments.</summary>
    public string? CreatedByUserId { get; set; }

    [ForeignKey(nameof(CreatedByUserId))]
    public User? CreatedByUser { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
