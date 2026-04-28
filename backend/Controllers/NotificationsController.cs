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
public class NotificationsController(
    INotificationService notificationService,
    UserManager<User> userManager) : ApiControllerBase(userManager)
{
    private readonly INotificationService _notificationService = notificationService;

    [HttpGet]
    public async Task<ActionResult<IList<NotificationDto>>> GetNotifications(CancellationToken ct)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        var notifications = await _notificationService.GetNotificationsAsync(user.Id, ct);
        return Ok(notifications);
    }

    [HttpGet("unread-count")]
    public async Task<ActionResult<UnreadCountDto>> GetUnreadCount(CancellationToken ct)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        var count = await _notificationService.GetUnreadCountAsync(user.Id, ct);
        return Ok(new UnreadCountDto { Count = count });
    }

    [HttpPut("{id:int}/read")]
    public async Task<IActionResult> MarkAsRead(int id, CancellationToken ct)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        await _notificationService.MarkAsReadAsync(id, user.Id, ct);
        return NoContent();
    }

    [HttpPut("read-all")]
    public async Task<IActionResult> MarkAllAsRead(CancellationToken ct)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        await _notificationService.MarkAllAsReadAsync(user.Id, ct);
        return NoContent();
    }
}
