using System.ComponentModel.DataAnnotations;

namespace TimeManagementBackend.Models.DTOs;

public class TimeLogDto
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public TimeSpan Break { get; set; }
}

public class TimeLogCreateDto
{
    [Required]
    public DateTime Date { get; set; }

    [Required]
    public TimeSpan StartTime { get; set; }

    [Required]
    public TimeSpan EndTime { get; set; }

    [Required]
    public TimeSpan Break { get; set; }
}

public class TimeLogUpdateDto
{
    [Required]
    public DateTime Date { get; set; }

    [Required]
    public TimeSpan StartTime { get; set; }

    [Required]
    public TimeSpan EndTime { get; set; }

    [Required]
    public TimeSpan Break { get; set; }
}