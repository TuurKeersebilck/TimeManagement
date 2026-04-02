using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TimeManagementBackend.Models.DTOs;
using TimeManagementBackend.Services;

namespace TimeManagementBackend.Controllers;

/// <summary>Read-only holiday endpoint accessible by all authenticated users.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class PublicHolidaysController(IPublicHolidayService service) : ControllerBase
{
    [HttpGet("{year:int}")]
    public async Task<ActionResult<IEnumerable<PublicHolidayDto>>> GetHolidays(int year, CancellationToken ct)
        => Ok(await service.GetHolidaysAsync(year, ct));
}
