using System.ComponentModel.DataAnnotations;
using TimeManagementBackend.Models;

namespace TimeManagementBackend.Models.DTOs;

public class SubmitClockEventDto
{
    [Required]
    public ClockEventType Type { get; set; }

    [Required]
    public TimeSpan RecordedTime { get; set; }

    /// <summary>
    /// Client's timezone offset in minutes as returned by JS Date.getTimezoneOffset().
    /// Negative for timezones ahead of UTC (e.g. UTC+2 = -120).
    /// Used to convert server UTC time to local time for the ±5 min validation.
    /// </summary>
    [Required]
    public int TimezoneOffsetMinutes { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }
}

public class ClockEventDto
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public ClockEventType Type { get; set; }
    public TimeSpan RecordedTime { get; set; }
    public string? Description { get; set; }
}

/// <summary>Aggregated daily view of all clock events for one user on one date.</summary>
public class DaySummaryDto
{
    public DateOnly Date { get; set; }
    public TimeSpan? ClockIn { get; set; }
    public TimeSpan? BreakStart { get; set; }
    public TimeSpan? BreakEnd { get; set; }
    public TimeSpan? ClockOut { get; set; }
    public double TotalHours { get; set; }
    public string? Description { get; set; }
    public bool IsComplete { get; set; }
}
