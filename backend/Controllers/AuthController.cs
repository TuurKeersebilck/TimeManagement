using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
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

    public AuthController(
        UserManager<User> userManager,
        SignInManager<User> signInManager,
        JwtService jwtService,
        JwtConfig jwtConfig,
        ILogger<AuthController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _jwtService = jwtService;
        _jwtConfig = jwtConfig;
        _logger = logger;
    }

    [HttpPost("register")]
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

            return Ok(new AuthResponseDto
            {
                IsSuccess = true,
                Message = "User registered successfully",
                Token = token,
                Email = user.Email,
                FullName = user.FullName,
                Roles = [.. roles],
                Expiration = DateTime.Now.AddMinutes(_jwtConfig.ExpiryInMinutes)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during user registration for {Email}", registerDto.Email);
            return StatusCode(500, new ErrorResponseDto { Message = "An error occurred during registration" });
        }
    }

    [HttpPost("login")]
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

            // Generate JWT token
            var roles = await _userManager.GetRolesAsync(user);
            var token = _jwtService.GenerateToken(user, roles);

            return Ok(new AuthResponseDto
            {
                IsSuccess = true,
                Message = "Login successful",
                Token = token,
                Email = user.Email ?? string.Empty,
                FullName = user.FullName,
                Roles = [.. roles],
                Expiration = DateTime.Now.AddMinutes(_jwtConfig.ExpiryInMinutes)
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
            return StatusCode(500, new ErrorResponseDto { Message = "An error occurred while fetching user information" });
        }
    }
}
