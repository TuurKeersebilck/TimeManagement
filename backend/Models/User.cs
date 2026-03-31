using Microsoft.AspNetCore.Identity;

namespace TimeManagementBackend.Models;

public class User : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Employee;
    public ICollection<TimeLog> TimeLogs { get; set; } = new List<TimeLog>();
}