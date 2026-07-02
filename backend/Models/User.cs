using Microsoft.AspNetCore.Identity;

namespace TimeManagementBackend.Models;

public class User : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Employee;
    public bool IsDisabled { get; set; } = false;
    public string? CalendarTokenHash { get; set; }
    public DateTimeOffset? CalendarTokenExpiresAt { get; set; }
    public ICollection<TimeAdjustmentRequest> AdjustmentRequests { get; set; } = new List<TimeAdjustmentRequest>();
    public ICollection<WorkSession> WorkSessions { get; set; } = new List<WorkSession>();
    public ICollection<WorkDay> WorkDays { get; set; } = new List<WorkDay>();
    public ICollection<TimeBankAdjustment> TimeBankAdjustments { get; set; } = new List<TimeBankAdjustment>();
    public ICollection<MonthlySettlement> MonthlySettlements { get; set; } = new List<MonthlySettlement>();
}