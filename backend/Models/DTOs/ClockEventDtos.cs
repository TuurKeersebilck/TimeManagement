using System.ComponentModel.DataAnnotations;
using TimeManagementBackend.Models;

namespace TimeManagementBackend.Models.DTOs;

public class SubmitClockEventDto
{
    [Required]
    public ClockEventType Type { get; set; }

    /// <summary>UTC timestamp selected by the user (ISO 8601). Must be within ±5 minutes of server UTC now.</summary>
    [Required]
    public DateTimeOffset RecordedAt { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }
}

public class ClockEventDto
{
    public int Id { get; set; }
    public ClockEventType Type { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
    public string? Description { get; set; }
}

/// <summary>Aggregated daily view of all clock events for one user on one date.</summary>
public class DaySummaryDto
{
    public DateOnly Date { get; set; }
    public DateTimeOffset? ClockIn { get; set; }
    public DateTimeOffset? BreakStart { get; set; }
    public DateTimeOffset? BreakEnd { get; set; }
    public DateTimeOffset? ClockOut { get; set; }
    public double TotalHours { get; set; }
    public string? Description { get; set; }
    public bool IsComplete { get; set; }
}
