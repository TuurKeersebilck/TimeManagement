using System.ComponentModel.DataAnnotations;
using TimeManagementBackend.Models;

namespace TimeManagementBackend.Models.DTOs;

public class VacationBalanceDto
{
    public int VacationTypeId { get; set; }
    public string VacationTypeName { get; set; } = string.Empty;
    public string? VacationTypeColor { get; set; }
    public bool IsUnlimited { get; set; }
    public bool IsAdminOnly { get; set; }
    /// <summary>Null for an unlimited type.</summary>
    public decimal? YearlyBalance { get; set; }
    public decimal UsedDays { get; set; }
    /// <summary>Days in requests sent and awaiting review; not yet included in UsedDays.</summary>
    public decimal PendingDays { get; set; }
    /// <summary>Days in the employee's drafts, planned but not sent yet.</summary>
    public decimal DraftDays { get; set; }
    /// <summary>Yearly balance minus used days (drafts and pending requests not subtracted). Null for an unlimited type.</summary>
    public decimal? RemainingDays { get; set; }
}

public class VacationDayDto
{
    public int Id { get; set; }
    public int VacationTypeId { get; set; }
    public string VacationTypeName { get; set; } = string.Empty;
    public string? VacationTypeColor { get; set; }
    public DateOnly Date { get; set; }
    public decimal Amount { get; set; }
    public string? Note { get; set; }
}

public class CreateVacationDayDto
{
    [Required]
    public int VacationTypeId { get; set; }

    [Required]
    public DateOnly Date { get; set; }

    [Required]
    public decimal Amount { get; set; }

    public string? Note { get; set; }
}

public class CreateVacationRangeDto
{
    [Required]
    public int VacationTypeId { get; set; }

    [Required]
    public DateOnly StartDate { get; set; }

    [Required]
    public DateOnly EndDate { get; set; }

    [Required]
    public decimal Amount { get; set; }

    public string? Note { get; set; }
}

public class VacationRangeResultDto
{
    public IEnumerable<VacationDayDto> Created { get; set; } = [];
    public int SkippedWeekends { get; set; }
    public int SkippedHolidays { get; set; }
    public int SkippedExisting { get; set; }
}

/// <summary>The dates a range would book, and why the rest of the range was left out.</summary>
public record VacationRangePlan(
    IReadOnlyList<DateOnly> NewDays,
    int SkippedWeekends,
    int SkippedHolidays,
    int SkippedExisting);

// ── Leave requests ─────────────────────────────────────────────────────────────

public class CreateVacationRequestDto
{
    [Required]
    public int VacationTypeId { get; set; }

    [Required]
    public DateOnly StartDate { get; set; }

    [Required]
    public DateOnly EndDate { get; set; }

    [Required]
    public decimal Amount { get; set; }

    [MaxLength(500)]
    public string? Note { get; set; }
}

public class RejectVacationRequestDto
{
    [MaxLength(500)]
    public string? Reason { get; set; }
}

public class VacationRequestDto
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public int VacationTypeId { get; set; }
    public string VacationTypeName { get; set; } = string.Empty;
    public string? VacationTypeColor { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public decimal Amount { get; set; }
    public decimal TotalDays { get; set; }
    public string? Note { get; set; }
    public VacationRequestStatus Status { get; set; }
    public DateTimeOffset RequestedAt { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public string? ReviewedByName { get; set; }
    public string? ReviewNote { get; set; }

    /// <summary>Admin view of a pending request only: what the employee's balance looks like.</summary>
    public VacationRequestBalanceDto? Balance { get; set; }

    /// <summary>Admin view of a pending request only: colleagues already off on days in the range.</summary>
    public List<VacationRequestOverlapDto>? Overlaps { get; set; }
}

public class VacationRequestBalanceDto
{
    /// <summary>Null for an unlimited type or one no longer assigned to the employee.</summary>
    public decimal? YearlyBalance { get; set; }
    public decimal UsedDays { get; set; }
    /// <summary>Days held by the employee's other pending requests of this type.</summary>
    public decimal OtherPendingDays { get; set; }
    /// <summary>What's left if this request is approved (and the others are too). Null when unlimited.</summary>
    public decimal? RemainingAfterApproval { get; set; }
}

public class VacationRequestOverlapDto
{
    public string EmployeeName { get; set; } = string.Empty;
    public List<DateOnly> Dates { get; set; } = [];
    /// <summary>True when the overlap is another pending request rather than approved leave.</summary>
    public bool IsPending { get; set; }
}
