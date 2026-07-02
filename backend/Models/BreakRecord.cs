using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TimeManagementBackend.Models;

public class BreakRecord
{
    [Key]
    public int Id { get; set; }

    public int WorkSessionId { get; set; }

    [ForeignKey(nameof(WorkSessionId))]
    public WorkSession WorkSession { get; set; } = null!;

    /// <summary>Effective break-start time.</summary>
    [Required]
    public DateTimeOffset BreakStart { get; set; }

    /// <summary>Server's UtcNow at the moment of start-break action. Immutable.</summary>
    [Required]
    public DateTimeOffset BreakStartServerStamp { get; set; }

    /// <summary>Effective break-end time. Null while break is ongoing.</summary>
    public DateTimeOffset? BreakEnd { get; set; }

    /// <summary>Server's UtcNow at the moment of end-break action. Immutable. Null while break is ongoing.</summary>
    public DateTimeOffset? BreakEndServerStamp { get; set; }
}
