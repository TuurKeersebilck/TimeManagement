using System.ComponentModel.DataAnnotations;

namespace TimeManagementBackend.Models;

public enum SettlementOutcome
{
    Paid = 0,
    /// <summary>Legacy — kept for historical rows only; the confirm flow no longer sets this.</summary>
    LeaveDeducted = 1,
    Unpaid = 2,
    // Recuperated reserved for a future version — do not expose in confirm UI
}

public enum SettlementStatus
{
    PendingReview = 0,
    Settled = 1,
}

public class MonthlySettlement
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;
    public User User { get; set; } = null!;

    public int Year { get; set; }
    public int Month { get; set; }

    public decimal NetBalanceHours { get; set; }
    public decimal OvertimeHours { get; set; }
    public decimal DeficitHours { get; set; }

    /// <summary>Overtime hours paid out via payroll, decided at confirm time. Null until settled.</summary>
    public decimal? PaidOutHours { get; set; }

    /// <summary>
    /// Hours carried to next month's flex balance (positive = overtime carry, negative = deficit carry),
    /// decided at confirm time. Materialized as a linked TimeBankAdjustment. Null until settled.
    /// </summary>
    public decimal? CarriedForwardHours { get; set; }

    /// <summary>Derived at confirm time: Paid when any hours were paid out, otherwise Unpaid.</summary>
    public SettlementOutcome? Outcome { get; set; }
    public SettlementStatus Status { get; set; } = SettlementStatus.PendingReview;

    public string? ReviewedByUserId { get; set; }
    public User? ReviewedByUser { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;
}
