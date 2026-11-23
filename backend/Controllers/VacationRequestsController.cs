using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TimeManagementBackend.Models;
using TimeManagementBackend.Models.DTOs;
using TimeManagementBackend.Services;

namespace TimeManagementBackend.Controllers;

/// <summary>Vacation requests: employees plan drafts and submit them, admins approve or deny.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class VacationRequestsController(
    IVacationRequestService service,
    UserManager<User> userManager) : ApiControllerBase(userManager)
{
    [HttpPost]
    public async Task<ActionResult<VacationRequestDto>> Create([FromBody] CreateVacationRequestDto dto, CancellationToken ct)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        return Ok(await service.CreateAsync(user.Id, dto, ct));
    }

    [HttpGet("mine")]
    public async Task<ActionResult<IEnumerable<VacationRequestDto>>> GetMine(CancellationToken ct)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        return Ok(await service.GetMineAsync(user.Id, ct));
    }

    /// <summary>Removes a draft (planned, not sent yet).</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteDraft(int id, CancellationToken ct)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        await service.DeleteDraftAsync(user.Id, id, ct);
        return NoContent();
    }

    /// <summary>Sends all of the caller's drafts to the admin for review.</summary>
    [HttpPost("submit")]
    public async Task<ActionResult<IEnumerable<VacationRequestDto>>> Submit(CancellationToken ct)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        return Ok(await service.SubmitAsync(user.Id, ct));
    }

    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, CancellationToken ct)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        await service.CancelAsync(user.Id, id, ct);
        return NoContent();
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IEnumerable<VacationRequestDto>>> GetAll(CancellationToken ct)
        => Ok(await service.GetAllAsync(ct));

    [HttpGet("pending-count")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<object>> GetPendingCount(CancellationToken ct)
        => Ok(new { count = await service.GetPendingCountAsync(ct) });

    [HttpPost("{id:int}/approve")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<VacationRangeResultDto>> Approve(int id, CancellationToken ct)
    {
        var admin = await GetCurrentUserAsync();
        if (admin == null) return Unauthorized();

        return Ok(await service.ApproveAsync(id, admin.Id, ct));
    }

    [HttpPost("{id:int}/reject")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Reject(int id, [FromBody] RejectVacationRequestDto? dto, CancellationToken ct)
    {
        var admin = await GetCurrentUserAsync();
        if (admin == null) return Unauthorized();

        await service.RejectAsync(id, admin.Id, dto?.Reason, ct);
        return NoContent();
    }
}
