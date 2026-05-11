using Microsoft.AspNetCore.Identity;

namespace TimeManagementBackend.Models;

public class User : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Employee;
    public bool IsDisabled { get; set; } = false;
    public ICollection<ClockEvent> ClockEvents { get; set; } = new List<ClockEvent>();
    public ICollection<TimeAdjustmentRequest> AdjustmentRequests { get; set; } = new List<TimeAdjustmentRequest>();
}