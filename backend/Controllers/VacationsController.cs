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
public class VacationsController(
    IVacationService service,
    IAdminService adminService,
    UserManager<User> userManager) : ControllerBase
{
    private readonly IVacationService _service = service;
    private readonly IAdminService _adminService = adminService;
    private readonly UserManager<User> _userManager = userManager;

    private Task<User?> GetCurrentUserAsync() => _userManager.GetUserAsync(User);

    [HttpGet("balances")]
    public async Task<ActionResult<IEnumerable<VacationBalanceDto>>> GetBalances(CancellationToken ct)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        var balances = await _service.GetMyBalancesAsync(user.Id, ct);
        return Ok(balances);
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<VacationDayDto>>> GetVacationDays(CancellationToken ct)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        var days = await _service.GetMyVacationDaysAsync(user.Id, ct);
        return Ok(days);
    }

    [HttpPost]
    public async Task<ActionResult<VacationDayDto>> CreateVacationDay([FromBody] CreateVacationDayDto dto, CancellationToken ct)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        if (await _service.ExistsForDateAndTypeAsync(user.Id, dto.Date, dto.VacationTypeId, ct))
            return Conflict(new ErrorResponseDto { Message = "A vacation day of this type already exists for this date", Code = "DUPLICATE_DATE" });

        var created = await _service.CreateVacationDayAsync(user.Id, dto, ct);
        return CreatedAtAction(nameof(GetVacationDays), created);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<VacationDayDto>> UpdateVacationDay(int id, [FromBody] CreateVacationDayDto dto, CancellationToken ct)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        var updated = await _service.UpdateVacationDayAsync(user.Id, id, dto, ct);
        return Ok(updated);
    }

    [HttpPost("range")]
    public async Task<ActionResult<VacationRangeResultDto>> CreateVacationRange([FromBody] CreateVacationRangeDto dto, CancellationToken ct)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        var result = await _service.CreateVacationRangeAsync(user.Id, dto, ct);
        return Ok(result);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteVacationDay(int id, CancellationToken ct)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        await _service.DeleteVacationDayAsync(user.Id, id, ct);
        return NoContent();
    }

    // ─── Team endpoints (all authenticated users) ─────────────────────────────

    [HttpGet("team")]
    public async Task<ActionResult<IEnumerable<AdminVacationDayDto>>> GetTeamVacationDays(
        [FromQuery] int? vacationTypeId,
        [FromQuery] int? year,
        [FromQuery] int? month,
        CancellationToken ct)
    {
        var days = await _adminService.GetAllVacationDaysAsync(null, vacationTypeId, year, month, ct);
        return Ok(days);
    }

    [HttpGet("types")]
    public async Task<ActionResult<IEnumerable<VacationTypeDto>>> GetVacationTypes(CancellationToken ct)
    {
        var types = await _adminService.GetVacationTypesAsync(ct);
        return Ok(types);
    }
}
