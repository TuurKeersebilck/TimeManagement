using Microsoft.AspNetCore.Identity;

namespace TimeManagementBackend.Models;

public class User : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
    public ICollection<TimeLog> TimeLogs { get; set; } = new List<TimeLog>();
}