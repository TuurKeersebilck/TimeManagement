namespace TimeManagementBackend.Models.DTOs;

public class PerDayOvertimeDto
{
    public DateOnly Date { get; set; }

    /// <summary>Hours worked across all Closed sessions for this day. Open/Invalidated sessions = 0.</summary>
    public decimal WorkedHours { get; set; }

    /// <summary>Target hours from EffectiveTargetService (applies vacation fractions, holidays, weekends).</summary>
    public decimal TargetHours { get; set; }

    /// <summary>WorkedHours − TargetHours. Positive = surplus, negative = deficit.</summary>
    public decimal FlexDelta { get; set; }
}

public enum ComplianceFlagType
{
    DailyOvertime,
    WeeklyOvertime,
}

public class ComplianceFlagDto
{
    /// <summary>Day of the violation (for weekly: ISO week's Monday).</summary>
    public DateOnly Date { get; set; }

    public ComplianceFlagType Type { get; set; }
    public decimal HoursWorked { get; set; }

    /// <summary>The threshold that was exceeded: target + overtime allowance.</summary>
    public decimal Threshold { get; set; }
}

public class OvertimeResultDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public List<PerDayOvertimeDto> PerDay { get; set; } = [];

    /// <summary>Σ FlexDelta over all included days + TimeBankAdjustment.Hours for this month.</summary>
    public decimal RunningBalanceHours { get; set; }

    public List<ComplianceFlagDto> ComplianceFlags { get; set; } = [];
}
