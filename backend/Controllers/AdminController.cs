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
        CancellationToken ct)
    {
        var logs = await _adminService.GetAllTimeLogsAsync(userId, ct);
        return Ok(logs);
    }

    [HttpGet("employees")]
    public async Task<ActionResult<IEnumerable<EmployeeDto>>> GetEmployees(CancellationToken ct)
    {
        var employees = await _adminService.GetEmployeesAsync(ct);
        return Ok(employees);
    }
}
