using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TimeManagementBackend.Models;

public class ClockEvent
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;

    [Required]
    public DateOnly Date { get; set; }

    [Required]
    public ClockEventType Type { get; set; }

    /// <summary>Server-recorded time when the clock button was pressed.</summary>
    [Required]
    public TimeSpan ActualTime { get; set; }

    /// <summary>User-adjusted time, validated to be within ±5 minutes of ActualTime.</summary>
    [Required]
    public TimeSpan RecordedTime { get; set; }

    /// <summary>One description per day, stored on the ClockOut event.</summary>
    [MaxLength(1000)]
    public string? Description { get; set; }
}
