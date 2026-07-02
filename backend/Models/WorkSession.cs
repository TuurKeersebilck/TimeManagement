using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TimeManagementBackend.Models;

public class WorkSession
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;

    /// <summary>UTC calendar date derived server-side from ClockIn. Never client-supplied.</summary>
    [Required]
    public DateOnly Date { get; set; }

    /// <summary>Effective clock-in time (user-adjustable within ±5 min of ClockInServerStamp).</summary>
    [Required]
    public DateTimeOffset ClockIn { get; set; }

    /// <summary>Server's UtcNow at the moment of clock-in action. Immutable.</summary>
    [Required]
    public DateTimeOffset ClockInServerStamp { get; set; }

    /// <summary>Effective clock-out time. Null while session is open.</summary>
    public DateTimeOffset? ClockOut { get; set; }

    /// <summary>Server's UtcNow at the moment of clock-out action. Immutable. Null while session is open.</summary>
    public DateTimeOffset? ClockOutServerStamp { get; set; }

    [Required]
    public WorkSessionStatus Status { get; set; } = WorkSessionStatus.Open;

    public ICollection<BreakRecord> Breaks { get; set; } = new List<BreakRecord>();
}
