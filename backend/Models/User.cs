using Microsoft.AspNetCore.Identity;

namespace TimeManagementBackend.Models;

public class User : IdentityUser
{
    public ICollection<TimeLog> TimeLogs { get; set; } = new List<TimeLog>();
}