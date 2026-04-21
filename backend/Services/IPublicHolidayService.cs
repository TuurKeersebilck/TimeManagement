using TimeManagementBackend.Models.DTOs;

namespace TimeManagementBackend.Services;

public interface IPublicHolidayService
{
    Task<AppConfigurationDto> GetConfigurationAsync(CancellationToken ct = default);
    Task<AppConfigurationDto> SetCountryAsync(string countryCode, CancellationToken ct = default);
    Task<AppConfigurationDto> SetDefaultTargetsAsync(decimal? dailyHours, decimal? weeklyHours, CancellationToken ct = default);
    Task<IEnumerable<PublicHolidayDto>> GetHolidaysAsync(int year, CancellationToken ct = default);
    Task<IEnumerable<PublicHolidayDto>> RefreshHolidaysAsync(int year, CancellationToken ct = default);
    Task<PublicHolidayDto> AddCustomHolidayAsync(CreateHolidayDto dto, CancellationToken ct = default);
    Task<PublicHolidayDto> SetIsWorkingDayAsync(int id, bool isWorkingDay, CancellationToken ct = default);
    Task DeleteHolidayAsync(int id, CancellationToken ct = default);
    Task<IEnumerable<AvailableCountryDto>> GetAvailableCountriesAsync(CancellationToken ct = default);
    Task<AppConfigurationDto> SetNotificationEmailAsync(string? email, CancellationToken ct = default);
    Task<AppConfigurationDto> SetNotificationTogglesAsync(bool enableAdjustmentRequestEmails, bool enableMissedClockInEmails, CancellationToken ct = default);
}
