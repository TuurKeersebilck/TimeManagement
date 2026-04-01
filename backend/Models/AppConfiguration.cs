namespace TimeManagementBackend.Models;

/// <summary>Single-row table holding app-wide configuration.</summary>
public class AppConfiguration
{
    public int Id { get; set; }

    /// <summary>ISO 3166-1 alpha-2 country code used to source public holidays (e.g. "BE", "NL").</summary>
    public string? CountryCode { get; set; }
}
