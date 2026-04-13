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
public class TimeAdjustmentRequestsController(
    ITimeAdjustmentRequestService service,
    IConfiguration configuration,
    UserManager<User> userManager) : ApiControllerBase(userManager)
{
    [HttpPost]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<ActionResult<AdjustmentRequestDto>> Create(
        [FromBody] CreateAdjustmentRequestDto dto,
        CancellationToken ct)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        var backendUrl = BuildBackendUrl();
        var result = await service.CreateRequestAsync(user.Id, dto, backendUrl, ct);
        return Ok(result);
    }

    [HttpGet]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]
    public async Task<ActionResult<IEnumerable<AdjustmentRequestDto>>> GetAll(CancellationToken ct)
    {
        return Ok(await service.GetAllRequestsAsync(ct));
    }

    /// <summary>
    /// One-click approve link from admin email. Returns an HTML confirmation page.
    /// No authentication required — the token itself is the credential.
    /// </summary>
    [HttpGet("approve/{token}")]
    [AllowAnonymous]
    public async Task<ContentResult> Approve(string token, CancellationToken ct)
    {
        var message = await service.ApproveAsync(token, ct);

        var html = $$"""
            <!doctype html>
            <html lang="en">
            <head>
              <meta charset="utf-8" />
              <meta name="viewport" content="width=device-width, initial-scale=1" />
              <title>Time Adjustment</title>
              <style>
                body { font-family: sans-serif; display: flex; align-items: center; justify-content: center;
                        min-height: 100vh; margin: 0; background: #f8fafc; }
                .card { background: #fff; border-radius: 12px; padding: 40px 48px;
                         box-shadow: 0 4px 24px rgba(0,0,0,.08); max-width: 420px; text-align: center; }
                h2 { color: #1e293b; margin-top: 0; }
                p { color: #475569; line-height: 1.6; }
              </style>
            </head>
            <body>
              <div class="card">
                <h2>Time Adjustment</h2>
                <p>{{System.Net.WebUtility.HtmlEncode(message)}}</p>
              </div>
            </body>
            </html>
            """;

        return Content(html, "text/html");
    }

    private string BuildBackendUrl()
    {
        // Prefer an explicit BACKEND_URL env var; fall back to the current request's origin
        var configured = configuration["BackendUrl"];
        if (!string.IsNullOrEmpty(configured))
            return configured;

        return $"{Request.Scheme}://{Request.Host}";
    }
}
