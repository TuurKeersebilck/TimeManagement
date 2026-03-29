namespace TimeManagementBackend.Models.DTOs;

public class AdminTimeLogDto
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeEmail { get; set; } = string.Empty;
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

public class EmployeeDto
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}
