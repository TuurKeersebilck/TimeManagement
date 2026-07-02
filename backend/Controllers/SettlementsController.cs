using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TimeManagementBackend.Exceptions;
using TimeManagementBackend.Models;
using TimeManagementBackend.Models.DTOs;
using TimeManagementBackend.Services;

namespace TimeManagementBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]
public class SettlementsController(
    ISettlementService settlementService,
    UserManager<User> userManager) : ApiControllerBase(userManager)
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<MonthlySettlementDto>>> GetAll(
        [FromQuery] int? year,
        [FromQuery] int? month,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        return Ok(await settlementService.GetSettlementsAsync(year ?? now.Year, month ?? now.Month, ct));
    }

    [HttpPost("generate")]
    public async Task<ActionResult<IEnumerable<MonthlySettlementDto>>> Generate(
        [FromQuery] int year, [FromQuery] int month, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        if (year > now.Year || (year == now.Year && month >= now.Month))
            throw new ValidationException("Settlements can only be generated for a completed month.");

        await settlementService.GenerateForAllEmployeesAsync(year, month, ct);
        return Ok(await settlementService.GetSettlementsAsync(year, month, ct));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<MonthlySettlementDto>> GetById(int id, CancellationToken ct)
        => Ok(await settlementService.GetSettlementDetailAsync(id, ct));

    [HttpPost("{id:int}/confirm")]
    public async Task<IActionResult> Confirm(int id, [FromBody] ConfirmSettlementDto dto, CancellationToken ct)
    {
        var admin = await GetCurrentUserAsync();
        if (admin == null) return Unauthorized();

        await settlementService.ConfirmAsync(id, dto, admin.Id, ct);
        return NoContent();
    }

    [HttpGet("employee/{userId}")]
    public async Task<ActionResult<IEnumerable<MonthlySettlementDto>>> GetEmployeeHistory(
        string userId, CancellationToken ct)
        => Ok(await settlementService.GetEmployeeHistoryAsync(userId, ct));
}
