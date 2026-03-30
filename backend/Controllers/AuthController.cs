using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using TimeManagementBackend.Config;
using TimeManagementBackend.Models;
using TimeManagementBackend.Models.DTOs;
using TimeManagementBackend.Services;

namespace TimeManagementBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<User> _userManager;
    private readonly SignInManager<User> _signInManager;
    private readonly JwtService _jwtService;
    private readonly ILogger<AuthController> _logger;
    private readonly JwtConfig _jwtConfig;
    private readonly ITokenBlacklistService _blacklist;

    public AuthController(
        UserManager<User> userManager,
        SignInManager<User> signInManager,
        JwtService jwtService,
        JwtConfig jwtConfig,
        ITokenBlacklistService blacklist,
        ILogger<AuthController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _jwtService = jwtService;
        _jwtConfig = jwtConfig;
        _blacklist = blacklist;
        _logger = logger;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponseDto>> Register(RegisterDto registerDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new AuthResponseDto
            {
                IsSuccess = false,
                Message = "Invalid registration data"
            });
        }

        if (string.IsNullOrEmpty(registerDto.Email) || string.IsNullOrEmpty(registerDto.FullName))
        {
            return BadRequest(new AuthResponseDto
            {
                IsSuccess = false,
                Message = "Full name and email are required"
            });
        }

        if (registerDto.Password != registerDto.ConfirmPassword)
        {
            return BadRequest(new AuthResponseDto
            {
                IsSuccess = false,
                Message = "Passwords do not match"
            });
        }

        // Check if email already exists
        var existingUserByEmail = await _userManager.FindByEmailAsync(registerDto.Email);
        if (existingUserByEmail != null)
        {
            return Conflict(new AuthResponseDto
            {
                IsSuccess = false,
                Message = "Email is already registered"
            });
        }

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

            return BadRequest(new AuthResponseDto
            {
                IsSuccess = false,
                Message = string.Join(", ", result.Errors.Select(e => e.Description))
            });
        }

        try
        {
            // Add to default role (User)
            var roleResult = await _userManager.AddToRoleAsync(user, "User");
            if (!roleResult.Succeeded)
            {
                _logger.LogWarning("Failed to add user {Email} to role: {Errors}",
                    user.Email,
                    string.Join(", ", roleResult.Errors.Select(e => e.Description)));
            }

            _logger.LogInformation("User {Email} created successfully", user.Email);

            // Generate JWT token
            var roles = await _userManager.GetRolesAsync(user);
            var token = _jwtService.GenerateToken(user, roles);
            SetAuthCookie(token);

            return Ok(new AuthResponseDto
            {
                IsSuccess = true,
                Message = "User registered successfully",
                Email = user.Email,
                FullName = user.FullName,
                Roles = [.. roles],
                Expiration = DateTime.Now.AddMinutes(_jwtConfig.ExpiryInMinutes)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during user registration for {Email}", registerDto.Email);
            return StatusCode(500, new AuthResponseDto
            {
                IsSuccess = false,
                Message = "An error occurred during registration"
            });
        }
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponseDto>> Login(LoginDto loginDto)
    {
        if (!ModelState.IsValid || string.IsNullOrEmpty(loginDto.Email) || string.IsNullOrEmpty(loginDto.Password))
        {
            return BadRequest(new AuthResponseDto
            {
                IsSuccess = false,
                Message = "Email and password are required"
            });
        }

        try
        {
            var user = await _userManager.FindByEmailAsync(loginDto.Email);

            if (user == null)
            {
                _logger.LogWarning("Login attempt with invalid email: {Email}", loginDto.Email);
                return Unauthorized(new AuthResponseDto
                {
                    IsSuccess = false,
                    Message = "Invalid email or password"
                });
            }

            var result = await _signInManager.CheckPasswordSignInAsync(user, loginDto.Password, false);

            if (!result.Succeeded)
            {
                _logger.LogWarning("Failed login attempt for user: {Email}", loginDto.Email);
                return Unauthorized(new AuthResponseDto
                {
                    IsSuccess = false,
                    Message = "Invalid email or password"
                });
            }

            _logger.LogInformation("User {Username} logged in successfully", user.UserName);

            // Generate JWT token and set as HttpOnly cookie
            var roles = await _userManager.GetRolesAsync(user);
            var token = _jwtService.GenerateToken(user, roles);
            SetAuthCookie(token);

            return Ok(new AuthResponseDto
            {
                IsSuccess = true,
                Message = "Login successful",
                Email = user.Email ?? string.Empty,
                FullName = user.FullName,
                Roles = [.. roles],
                Expiration = DateTime.Now.AddMinutes(_jwtConfig.ExpiryInMinutes)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login for {Email}", loginDto.Email);
            return StatusCode(500, new AuthResponseDto
            {
                IsSuccess = false,
                Message = "An error occurred during login"
            });
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
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var user = await _userManager.FindByIdAsync(userId);
            
            if (user == null)
            {
                return NotFound(new { message = "User not found" });
            }

            var roles = await _userManager.GetRolesAsync(user);

            return Ok(new UserDto
            {
                Id = user.Id,
                Email = user.Email ?? string.Empty,
                FullName = user.FullName,
                UserName = user.UserName ?? string.Empty,
                Roles = [.. roles]
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching current user");
            return StatusCode(500, new { message = "An error occurred while fetching user information" });
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

    private void SetAuthCookie(string token)
    {
        var isDev = HttpContext.RequestServices
            .GetRequiredService<IWebHostEnvironment>().IsDevelopment();

        Response.Cookies.Append("access_token", token, new CookieOptions
        {
            HttpOnly = true,
            Secure = !isDev,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddMinutes(_jwtConfig.ExpiryInMinutes)
        });
    }

    [Authorize]
    [HttpPut("profile")]
    public async Task<ActionResult<UserDto>> UpdateProfile(UpdateProfileDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { message = "Invalid profile data" });

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = await _userManager.FindByIdAsync(userId!);
        if (user == null)
            return NotFound(new { message = "User not found" });

        // Check email uniqueness if it changed
        if (!string.Equals(user.Email, dto.Email, StringComparison.OrdinalIgnoreCase))
        {
            var existing = await _userManager.FindByEmailAsync(dto.Email);
            if (existing != null)
                return Conflict(new { message = "Email is already in use" });

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
            return BadRequest(new { message = string.Join(", ", result.Errors.Select(e => e.Description)) });
        }

        var roles = await _userManager.GetRolesAsync(user);
        return Ok(new UserDto
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            FullName = user.FullName,
            UserName = user.UserName ?? string.Empty,
            Roles = [.. roles]
        });
    }

    [Authorize]
    [HttpPut("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { message = "Invalid password data" });

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = await _userManager.FindByIdAsync(userId!);
        if (user == null)
            return NotFound(new { message = "User not found" });

        var result = await _userManager.ChangePasswordAsync(user, dto.CurrentPassword, dto.NewPassword);
        if (!result.Succeeded)
        {
            _logger.LogWarning("Password change failed for {UserId}: {Errors}", userId,
                string.Join(", ", result.Errors.Select(e => e.Description)));
            return BadRequest(new { message = string.Join(", ", result.Errors.Select(e => e.Description)) });
        }

        return NoContent();
    }
}