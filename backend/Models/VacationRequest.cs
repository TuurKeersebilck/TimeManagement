using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TimeManagementBackend.Models;

public enum VacationRequestStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
    /// <summary>Withdrawn by the employee before it was reviewed.</summary>
    Cancelled = 3,
    /// <summary>
    /// Planned by the employee but not sent yet. Only the employee sees drafts; they go to the
    /// admin together, in one email, when the employee submits.
    /// </summary>
    Draft = 4,
}

/// <summary>
/// An employee asking for leave over a date range. It starts as a draft, becomes pending when the
/// employee submits it, and nothing is booked until an admin approves it:
/// approval creates the actual <see cref="VacationDay"/> rows, so everything that reads vacation
/// days (overtime, settlements, calendars, exports) only ever sees approved leave.
/// </summary>
public class VacationRequest
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;

    public int VacationTypeId { get; set; }

    [ForeignKey(nameof(VacationTypeId))]
    public VacationType VacationType { get; set; } = null!;

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    /// <summary>Per day: 1.0 = full day, 0.5 = half day.</summary>
    [Column(TypeName = "numeric(3,1)")]
    public decimal Amount { get; set; }

    /// <summary>
    /// Leave days the request asks for, counted when it was submitted: working days in the range
    /// (no weekends, public holidays or days already booked for this type) times <see cref="Amount"/>.
    /// While a draft or pending, this is held against the employee's balance.
    /// </summary>
    [Column(TypeName = "numeric(5,1)")]
    public decimal TotalDays { get; set; }

    [MaxLength(500)]
    public string? Note { get; set; }

    public VacationRequestStatus Status { get; set; } = VacationRequestStatus.Pending;

    public DateTimeOffset RequestedAt { get; set; }

    public DateTimeOffset? ReviewedAt { get; set; }

    public string? ReviewedByUserId { get; set; }

    [ForeignKey(nameof(ReviewedByUserId))]
    public User? ReviewedByUser { get; set; }

    /// <summary>Optional explanation from the admin, shown to the employee when a request is denied.</summary>
    [MaxLength(500)]
    public string? ReviewNote { get; set; }
}
