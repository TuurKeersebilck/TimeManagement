namespace TimeManagementBackend.Models;

public class PublicHoliday
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;
    public int Year { get; set; }

    /// <summary>True when added manually by an admin rather than fetched from the API.</summary>
    public bool IsCustom { get; set; }

    /// <summary>
    /// True when the company works on this public holiday (i.e. it is NOT a day off).
    /// Defaults to false — holidays are days off by default.
    /// When true the day is still shown on the calendar but counts as a normal working day
    /// for vacation range calculation.
    /// </summary>
    public bool IsWorkingDay { get; set; }
}
