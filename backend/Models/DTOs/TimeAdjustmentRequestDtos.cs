using System.ComponentModel.DataAnnotations;
using TimeManagementBackend.Models;

namespace TimeManagementBackend.Models.DTOs;

public class CreateAdjustmentRequestDto
{
    [Required]
    public DateOnly Date { get; set; }

    public DateTimeOffset? RequestedClockIn { get; set; }
    public DateTimeOffset? RequestedBreakStart { get; set; }
    public DateTimeOffset? RequestedBreakEnd { get; set; }
    public DateTimeOffset? RequestedClockOut { get; set; }

    [Required]
    [MaxLength(2000)]
    public string Reason { get; set; } = string.Empty;
}

public class AdjustmentRequestDto
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public DateTimeOffset? RequestedClockIn { get; set; }
    public DateTimeOffset? RequestedBreakStart { get; set; }
    public DateTimeOffset? RequestedBreakEnd { get; set; }
    public DateTimeOffset? RequestedClockOut { get; set; }
    public string Reason { get; set; } = string.Empty;
    public AdjustmentRequestStatus Status { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
}
