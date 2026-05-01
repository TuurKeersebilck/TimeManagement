using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using TimeManagementBackend.Config;
using TimeManagementBackend.Data;
using TimeManagementBackend.Models;
using TimeManagementBackend.Models.DTOs;
using TimeManagementBackend.Services;

namespace TimeManagementBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InvitesController(
    AppDbContext db,
    UserManager<User> userManager,
    JwtService jwtService,
    JwtConfig jwtConfig,
    IEmailService email,
    IConfiguration configuration,
    ILogger<InvitesController> logger) : ControllerBase
{
    private readonly string _appUrl = configuration["AppUrl"] ?? "http://localhost:5173";

    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]
    [HttpPost]
    public async Task<ActionResult<InviteDto>> CreateInvite(CreateInviteDto dto, CancellationToken ct)
    {
        var existingUser = await userManager.FindByEmailAsync(dto.Email);
        if (existingUser != null)
            return Conflict(new ErrorResponseDto { Message = "An account with this email already exists.", Code = "DUPLICATE_EMAIL" });

        // Invalidate any existing pending invite for this email
        var existing = await db.EmployeeInvites
            .Where(i => i.Email == dto.Email && !i.Used && i.ExpiresAt > DateTimeOffset.UtcNow)
            .ToListAsync(ct);
        foreach (var inv in existing) inv.Used = true;

        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

        var invite = new EmployeeInvite
        {
            Email = dto.Email,
            TokenHash = hash,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(48),
            Used = false,
            CreatedByUserId = adminId,
        };
        db.EmployeeInvites.Add(invite);
        await db.SaveChangesAsync(ct);

        var inviteLink = $"{_appUrl}/register?token={Uri.EscapeDataString(rawToken)}";
        try
        {
            await email.SendInviteEmailAsync(dto.Email, inviteLink);
        }
        catch
        {
            db.EmployeeInvites.Remove(invite);
            await db.SaveChangesAsync(ct);
            throw;
        }

        logger.LogInformation("Invite sent to {Email} by admin {AdminId}", dto.Email, adminId);
        return Ok(new InviteDto
        {
            Id = invite.Id,
            Email = invite.Email,
            CreatedAt = invite.CreatedAt,
            ExpiresAt = invite.ExpiresAt,
        });
    }

    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<InviteDto>>> GetPendingInvites(CancellationToken ct)
    {
        var invites = await db.EmployeeInvites
            .AsNoTracking()
            .Where(i => !i.Used && i.ExpiresAt > DateTimeOffset.UtcNow)
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new InviteDto
            {
                Id = i.Id,
                Email = i.Email,
                CreatedAt = i.CreatedAt,
                ExpiresAt = i.ExpiresAt,
            })
            .ToListAsync(ct);

        return Ok(invites);
    }

    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> CancelInvite(int id, CancellationToken ct)
    {
        var invite = await db.EmployeeInvites
            .FirstOrDefaultAsync(i => i.Id == id && !i.Used, ct);
        if (invite == null)
            return NotFound(new ErrorResponseDto { Message = "Invite not found." });

        invite.Used = true;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpGet("validate")]
    public async Task<ActionResult<ValidateInviteResponseDto>> ValidateToken([FromQuery] string token, CancellationToken ct)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        var invite = await db.EmployeeInvites
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.TokenHash == hash && !i.Used && i.ExpiresAt > DateTimeOffset.UtcNow, ct);

        if (invite == null)
            return BadRequest(new ErrorResponseDto { Message = "This invitation link is invalid or has expired.", Code = "INVALID_TOKEN" });

        return Ok(new ValidateInviteResponseDto { Email = invite.Email });
    }

    [HttpPost("accept")]
    [EnableRateLimiting("invite-accept-limit")]
    public async Task<ActionResult<AuthResponseDto>> AcceptInvite(AcceptInviteDto dto, CancellationToken ct)
    {
        if (dto.Password != dto.ConfirmPassword)
            return BadRequest(new ErrorResponseDto { Message = "Passwords do not match." });

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(dto.Token)));
        var invite = await db.EmployeeInvites
            .FirstOrDefaultAsync(i => i.TokenHash == hash && !i.Used && i.ExpiresAt > DateTimeOffset.UtcNow, ct);

        if (invite == null)
            return BadRequest(new ErrorResponseDto { Message = "This invitation link is invalid or has expired.", Code = "INVALID_TOKEN" });

        var existingUser = await userManager.FindByEmailAsync(invite.Email);
        if (existingUser != null)
            return Conflict(new ErrorResponseDto { Message = "An account with this email already exists.", Code = "DUPLICATE_EMAIL" });

        var user = new User
        {
            UserName = invite.Email,
            Email = invite.Email,
            FullName = dto.FullName,
        };

        var result = await userManager.CreateAsync(user, dto.Password);
        if (!result.Succeeded)
            return BadRequest(new ErrorResponseDto { Message = string.Join(", ", result.Errors.Select(e => e.Description)) });

        invite.Used = true;
        await db.SaveChangesAsync(ct);

        logger.LogInformation("User {Email} accepted invite and created account", invite.Email);

        var jwtToken = jwtService.GenerateToken(user);
        SetAuthCookie(jwtToken, jwtConfig.ExpiryInMinutes);

        return Ok(new AuthResponseDto
        {
            IsSuccess = true,
            Message = "Account created successfully",
            Email = user.Email!,
            FullName = user.FullName,
            Roles = [user.Role.ToString()],
            Expiration = DateTimeOffset.UtcNow.AddMinutes(jwtConfig.ExpiryInMinutes),
        });
    }

    private void SetAuthCookie(string token, int expiryMinutes)
    {
        var isDev = HttpContext.RequestServices
            .GetRequiredService<IWebHostEnvironment>().IsDevelopment();

        Response.Cookies.Append("access_token", token, new CookieOptions
        {
            HttpOnly = true,
            Secure = !isDev,
            SameSite = isDev ? SameSiteMode.Lax : SameSiteMode.None,
            Expires = DateTimeOffset.UtcNow.AddMinutes(expiryMinutes)
        });
    }
}
