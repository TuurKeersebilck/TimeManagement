namespace TimeManagementBackend.Models.DTOs;

public class CalendarTokenInfoDto
{
    public bool HasToken { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
}

public class CalendarTokenDto
{
    public string FeedUrl { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
}
