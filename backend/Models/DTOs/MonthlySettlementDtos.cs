using TimeManagementBackend.Models;

namespace TimeManagementBackend.Models.DTOs;

public class MonthlySettlementDto
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal NetBalanceHours { get; set; }
    public decimal OvertimeHours { get; set; }
    public decimal DeficitHours { get; set; }
    public decimal? PaidOutHours { get; set; }
    public decimal? CarriedForwardHours { get; set; }
    public SettlementOutcome? Outcome { get; set; }
    public SettlementStatus Status { get; set; }
    public string? ReviewedByUserId { get; set; }
    public string? ReviewedByName { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset GeneratedAt { get; set; }
}

/// <summary>
/// Allocation of a month's frozen balance, decided at confirm time.
/// Overtime month: PaidOutHours + CarryForwardHours ≤ overtime; the remainder is forfeited.
/// Deficit month: PaidOutHours must be 0; CarryForwardHours is either the full negative balance
/// (carry the deficit) or 0 (forgive it).
/// </summary>
public class ConfirmSettlementDto
{
    public decimal PaidOutHours { get; set; }
    public decimal CarryForwardHours { get; set; }
    public string? Notes { get; set; }
}

public class BlockerDto
{
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class SettlementBlockersDto
{
    public List<BlockerDto> Blockers { get; set; } = [];
}
