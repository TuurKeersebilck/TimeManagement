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
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class VacationsController(
    IVacationService service,
    IAdminService adminService,
    INotificationService notificationService,
    UserManager<User> userManager) : ApiControllerBase(userManager)
{
    private readonly IVacationService _service = service;
    private readonly IAdminService _adminService = adminService;
    private readonly INotificationService _notificationService = notificationService;

    [HttpGet("balances")]
    public async Task<ActionResult<IEnumerable<VacationBalanceDto>>> GetBalances(
        [FromQuery] int? year,
        CancellationToken ct)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        var balances = await _service.GetMyBalancesAsync(user.Id, year, ct);
        return Ok(balances);
    }

    [HttpGet("date/{date}")]
    public async Task<ActionResult<VacationDayDto>> GetVacationForDate(DateOnly date, CancellationToken ct)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        var vacation = await _service.GetVacationForDateAsync(user.Id, date, ct);
        if (vacation == null) return NoContent();
        return Ok(vacation);
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

        var dateLabel = dto.Date.ToString("d MMM yyyy");
        await _notificationService.NotifyAdminsAsync(
            $"{user.FullName} planned a vacation on {dateLabel}", NotificationType.Vacation, ct);

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

        if (result.Created.Any())
        {
            var startLabel = dto.StartDate.ToString("d MMM yyyy");
            var endLabel = dto.EndDate.ToString("d MMM yyyy");
            var message = dto.StartDate == dto.EndDate
                ? $"{user.FullName} planned a vacation on {startLabel}"
                : $"{user.FullName} planned a vacation from {startLabel} until {endLabel}";
            await _notificationService.NotifyAdminsAsync(message, NotificationType.Vacation, ct);
        }

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

    // ─── Admin: manage vacation days on behalf of an employee ─────────────────

    [HttpGet("employees/{userId}/balances")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IEnumerable<VacationBalanceDto>>> GetEmployeeBalances(
        string userId, [FromQuery] int? year, CancellationToken ct)
    {
        var balances = await _service.GetMyBalancesAsync(userId, year, ct);
        return Ok(balances);
    }

    [HttpGet("employees/{userId}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IEnumerable<VacationDayDto>>> GetEmployeeVacationDays(string userId, CancellationToken ct)
    {
        var days = await _service.GetMyVacationDaysAsync(userId, ct);
        return Ok(days);
    }

    [HttpPost("employees/{userId}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<VacationDayDto>> CreateEmployeeVacationDay(
        string userId, [FromBody] CreateVacationDayDto dto, CancellationToken ct)
    {
        _ = await UserManager.FindByIdAsync(userId)
            ?? throw new ResourceNotFoundException("Employee not found.");

        if (await _service.ExistsForDateAndTypeAsync(userId, dto.Date, dto.VacationTypeId, ct))
            return Conflict(new ErrorResponseDto { Message = "A vacation day of this type already exists for this date", Code = "DUPLICATE_DATE" });

        var created = await _service.CreateVacationDayAsync(userId, dto, ct);

        var dateLabel = dto.Date.ToString("d MMM yyyy");
        await _notificationService.NotifyUserAsync(
            userId, $"An admin planned a vacation for you on {dateLabel}", NotificationType.Vacation, ct);

        return CreatedAtAction(nameof(GetEmployeeVacationDays), new { userId }, created);
    }

    [HttpPut("employees/{userId}/{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<VacationDayDto>> UpdateEmployeeVacationDay(
        string userId, int id, [FromBody] CreateVacationDayDto dto, CancellationToken ct)
    {
        var updated = await _service.UpdateVacationDayAsync(userId, id, dto, ct);

        var dateLabel = dto.Date.ToString("d MMM yyyy");
        await _notificationService.NotifyUserAsync(
            userId, $"An admin updated your vacation on {dateLabel}", NotificationType.Vacation, ct);

        return Ok(updated);
    }

    [HttpPost("employees/{userId}/range")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<VacationRangeResultDto>> CreateEmployeeVacationRange(
        string userId, [FromBody] CreateVacationRangeDto dto, CancellationToken ct)
    {
        _ = await UserManager.FindByIdAsync(userId)
            ?? throw new ResourceNotFoundException("Employee not found.");

        var result = await _service.CreateVacationRangeAsync(userId, dto, ct);

        if (result.Created.Any())
        {
            var startLabel = dto.StartDate.ToString("d MMM yyyy");
            var endLabel = dto.EndDate.ToString("d MMM yyyy");
            var message = dto.StartDate == dto.EndDate
                ? $"An admin planned a vacation for you on {startLabel}"
                : $"An admin planned a vacation for you from {startLabel} until {endLabel}";
            await _notificationService.NotifyUserAsync(userId, message, NotificationType.Vacation, ct);
        }

        return Ok(result);
    }

    [HttpDelete("employees/{userId}/{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteEmployeeVacationDay(string userId, int id, CancellationToken ct)
    {
        var existing = (await _service.GetMyVacationDaysAsync(userId, ct))
            .FirstOrDefault(d => d.Id == id);

        await _service.DeleteVacationDayAsync(userId, id, ct);

        if (existing != null)
        {
            var dateLabel = existing.Date.ToString("d MMM yyyy");
            await _notificationService.NotifyUserAsync(
                userId, $"An admin removed your vacation on {dateLabel}", NotificationType.Vacation, ct);
        }

        return NoContent();
    }

    // ─── Team endpoints (all authenticated users) ─────────────────────────────

    [HttpGet("team")]
    public async Task<ActionResult<IEnumerable<AdminVacationDayDto>>> GetTeamVacationDays(
        [FromQuery] int? year,
        [FromQuery] int? month,
        CancellationToken ct)
    {
        var days = await _adminService.GetAllVacationDaysAsync(null, null, year, month, ct);

        // Non-admins may only see who is off and when — not the vacation type or note
        if (!User.IsInRole("Admin"))
        {
            days = days.Select(d => new AdminVacationDayDto
            {
                Id = d.Id,
                UserId = d.UserId,
                EmployeeName = d.EmployeeName,
                Date = d.Date,
                Amount = d.Amount,
            });
        }

        return Ok(days);
    }

    [HttpGet("types")]
    public async Task<ActionResult<IEnumerable<VacationTypeDto>>> GetVacationTypes(CancellationToken ct)
    {
        var types = await _adminService.GetVacationTypesAsync(ct);
        return Ok(types);
    }
}
