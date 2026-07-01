using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TimeManagementBackend.Models;

/// <summary>
/// Admin-created adjustment to an employee's flex balance.
/// Positive Hours = add time, negative Hours = deduct time.
/// Minimal schema for #216 calculation; extended with Type/Reason/audit fields in #218.
/// </summary>
public class TimeBankAdjustment
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;

    /// <summary>The month this adjustment is included in — determines which month's balance it affects.</summary>
    [Required]
    public DateOnly EffectiveDate { get; set; }

    /// <summary>Hours to add or subtract from the flex balance. Positive = bonus, negative = deduction.</summary>
    [Required]
    public decimal Hours { get; set; }
}
