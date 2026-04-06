using System.ComponentModel.DataAnnotations;

namespace TimeManagementBackend.Models;
public class TimeLog
{
    [Key]
    public int Id { get; set; }

    [Required(ErrorMessage = "Date is required")]
    public DateOnly Date { get; set; }

    [Required(ErrorMessage = "Start time is required")]
    public TimeSpan StartTime { get; set; }

    [Required(ErrorMessage = "End time is required")]
    public TimeSpan EndTime { get; set; }

    public TimeSpan? BreakStart { get; set; }

    public TimeSpan? BreakEnd { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }

    public string? UserId { get; set; }
}
