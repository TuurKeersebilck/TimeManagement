using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TimeManagementBackend.Models.DTOs;
using TimeManagementBackend.Services;

namespace TimeManagementBackend.Controllers;

[ApiController]
[Route("api/admin/settings")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]
public class AdminSettingsController(IPublicHolidayService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<AppConfigurationDto>> GetConfiguration(CancellationToken ct)
        => Ok(await service.GetConfigurationAsync(ct));

    [HttpPut("country")]
    public async Task<ActionResult<AppConfigurationDto>> SetCountry([FromBody] SetCountryDto dto, CancellationToken ct)
        => Ok(await service.SetCountryAsync(dto.CountryCode, ct));

    [HttpPut("targets")]
    public async Task<ActionResult<AppConfigurationDto>> SetDefaultTargets([FromBody] SetDefaultTargetsDto dto, CancellationToken ct)
        => Ok(await service.SetDefaultTargetsAsync(dto.DefaultDailyHours, dto.DefaultWeeklyHours, ct));

    [HttpPut("notification-email")]
    public async Task<ActionResult<AppConfigurationDto>> SetNotificationEmail([FromBody] SetNotificationEmailDto dto, CancellationToken ct)
        => Ok(await service.SetNotificationEmailAsync(dto.Email, ct));

    [HttpPut("notification-toggles")]
    public async Task<ActionResult<AppConfigurationDto>> SetNotificationToggles([FromBody] SetNotificationTogglesDto dto, CancellationToken ct)
        => Ok(await service.SetNotificationTogglesAsync(dto.EnableAdjustmentRequestEmails, dto.EnableMissedClockInEmails, ct));

    [HttpGet("available-countries")]
    public async Task<ActionResult<IEnumerable<AvailableCountryDto>>> GetAvailableCountries(CancellationToken ct)
        => Ok(await service.GetAvailableCountriesAsync(ct));

    [HttpGet("holidays/{year:int}")]
    public async Task<ActionResult<IEnumerable<PublicHolidayDto>>> GetHolidays(int year, CancellationToken ct)
        => Ok(await service.GetHolidaysAsync(year, ct));

    [HttpPost("holidays/refresh/{year:int}")]
    public async Task<ActionResult<IEnumerable<PublicHolidayDto>>> RefreshHolidays(int year, CancellationToken ct)
        => Ok(await service.RefreshHolidaysAsync(year, ct));

    [HttpPost("holidays")]
    public async Task<ActionResult<PublicHolidayDto>> AddHoliday([FromBody] CreateHolidayDto dto, CancellationToken ct)
        => Ok(await service.AddCustomHolidayAsync(dto, ct));

    [HttpPatch("holidays/{id:int}/is-working-day")]
    public async Task<ActionResult<PublicHolidayDto>> SetIsWorkingDay(int id, [FromBody] SetIsWorkingDayDto dto, CancellationToken ct)
        => Ok(await service.SetIsWorkingDayAsync(id, dto.IsWorkingDay, ct));

    [HttpDelete("holidays/{id:int}")]
    public async Task<IActionResult> DeleteHoliday(int id, CancellationToken ct)
    {
        await service.DeleteHolidayAsync(id, ct);
        return NoContent();
    }
}
