namespace TimeManagementBackend.Models;

/// <summary>Optional per-employee working hours override. Null values fall back to the global default in AppConfiguration.</summary>
public class EmployeeTarget
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public User User { get; set; } = null!;
    public decimal? DailyHours { get; set; }
    public decimal? WeeklyHours { get; set; }
    /// <summary>Per-employee minimum break duration in minutes. Null = use global default.</summary>
    public int? MinimumBreakMinutes { get; set; }
    /// <summary>Per-employee daily overtime allowance in hours before a compliance flag fires. Null = use global default.</summary>
    public decimal? DailyOvertimeAllowanceHours { get; set; }
    /// <summary>Per-employee weekly overtime allowance in hours before a compliance flag fires. Null = use global default.</summary>
    public decimal? WeeklyOvertimeAllowanceHours { get; set; }
}
