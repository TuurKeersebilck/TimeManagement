namespace TimeManagementBackend.Models;
public class TimeLog
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public TimeSpan Break { get; set; }

    // Computed property (wordt niet opgeslagen in DB)
    public double TotalHours => (EndTime - StartTime - Break).TotalHours;

    public int UserId { get; set; }
    public User? User { get; set; }
}