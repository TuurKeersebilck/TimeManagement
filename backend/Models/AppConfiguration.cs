namespace TimeManagementBackend.Models;

/// <summary>Single-row table holding app-wide configuration.</summary>
public class AppConfiguration
{
    public int Id { get; set; }

    /// <summary>ISO 3166-1 alpha-2 country code used to source public holidays (e.g. "BE", "NL").</summary>
    public string? CountryCode { get; set; }

    /// <summary>Default daily working hours target (e.g. 8.0). Null = no target configured.</summary>
    public decimal? DefaultDailyHours { get; set; }

    /// <summary>Default weekly working hours target (e.g. 40.0). Null = no target configured.</summary>
    public decimal? DefaultWeeklyHours { get; set; }

    /// <summary>Email address that receives adjustment request notifications. If null, no email is sent.</summary>
    [System.ComponentModel.DataAnnotations.MaxLength(254)]
    public string? NotificationEmail { get; set; }

    /// <summary>When false, no email is sent when an employee submits a time adjustment request.</summary>
    public bool EnableAdjustmentRequestEmails { get; set; } = true;

    /// <summary>When false, the daily missed clock-in reminder emails are not sent to employees.</summary>
    public bool EnableMissedClockInEmails { get; set; } = true;

    /// <summary>Global minimum break duration in minutes. Null = no minimum enforced.</summary>
    public int? MinimumBreakMinutes { get; set; }

    /// <summary>Global daily overtime allowance in hours before a compliance flag fires. Null = no allowance (flag at target).</summary>
    public decimal? DefaultDailyOvertimeAllowanceHours { get; set; }

    /// <summary>Global weekly overtime allowance in hours before a compliance flag fires. Null = no allowance (flag at target).</summary>
    public decimal? DefaultWeeklyOvertimeAllowanceHours { get; set; }

    /// <summary>Open sessions exceeding this duration (hours) are auto-invalidated by the background job. Default 10h.</summary>
    public decimal MaxSessionHours { get; set; } = 10m;
}
