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
public class TimeLogsController(
    ITimeLogService service,
    UserManager<User> userManager,
    ILogger<TimeLogsController> logger) : ControllerBase
{
    private readonly ITimeLogService _service = service;
    private readonly UserManager<User> _userManager = userManager;
    private readonly ILogger<TimeLogsController> _logger = logger;

    private Task<User?> GetCurrentUserAsync() => _userManager.GetUserAsync(User);

    private static string? ValidateTimes(TimeSpan startTime, TimeSpan endTime, TimeSpan? breakStart, TimeSpan? breakEnd)
    {
        if (endTime <= startTime)
            return "End time must be after start time";

        var hasBreakStart = breakStart.HasValue;
        var hasBreakEnd = breakEnd.HasValue;

        if (hasBreakStart != hasBreakEnd)
            return "Both break start and break end must be provided together";

        if (hasBreakStart && hasBreakEnd)
        {
            if (breakStart!.Value < startTime || breakEnd!.Value > endTime)
                return "Break must fall within the working hours";

            if (breakEnd!.Value <= breakStart!.Value)
                return "Break end must be after break start";
        }

        return null;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<TimeLogDto>>> GetTimeLogs(CancellationToken ct)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        var items = await _service.GetForUserAsync(user.Id, ct);
        return Ok(items);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<TimeLogDto>> GetTimeLog(int id, CancellationToken ct)
    {
        if (id <= 0) return BadRequest("Invalid id");

        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        var item = await _service.GetByIdAsync(id, user.Id, ct);
        if (item == null) return NotFound();
        return Ok(item);
    }

    [HttpPost]
    public async Task<ActionResult<TimeLogDto>> CreateTimeLog([FromBody] TimeLogCreateDto dto, CancellationToken ct)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        var validationError = ValidateTimes(dto.StartTime, dto.EndTime, dto.BreakStart, dto.BreakEnd);
        if (validationError != null) return BadRequest(validationError);

        if (await _service.ExistsForDateAsync(user.Id, dto.Date, cancellationToken: ct))
            return Conflict("A time log already exists for this date");

        var created = await _service.CreateAsync(dto, user.Id, ct);
        return CreatedAtAction(nameof(GetTimeLog), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateTimeLog(int id, [FromBody] TimeLogUpdateDto dto, CancellationToken ct)
    {
        if (id <= 0) return BadRequest("Invalid id");

        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        var validationError = ValidateTimes(dto.StartTime, dto.EndTime, dto.BreakStart, dto.BreakEnd);
        if (validationError != null) return BadRequest(validationError);

        if (await _service.ExistsForDateAsync(user.Id, dto.Date, excludeId: id, cancellationToken: ct))
            return Conflict("A time log already exists for this date");

        var updated = await _service.UpdateAsync(id, dto, user.Id, ct);
        if (!updated) return NotFound();

        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteTimeLog(int id, CancellationToken ct)
    {
        if (id <= 0) return BadRequest("Invalid id");

        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        var deleted = await _service.DeleteAsync(id, user.Id, ct);
        if (!deleted) return NotFound();

        return NoContent();
    }
}
