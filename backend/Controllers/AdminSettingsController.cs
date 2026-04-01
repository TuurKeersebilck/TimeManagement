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

    [HttpDelete("holidays/{id:int}")]
    public async Task<IActionResult> DeleteHoliday(int id, CancellationToken ct)
    {
        await service.DeleteHolidayAsync(id, ct);
        return NoContent();
    }
}
