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

        if (dto.EndTime <= dto.StartTime)
            return BadRequest("End time must be after start time");

        if (dto.Break < TimeSpan.Zero || dto.Break >= (dto.EndTime - dto.StartTime))
            return BadRequest("Invalid break time");

        var created = await _service.CreateAsync(dto, user.Id, ct);
        return CreatedAtAction(nameof(GetTimeLog), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateTimeLog(int id, [FromBody] TimeLogUpdateDto dto, CancellationToken ct)
    {
        if (id <= 0) return BadRequest("Invalid id");

        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        if (dto.EndTime <= dto.StartTime)
            return BadRequest("End time must be after start time");

        if (dto.Break < TimeSpan.Zero || dto.Break >= (dto.EndTime - dto.StartTime))
            return BadRequest("Invalid break time");

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
