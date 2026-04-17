using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
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
public class AuthController(
    UserManager<User> userManager,
    SignInManager<User> signInManager,
    JwtService jwtService,
    JwtConfig jwtConfig,
    ITokenBlacklistService blacklist,
    AppDbContext db,
    IEmailService email,
    IConfiguration configuration,
    ILogger<AuthController> logger) : ControllerBase
{
    private readonly UserManager<User> _userManager = userManager;
    private readonly SignInManager<User> _signInManager = signInManager;
    private readonly JwtService _jwtService = jwtService;
    private readonly ILogger<AuthController> _logger = logger;
    private readonly JwtConfig _jwtConfig = jwtConfig;
    private readonly ITokenBlacklistService _blacklist = blacklist;
    private readonly AppDbContext _db = db;
    private readonly IEmailService _email = email;
    private readonly string _appUrl = configuration["AppUrl"] ?? "http://localhost:5173";

    [HttpPost("register")]
    [EnableRateLimiting("register-limit")]
    public async Task<ActionResult<AuthResponseDto>> Register(RegisterDto registerDto)
    {
        if (!ModelState.IsValid)
            return BadRequest(new ErrorResponseDto { Message = "Invalid registration data" });

        if (string.IsNullOrEmpty(registerDto.Email) || string.IsNullOrEmpty(registerDto.FullName))
            return BadRequest(new ErrorResponseDto { Message = "Full name and email are required" });

        if (registerDto.Password != registerDto.ConfirmPassword)
            return BadRequest(new ErrorResponseDto { Message = "Passwords do not match" });

        // Check if email already exists
        var existingUserByEmail = await _userManager.FindByEmailAsync(registerDto.Email);
        if (existingUserByEmail != null)
            return Conflict(new ErrorResponseDto { Message = "Email is already registered", Code = "DUPLICATE_EMAIL" });

        var user = new User
        {
            UserName = registerDto.Email, // Use email as username for Identity
            Email = registerDto.Email,
            FullName = registerDto.FullName
        };

        var result = await _userManager.CreateAsync(user, registerDto.Password);

        if (!result.Succeeded)
        {
            _logger.LogWarning("Failed to create user {Email}: {Errors}",
                registerDto.Email,
                string.Join(", ", result.Errors.Select(e => e.Description)));

            return BadRequest(new ErrorResponseDto { Message = string.Join(", ", result.Errors.Select(e => e.Description)) });
        }

        try
        {
            _logger.LogInformation("User {Email} created successfully", user.Email);

            var token = _jwtService.GenerateToken(user);
            SetAuthCookie(token, _jwtConfig.ExpiryInMinutes);

            return Ok(new AuthResponseDto
            {
                IsSuccess = true,
                Message = "User registered successfully",
                Email = user.Email,
                FullName = user.FullName,
                Roles = [user.Role.ToString()],
                Expiration = DateTimeOffset.UtcNow.AddMinutes(_jwtConfig.ExpiryInMinutes)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during user registration for {Email}", registerDto.Email);
            return StatusCode(500, new ErrorResponseDto { Message = "An error occurred during registration" });
        }
    }

    [HttpPost("login")]
    [EnableRateLimiting("login-limit")]
    public async Task<ActionResult<AuthResponseDto>> Login(LoginDto loginDto)
    {
        if (!ModelState.IsValid || string.IsNullOrEmpty(loginDto.Email) || string.IsNullOrEmpty(loginDto.Password))
            return BadRequest(new ErrorResponseDto { Message = "Email and password are required" });

        try
        {
            var user = await _userManager.FindByEmailAsync(loginDto.Email);

            if (user == null)
            {
                _logger.LogWarning("Login attempt with invalid email: {Email}", loginDto.Email);
                return Unauthorized(new ErrorResponseDto { Message = "Invalid email or password" });
            }

            var result = await _signInManager.CheckPasswordSignInAsync(user, loginDto.Password, false);

            if (!result.Succeeded)
            {
                _logger.LogWarning("Failed login attempt for user: {Email}", loginDto.Email);
                return Unauthorized(new ErrorResponseDto { Message = "Invalid email or password" });
            }

            _logger.LogInformation("User {Username} logged in successfully", user.UserName);

            var expiryMinutes = loginDto.RememberMe ? 60 * 24 * 30 : _jwtConfig.ExpiryInMinutes;
            var token = _jwtService.GenerateToken(user, expiryMinutes);
            SetAuthCookie(token, expiryMinutes);

            return Ok(new AuthResponseDto
            {
                IsSuccess = true,
                Message = "Login successful",
                Email = user.Email ?? string.Empty,
                FullName = user.FullName,
                Roles = [user.Role.ToString()],
                Expiration = DateTimeOffset.UtcNow.AddMinutes(expiryMinutes)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login for {Email}", loginDto.Email);
            return StatusCode(500, new ErrorResponseDto { Message = "An error occurred during login" });
        }
    }

    [Authorize]
    [HttpGet("profile")]
    public async Task<ActionResult<UserDto>> GetCurrentUser()
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new ErrorResponseDto { Message = "User not authenticated" });

            var user = await _userManager.FindByIdAsync(userId);

            if (user == null)
                return NotFound(new ErrorResponseDto { Message = "User not found" });

            return Ok(new UserDto
            {
                Id = user.Id,
                Email = user.Email ?? string.Empty,
                FullName = user.FullName,
                UserName = user.UserName ?? string.Empty,
                Roles = [user.Role.ToString()]
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching current user");
            return StatusCode(500, new ErrorResponseDto { Message = "An error occurred while fetching user information" });
        }
    }

    [Authorize]
    [HttpPost("logout")]
    public IActionResult Logout()
    {
        var jti = User.FindFirstValue(JwtRegisteredClaimNames.Jti);
        var expClaim = User.FindFirstValue(JwtRegisteredClaimNames.Exp);

        if (jti != null && long.TryParse(expClaim, out var expUnix))
        {
            var expiry = DateTimeOffset.FromUnixTimeSeconds(expUnix).UtcDateTime;
            _blacklist.Revoke(jti, expiry);
        }

        Response.Cookies.Delete("access_token");
        return NoContent();
    }

    [HttpPost("forgot-password")]
    [EnableRateLimiting("login-limit")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordDto dto)
    {
        // Always return 204 — never reveal whether the email exists
        var user = await _userManager.FindByEmailAsync(dto.Email);
        if (user == null)
        {
            _logger.LogInformation("Forgot-password request for unknown email {Email}", dto.Email);
            return NoContent();
        }

        // Invalidate any existing unused tokens for this user
        var existing = await _db.PasswordResetTokens
            .Where(t => t.UserId == user.Id && !t.Used && t.ExpiresAt > DateTimeOffset.UtcNow)
            .ToListAsync();
        foreach (var t in existing) t.Used = true;

        // Generate a cryptographically random token
        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

        _db.PasswordResetTokens.Add(new PasswordResetToken
        {
            UserId = user.Id,
            TokenHash = hash,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1),
            Used = false,
        });
        await _db.SaveChangesAsync();

        var resetLink = $"{_appUrl}/reset-password?token={Uri.EscapeDataString(rawToken)}";
        await _email.SendPasswordResetEmailAsync(user.Email!, user.FullName, resetLink);

        _logger.LogInformation("Password reset email sent to {Email}", user.Email);
        return NoContent();
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordDto dto)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(dto.Token)));

        var record = await _db.PasswordResetTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == hash && !t.Used && t.ExpiresAt > DateTimeOffset.UtcNow);

        if (record == null)
            return BadRequest(new ErrorResponseDto { Message = "This reset link is invalid or has expired.", Code = "INVALID_TOKEN" });

        var result = await _userManager.ResetPasswordAsync(
            record.User,
            await _userManager.GeneratePasswordResetTokenAsync(record.User),
            dto.NewPassword);

        if (!result.Succeeded)
            return BadRequest(new ErrorResponseDto { Message = string.Join(", ", result.Errors.Select(e => e.Description)) });

        record.Used = true;
        await _db.SaveChangesAsync();

        _logger.LogInformation("Password reset completed for user {UserId}", record.UserId);
        return NoContent();
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

    [Authorize]
    [HttpPut("profile")]
    public async Task<ActionResult<UserDto>> UpdateProfile(UpdateProfileDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(new ErrorResponseDto { Message = "Invalid profile data" });

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = await _userManager.FindByIdAsync(userId!);
        if (user == null)
            return NotFound(new ErrorResponseDto { Message = "User not found" });

        // Check email uniqueness if it changed
        if (!string.Equals(user.Email, dto.Email, StringComparison.OrdinalIgnoreCase))
        {
            var existing = await _userManager.FindByEmailAsync(dto.Email);
            if (existing != null)
                return Conflict(new ErrorResponseDto { Message = "Email is already in use", Code = "DUPLICATE_EMAIL" });

            user.Email = dto.Email;
            user.UserName = dto.Email;
            user.NormalizedEmail = dto.Email.ToUpperInvariant();
            user.NormalizedUserName = dto.Email.ToUpperInvariant();
        }

        user.FullName = dto.FullName;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            _logger.LogWarning("Profile update failed for {UserId}: {Errors}", userId,
                string.Join(", ", result.Errors.Select(e => e.Description)));
            return BadRequest(new ErrorResponseDto { Message = string.Join(", ", result.Errors.Select(e => e.Description)) });
        }

        return Ok(new UserDto
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            FullName = user.FullName,
            UserName = user.UserName ?? string.Empty,
            Roles = [user.Role.ToString()]
        });
    }

    [Authorize]
    [HttpPut("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(new ErrorResponseDto { Message = "Invalid password data" });

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = await _userManager.FindByIdAsync(userId!);
        if (user == null)
            return NotFound(new ErrorResponseDto { Message = "User not found" });

        var result = await _userManager.ChangePasswordAsync(user, dto.CurrentPassword, dto.NewPassword);
        if (!result.Succeeded)
        {
            _logger.LogWarning("Password change failed for {UserId}: {Errors}", userId,
                string.Join(", ", result.Errors.Select(e => e.Description)));
            return BadRequest(new ErrorResponseDto { Message = string.Join(", ", result.Errors.Select(e => e.Description)) });
        }

        // Revoke the current token so the user must log in again with the new password
        var jti = User.FindFirstValue(JwtRegisteredClaimNames.Jti);
        var expClaim = User.FindFirstValue(JwtRegisteredClaimNames.Exp);
        if (jti != null && long.TryParse(expClaim, out var expUnix))
        {
            var expiry = DateTimeOffset.FromUnixTimeSeconds(expUnix).UtcDateTime;
            _blacklist.Revoke(jti, expiry);
        }

        Response.Cookies.Delete("access_token");
        return NoContent();
    }
}
