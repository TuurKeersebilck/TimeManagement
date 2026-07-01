using System.ComponentModel.DataAnnotations;
using TimeManagementBackend.Models;

namespace TimeManagementBackend.Models.DTOs;

// ── Snapshot types ─────────────────────────────────────────────────────────────

public class SnapshotBreakDto
{
    /// <summary>Null for new breaks being added; set to the existing BreakRecord.Id for edits.</summary>
    public int? BreakRecordId { get; set; }

    [Required]
    public DateTimeOffset BreakStart { get; set; }

    [Required]
    public DateTimeOffset BreakEnd { get; set; }
}

public class SnapshotSessionDto
{
    /// <summary>Null for new sessions being added; set to the existing WorkSession.Id for edits.</summary>
    public int? WorkSessionId { get; set; }

    [Required]
    public DateTimeOffset ClockIn { get; set; }

    [Required]
    public DateTimeOffset ClockOut { get; set; }

    public List<SnapshotBreakDto> Breaks { get; set; } = [];
}

public class DesiredDaySnapshotDto
{
    [Required]
    public List<SnapshotSessionDto> Sessions { get; set; } = [];
}

// ── Request DTOs ───────────────────────────────────────────────────────────────

public class CreateAdjustmentRequestDto
{
    [Required]
    public DateOnly Date { get; set; }

    [Required]
    public DesiredDaySnapshotDto DesiredDaySnapshot { get; set; } = null!;

    [Required]
    [MaxLength(2000)]
    public string Reason { get; set; } = string.Empty;
}

// ── Response DTOs ──────────────────────────────────────────────────────────────

public class AdjustmentRequestDto
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public DateOnly Date { get; set; }

    /// <summary>Raw JSON string of the DesiredDaySnapshot. Frontend parses it for display.</summary>
    public string DesiredDaySnapshot { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;
    public AdjustmentRequestStatus Status { get; set; }
    public DateTimeOffset RequestedAt { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
}

// ── TimeBankAdjustment DTOs ────────────────────────────────────────────────────

public class CreateTimeBankAdjustmentDto
{
    [Required]
    [MaxLength(2000)]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public DateOnly EffectiveDate { get; set; }

    [Required]
    public decimal Hours { get; set; }

    [Required]
    [MaxLength(2000)]
    public string Reason { get; set; } = string.Empty;
}

public class TimeBankAdjustmentDto
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public DateOnly EffectiveDate { get; set; }
    public decimal Hours { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? CreatedByUserId { get; set; }
    public string? CreatedByName { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

// ── Admin direct edit ──────────────────────────────────────────────────────────

public class AdminDirectEditDto
{
    [Required]
    public DesiredDaySnapshotDto Snapshot { get; set; } = null!;
}
