namespace TimeManagementBackend.Models;

/// <summary>Optional per-employee overrides for minimum break duration and overtime allowance. Null values fall back to the global default in AppConfiguration. Daily/weekly hour targets live in WorkdayTarget instead.</summary>
public class EmployeeTarget
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public User User { get; set; } = null!;
    /// <summary>Per-employee minimum break duration in minutes. Null = use global default.</summary>
    public int? MinimumBreakMinutes { get; set; }
    /// <summary>Per-employee daily overtime allowance in hours before a compliance flag fires. Null = use global default.</summary>
    public decimal? DailyOvertimeAllowanceHours { get; set; }
    /// <summary>Per-employee weekly overtime allowance in hours before a compliance flag fires. Null = use global default.</summary>
    public decimal? WeeklyOvertimeAllowanceHours { get; set; }
}
