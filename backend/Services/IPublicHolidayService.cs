using TimeManagementBackend.Models.DTOs;

namespace TimeManagementBackend.Services;

public interface IPublicHolidayService
{
    Task<AppConfigurationDto> GetConfigurationAsync(CancellationToken ct = default);
    Task<AppConfigurationDto> SetCountryAsync(string countryCode, CancellationToken ct = default);
    Task<IEnumerable<PublicHolidayDto>> GetHolidaysAsync(int year, CancellationToken ct = default);
    Task<IEnumerable<PublicHolidayDto>> RefreshHolidaysAsync(int year, CancellationToken ct = default);
    Task<PublicHolidayDto> AddCustomHolidayAsync(CreateHolidayDto dto, CancellationToken ct = default);
    Task DeleteHolidayAsync(int id, CancellationToken ct = default);
    Task<IEnumerable<AvailableCountryDto>> GetAvailableCountriesAsync(CancellationToken ct = default);
}
