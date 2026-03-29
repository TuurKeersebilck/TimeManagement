using System.ComponentModel.DataAnnotations;

namespace TimeManagementBackend.Models.DTOs;

public class TimeLogDto
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public TimeSpan? BreakStart { get; set; }
    public TimeSpan? BreakEnd { get; set; }
    public string? Description { get; set; }

    public double TotalHours
    {
        get
        {
            var worked = EndTime - StartTime;
            if (BreakStart.HasValue && BreakEnd.HasValue)
                worked -= BreakEnd.Value - BreakStart.Value;
            return worked.TotalHours;
        }
    }
}

public class TimeLogCreateDto
{
    [Required]
    public DateTime Date { get; set; }

    [Required]
    public TimeSpan StartTime { get; set; }

    [Required]
    public TimeSpan EndTime { get; set; }

    public TimeSpan? BreakStart { get; set; }

    public TimeSpan? BreakEnd { get; set; }

    public string? Description { get; set; }
}

public class TimeLogUpdateDto
{
    [Required]
    public DateTime Date { get; set; }

    [Required]
    public TimeSpan StartTime { get; set; }

    [Required]
    public TimeSpan EndTime { get; set; }

    public TimeSpan? BreakStart { get; set; }

    public TimeSpan? BreakEnd { get; set; }

    public string? Description { get; set; }
}
