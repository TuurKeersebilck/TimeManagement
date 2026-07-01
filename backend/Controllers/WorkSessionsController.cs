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
public class WorkSessionsController(
    IWorkSessionService service,
    UserManager<User> userManager) : ApiControllerBase(userManager)
{
    [HttpPost("clock-in")]
    public async Task<ActionResult<WorkSessionDto>> ClockIn([FromBody] ClockInDto dto, CancellationToken ct)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        var result = await service.ClockInAsync(user.Id, dto, ct);
        return CreatedAtAction(nameof(GetToday), result);
    }

    [HttpPost("clock-out")]
    public async Task<ActionResult<WorkSessionDto>> ClockOut([FromBody] ClockOutDto dto, CancellationToken ct)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        return Ok(await service.ClockOutAsync(user.Id, dto, ct));
    }

    [HttpPost("break/start")]
    public async Task<ActionResult<BreakRecordDto>> StartBreak(CancellationToken ct)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        var result = await service.StartBreakAsync(user.Id, ct);
        return CreatedAtAction(nameof(GetToday), result);
    }

    [HttpPost("break/end")]
    public async Task<ActionResult<BreakRecordDto>> EndBreak([FromBody] EndBreakDto dto, CancellationToken ct)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        return Ok(await service.EndBreakAsync(user.Id, dto, ct));
    }

    [HttpGet("today")]
    public async Task<ActionResult<TodayStatusDto>> GetToday(CancellationToken ct)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        return Ok(await service.GetTodayAsync(user.Id, ct));
    }

    [HttpGet("today-live")]
    public async Task<ActionResult<TodayLiveDto?>> GetTodayLive(CancellationToken ct)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        var live = await service.GetTodayLiveAsync(user.Id, ct);
        if (live == null) return NoContent();
        return Ok(live);
    }

    [HttpGet("summaries")]
    public async Task<ActionResult<IEnumerable<WorkDaySummaryDto>>> GetSummaries(
        [FromQuery] DateOnly? dateFrom,
        [FromQuery] DateOnly? dateTo,
        CancellationToken ct)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        return Ok(await service.GetSummariesAsync(user.Id, dateFrom, dateTo, ct));
    }

    [HttpGet("my-schedule")]
    public async Task<ActionResult<WorkScheduleDto>> GetMySchedule(CancellationToken ct)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        return Ok(await service.GetMyWorkScheduleAsync(user.Id, ct));
    }

    [HttpPatch("{date}")]
    public async Task<ActionResult<WorkDayDto>> UpdateDay(
        DateOnly date,
        [FromBody] UpdateWorkDayDto dto,
        CancellationToken ct)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        return Ok(await service.UpdateDayAsync(user.Id, date, dto, ct));
    }
}
