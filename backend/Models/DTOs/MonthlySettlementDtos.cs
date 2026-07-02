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
    public SettlementOutcome? Outcome { get; set; }
    public SettlementStatus Status { get; set; }
    public string? ReviewedByUserId { get; set; }
    public string? ReviewedByName { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset GeneratedAt { get; set; }
}

public class ConfirmSettlementDto
{
    public SettlementOutcome Outcome { get; set; }
    public decimal? OvertimeHoursOverride { get; set; }
    public decimal? DeficitHoursOverride { get; set; }
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
