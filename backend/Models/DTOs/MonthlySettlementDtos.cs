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
/// Allocation of a month's balance, decided at confirm time. The admin has full control:
/// PaidOutHours (≥ 0) goes to the payroll export; CarryForwardHours is signed — positive adds to
/// next month's flex balance, negative starts it in deficit. Any mismatch with the computed
/// balance is intentional (forfeited / forgiven / granted) and surfaced as a hint in the UI.
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
