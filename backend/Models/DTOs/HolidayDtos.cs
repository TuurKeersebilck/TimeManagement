namespace TimeManagementBackend.Models.DTOs;

public class PublicHolidayDto
{
    public int Id { get; set; }
    public string Date { get; set; } = string.Empty; // "YYYY-MM-DD"
    public string Name { get; set; } = string.Empty;
    public bool IsCustom { get; set; }
    /// <summary>True when the company works this day despite it being a public holiday.</summary>
    public bool IsWorkingDay { get; set; }
}

public class SetIsWorkingDayDto
{
    public bool IsWorkingDay { get; set; }
}

public class CreateHolidayDto
{
    public string Date { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class AppConfigurationDto
{
    public string? CountryCode { get; set; }
    public string? CountryName { get; set; }
    public decimal? DefaultDailyHours { get; set; }
    public decimal? DefaultWeeklyHours { get; set; }
    public string? NotificationEmail { get; set; }
}

public class SetNotificationEmailDto
{
    public string? Email { get; set; }
}

public class SetDefaultTargetsDto
{
    public decimal? DefaultDailyHours { get; set; }
    public decimal? DefaultWeeklyHours { get; set; }
}

public class SetCountryDto
{
    public string CountryCode { get; set; } = string.Empty;
}

public class AvailableCountryDto
{
    public string CountryCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

// Shape returned by Nager.Date API
public class NagerHolidayDto
{
    public string Date { get; set; } = string.Empty;
    public string LocalName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class NagerCountryDto
{
    public string CountryCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}
