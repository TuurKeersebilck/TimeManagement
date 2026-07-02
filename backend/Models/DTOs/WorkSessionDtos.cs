using System.ComponentModel.DataAnnotations;
using TimeManagementBackend.Models;

namespace TimeManagementBackend.Models.DTOs;

// ── Request DTOs ───────────────────────────────────────────────────────────────

public class ClockInDto
{
    /// <summary>Client UTC timestamp. Must be within ±5 min of server now. Falls back to server stamp if omitted.</summary>
    public DateTimeOffset? RecordedAt { get; set; }

    public bool WorkedFromHome { get; set; } = false;

    /// <summary>IANA timezone ID for server-side LocalDate derivation (e.g. "Europe/Brussels").</summary>
    [MaxLength(100)]
    public string? TimeZoneId { get; set; }
}

public class ClockOutDto
{
    public DateTimeOffset? RecordedAt { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }
}

public class StartBreakDto { }

public class EndBreakDto
{
    public DateTimeOffset? RecordedAt { get; set; }
}

public class UpdateWorkDayDto
{
    [MaxLength(1000)]
    public string? Description { get; set; }

    public bool? WorkedFromHome { get; set; }
}

// ── Response DTOs ──────────────────────────────────────────────────────────────

public class BreakRecordDto
{
    public int Id { get; set; }
    public DateTimeOffset BreakStart { get; set; }
    public DateTimeOffset BreakStartServerStamp { get; set; }
    public DateTimeOffset? BreakEnd { get; set; }
    public DateTimeOffset? BreakEndServerStamp { get; set; }
}

public class WorkSessionDto
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public DateTimeOffset ClockIn { get; set; }
    public DateTimeOffset ClockInServerStamp { get; set; }
    public DateTimeOffset? ClockOut { get; set; }
    public DateTimeOffset? ClockOutServerStamp { get; set; }
    public WorkSessionStatus Status { get; set; }
    public List<BreakRecordDto> Breaks { get; set; } = [];
}

public class WorkDayDto
{
    public DateOnly Date { get; set; }
    public bool WorkedFromHome { get; set; }
    public string? Description { get; set; }
}

public class TodayStatusDto
{
    /// <summary>The currently open session, if the user is clocked in.</summary>
    public WorkSessionDto? OpenSession { get; set; }

    /// <summary>Sessions closed today (normally at most one, but invalidation can create more).</summary>
    public List<WorkSessionDto> ClosedSessions { get; set; } = [];

    public WorkDayDto? WorkDay { get; set; }
}

public class TodayLiveDto
{
    public int SessionId { get; set; }
    public DateTimeOffset ClockIn { get; set; }

    /// <summary>Net worked minutes since clock-in, excluding both closed and ongoing break time.</summary>
    public double ElapsedMinutes { get; set; }

    public bool IsOnBreak { get; set; }
    public DateTimeOffset? BreakStartedAt { get; set; }
}

/// <summary>Aggregated view of one calendar day — replaces the old DaySummaryDto.</summary>
public class WorkDaySummaryDto
{
    public DateOnly Date { get; set; }
    public double TotalWorkedHours { get; set; }
    public bool HasOpenSession { get; set; }
    public WorkDayDto? WorkDay { get; set; }
    public List<WorkSessionDto> Sessions { get; set; } = [];
    public decimal? VacationAmount { get; set; }
    public string? VacationTypeName { get; set; }
}

/// <summary>Resolved work schedule for the authenticated employee — replaces MyTargetDto.</summary>
public class WorkScheduleDto
{
    /// <summary>Mon–Sun resolved targets (per-employee override ?? global default).</summary>
    public List<WorkdayTargetDto> WorkdayTargets { get; set; } = [];

    public int? MinimumBreakMinutes { get; set; }
    public decimal? DailyOvertimeAllowanceHours { get; set; }
    public decimal? WeeklyOvertimeAllowanceHours { get; set; }
}
