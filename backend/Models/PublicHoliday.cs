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
}
