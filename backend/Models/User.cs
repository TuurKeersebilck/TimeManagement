namespace TimeManagementBackend.Models;

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;

    public ICollection<TimeLog> TimeLogs { get; set; } = new List<TimeLog>();
}