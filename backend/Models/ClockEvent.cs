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

    /// <summary>UTC date derived from RecordedAt — kept as a stored column for efficient date-range queries and indexes.</summary>
    [Required]
    public DateOnly Date { get; set; }

    [Required]
    public ClockEventType Type { get; set; }

    /// <summary>Server-recorded UTC timestamp when the clock button was pressed.</summary>
    [Required]
    public DateTimeOffset ActualAt { get; set; }

    /// <summary>User-adjusted UTC timestamp, validated to be within ±5 minutes of ActualAt.</summary>
    [Required]
    public DateTimeOffset RecordedAt { get; set; }

    /// <summary>One description per day, stored on the ClockOut event.</summary>
    [MaxLength(1000)]
    public string? Description { get; set; }
}
