using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TimeManagementBackend.Models.DTOs;
using TimeManagementBackend.Services;

namespace TimeManagementBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]
public class AdminController(IAdminService adminService) : ControllerBase
{
    private readonly IAdminService _adminService = adminService;

    [HttpGet("timelogs")]
    public async Task<ActionResult<IEnumerable<AdminTimeLogDto>>> GetAllTimeLogs(
        [FromQuery] string? userId,
        [FromQuery] DateOnly? dateFrom,
        [FromQuery] DateOnly? dateTo,
        CancellationToken ct)
    {
        var logs = await _adminService.GetAllTimeLogsAsync(userId, dateFrom, dateTo, ct);
        return Ok(logs);
    }

    [HttpGet("employees")]
    public async Task<ActionResult<IEnumerable<EmployeeDto>>> GetEmployees(CancellationToken ct)
    {
        var employees = await _adminService.GetEmployeesAsync(ct);
        return Ok(employees);
    }

    // ─── Vacation types ───────────────────────────────────────────────────────

    [HttpGet("vacation-types")]
    public async Task<ActionResult<IEnumerable<VacationTypeDto>>> GetVacationTypes(CancellationToken ct)
    {
        var types = await _adminService.GetVacationTypesAsync(ct);
        return Ok(types);
    }

    [HttpPost("vacation-types")]
    public async Task<ActionResult<VacationTypeDto>> CreateVacationType(
        [FromBody] VacationTypeFormDto dto,
        CancellationToken ct)
    {
        var created = await _adminService.CreateVacationTypeAsync(dto, ct);
        return CreatedAtAction(nameof(GetVacationTypes), created);
    }

    [HttpPut("vacation-types/{id:int}")]
    public async Task<ActionResult<VacationTypeDto>> UpdateVacationType(
        int id,
        [FromBody] VacationTypeFormDto dto,
        CancellationToken ct)
    {
        var updated = await _adminService.UpdateVacationTypeAsync(id, dto, ct);
        return Ok(updated);
    }

    [HttpDelete("vacation-types/{id:int}")]
    public async Task<IActionResult> DeleteVacationType(int id, CancellationToken ct)
    {
        await _adminService.DeleteVacationTypeAsync(id, ct);
        return NoContent();
    }

    // ─── Employee vacation balances ───────────────────────────────────────────

    [HttpGet("employees/{userId}/vacation-balances")]
    public async Task<ActionResult<IEnumerable<EmployeeVacationBalanceDto>>> GetEmployeeBalances(
        string userId,
        CancellationToken ct)
    {
        var balances = await _adminService.GetEmployeeBalancesAsync(userId, ct);
        return Ok(balances);
    }

    [HttpPost("employees/{userId}/vacation-balances")]
    public async Task<ActionResult<EmployeeVacationBalanceDto>> AssignVacationType(
        string userId,
        [FromBody] AssignVacationTypeDto dto,
        CancellationToken ct)
    {
        var balance = await _adminService.AssignVacationTypeAsync(userId, dto, ct);
        return CreatedAtAction(nameof(GetEmployeeBalances), new { userId }, balance);
    }

    [HttpPut("employees/{userId}/vacation-balances/{balanceId:int}")]
    public async Task<ActionResult<EmployeeVacationBalanceDto>> UpdateEmployeeBalance(
        string userId,
        int balanceId,
        [FromBody] UpdateVacationBalanceDto dto,
        CancellationToken ct)
    {
        var balance = await _adminService.UpdateEmployeeBalanceAsync(balanceId, dto, ct);
        return Ok(balance);
    }

    [HttpDelete("employees/{userId}/vacation-balances/{balanceId:int}")]
    public async Task<IActionResult> RemoveEmployeeVacationType(
        string userId,
        int balanceId,
        CancellationToken ct)
    {
        await _adminService.RemoveEmployeeVacationTypeAsync(balanceId, ct);
        return NoContent();
    }

    // ─── Working hours targets ────────────────────────────────────────────────

    [HttpGet("employees/{userId}/target")]
    public async Task<ActionResult<EmployeeTargetDto>> GetEmployeeTarget(string userId, CancellationToken ct)
        => Ok(await _adminService.GetEmployeeTargetAsync(userId, ct));

    [HttpPut("employees/{userId}/target")]
    public async Task<ActionResult<EmployeeTargetDto>> SetEmployeeTarget(
        string userId,
        [FromBody] SetEmployeeTargetDto dto,
        CancellationToken ct)
        => Ok(await _adminService.SetEmployeeTargetAsync(userId, dto, ct));

    [HttpGet("employees/{userId}/weekly-summary")]
    public async Task<ActionResult<IEnumerable<WeekSummaryDto>>> GetWeeklySummary(
        string userId,
        [FromQuery] int weeks = 8,
        CancellationToken ct = default)
        => Ok(await _adminService.GetEmployeeWeeklySummaryAsync(userId, weeks, ct));

    // ─── Vacation overview ────────────────────────────────────────────────────

    [HttpGet("vacations")]
    public async Task<ActionResult<IEnumerable<AdminVacationDayDto>>> GetAllVacationDays(
        [FromQuery] string? userId,
        [FromQuery] int? vacationTypeId,
        [FromQuery] int? year,
        [FromQuery] int? month,
        CancellationToken ct)
    {
        var days = await _adminService.GetAllVacationDaysAsync(userId, vacationTypeId, year, month, ct);
        return Ok(days);
    }
}
