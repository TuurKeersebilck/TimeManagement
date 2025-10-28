using System.ComponentModel.DataAnnotations;

namespace TimeManagementBackend.Models;
public class TimeLog
{
    [Key]
    public int Id { get; set; }
    
    [Required(ErrorMessage = "Date is required")]
    public DateTime Date { get; set; }
    
    [Required(ErrorMessage = "Start time is required")]
    public TimeSpan StartTime { get; set; }
    
    [Required(ErrorMessage = "End time is required")]
    public TimeSpan EndTime { get; set; }
    
    [Required(ErrorMessage = "Break time is required")]
    public TimeSpan Break { get; set; }
    
    public string? UserId { get; set; }

    // Computed property
    [Display(Name = "Total Hours")]
    public double TotalHours => (EndTime - StartTime - Break).TotalHours;
}