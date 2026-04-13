using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TimeManagementBackend.Models;
using TimeManagementBackend.Models.DTOs;
using TimeManagementBackend.Services;

namespace TimeManagementBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class ClockEventsController(
    IClockEventService service,
    UserManager<User> userManager) : ApiControllerBase(userManager)
{
    [HttpGet("target")]
    public async Task<ActionResult<MyTargetDto>> GetMyTarget(CancellationToken ct)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        return Ok(await service.GetMyTargetAsync(user.Id, ct));
    }

    [HttpGet("today")]
    public async Task<ActionResult<IEnumerable<ClockEventDto>>> GetToday(CancellationToken ct)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        return Ok(await service.GetTodayEventsAsync(user.Id, ct));
    }

    [HttpGet("summaries")]
    public async Task<ActionResult<IEnumerable<DaySummaryDto>>> GetSummaries(CancellationToken ct)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        return Ok(await service.GetSummariesAsync(user.Id, ct));
    }

    [HttpPost]
    public async Task<ActionResult<ClockEventDto>> Submit([FromBody] SubmitClockEventDto dto, CancellationToken ct)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        var result = await service.SubmitEventAsync(user.Id, dto, ct);
        return CreatedAtAction(nameof(GetToday), result);
    }
}
