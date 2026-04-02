using System.ComponentModel.DataAnnotations;
using TimeManagementBackend.Helpers;

namespace TimeManagementBackend.Models.DTOs;

public class TimeLogDto
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public TimeSpan? BreakStart { get; set; }
    public TimeSpan? BreakEnd { get; set; }
    public string? Description { get; set; }

    public double TotalHours =>
        TimeCalculationHelper.CalculateWorkedHours(StartTime, EndTime, BreakStart, BreakEnd);
}

public class TimeLogFormDto
{
    [Required]
    public DateOnly Date { get; set; }

    [Required]
    public TimeSpan StartTime { get; set; }

    [Required]
    public TimeSpan EndTime { get; set; }

    public TimeSpan? BreakStart { get; set; }

    public TimeSpan? BreakEnd { get; set; }

    public string? Description { get; set; }
}

