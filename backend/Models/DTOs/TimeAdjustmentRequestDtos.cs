using System.ComponentModel.DataAnnotations;
using TimeManagementBackend.Models;

namespace TimeManagementBackend.Models.DTOs;

public class CreateAdjustmentRequestDto
{
    [Required]
    public DateOnly Date { get; set; }

    public TimeSpan? RequestedClockIn { get; set; }
    public TimeSpan? RequestedBreakStart { get; set; }
    public TimeSpan? RequestedBreakEnd { get; set; }
    public TimeSpan? RequestedClockOut { get; set; }

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
    public TimeSpan? RequestedClockIn { get; set; }
    public TimeSpan? RequestedBreakStart { get; set; }
    public TimeSpan? RequestedBreakEnd { get; set; }
    public TimeSpan? RequestedClockOut { get; set; }
    public string Reason { get; set; } = string.Empty;
    public AdjustmentRequestStatus Status { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
}
