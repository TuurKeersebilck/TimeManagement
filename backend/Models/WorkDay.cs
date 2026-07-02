using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TimeManagementBackend.Models;

/// <summary>Per-day employee metadata. Upserted on ClockIn (WorkedFromHome) and ClockOut (Description). Directly employee-editable.</summary>
public class WorkDay
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;

    /// <summary>UTC calendar date matching WorkSession.Date for the same day.</summary>
    [Required]
    public DateOnly Date { get; set; }

    public bool WorkedFromHome { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }
}
