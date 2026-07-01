using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TimeManagementBackend.Models;

/// <summary>Per-weekday working hours target. UserId = null means the global default.</summary>
public class WorkdayTarget
{
    [Key]
    public int Id { get; set; }

    /// <summary>Null = global default row. Non-null = per-employee override.</summary>
    public string? UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }

    [Required]
    public DayOfWeek DayOfWeek { get; set; }

    /// <summary>Target hours for this weekday. 0 = non-working day.</summary>
    [Required]
    public decimal Hours { get; set; }
}
