using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TimeManagementBackend.Models;
using TimeManagementBackend.Models.DTOs;
using TimeManagementBackend.Services;

namespace TimeManagementBackend.Controllers;

[ApiController]
[Route("api/calendar")]
public class CalendarController(
    ICalendarService calendarService,
    UserManager<User> userManager,
    IConfiguration configuration) : ApiControllerBase(userManager)
{
    private readonly ICalendarService _calendarService = calendarService;

    [HttpGet("{token}/feed.ics")]
    [EnableRateLimiting("calendar-feed-limit")]
    public async Task<IActionResult> GetFeed(string token, CancellationToken ct)
    {
        var ics = await _calendarService.GetIcsContentAsync(token, ct);
        if (ics == null)
            return NotFound();

        return Content(ics, "text/calendar; charset=utf-8");
    }

    [HttpGet("token")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<ActionResult<CalendarTokenInfoDto>> GetToken(CancellationToken ct)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        var (hasToken, expiresAt) = await _calendarService.GetTokenInfoAsync(user.Id, ct);
        return Ok(new CalendarTokenInfoDto { HasToken = hasToken, ExpiresAt = expiresAt });
    }

    [HttpPost("token/regenerate")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<ActionResult<CalendarTokenDto>> RegenerateToken(CancellationToken ct)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        var (rawToken, expiresAt) = await _calendarService.RegenerateTokenAsync(user.Id, ct);
        return Ok(new CalendarTokenDto
        {
            FeedUrl = BuildFeedUrl(rawToken),
            ExpiresAt = expiresAt,
        });
    }

    private string BuildFeedUrl(string rawToken)
    {
        var backendUrl = configuration["BackendUrl"];
        if (string.IsNullOrEmpty(backendUrl))
            backendUrl = $"{Request.Scheme}://{Request.Host}";
        return $"{backendUrl}/api/calendar/{rawToken}/feed.ics";
    }
}
