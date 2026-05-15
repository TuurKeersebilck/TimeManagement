using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TimeManagementBackend.Data;
using TimeManagementBackend.Models;
using TimeManagementBackend.Models.DTOs;

namespace TimeManagementBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SetupController(
    UserManager<User> userManager,
    RoleManager<IdentityRole> roleManager,
    AppDbContext db,
    ILogger<SetupController> logger) : ControllerBase
{
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        var setupRequired = !await db.Users.AnyAsync(u => u.Role == UserRole.Admin);
        return Ok(new { setupRequired });
    }

    [HttpPost("complete")]
    public async Task<IActionResult> Complete(SetupDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(new ErrorResponseDto { Message = "Invalid setup data" });

        if (await db.Users.AnyAsync(u => u.Role == UserRole.Admin))
            return Conflict(new ErrorResponseDto { Message = "Setup has already been completed.", Code = "SETUP_ALREADY_COMPLETE" });

        if (!await roleManager.RoleExistsAsync("Admin"))
            await roleManager.CreateAsync(new IdentityRole("Admin"));
        if (!await roleManager.RoleExistsAsync("Employee"))
            await roleManager.CreateAsync(new IdentityRole("Employee"));

        var user = new User
        {
            UserName = dto.Email,
            Email = dto.Email,
            FullName = dto.FullName,
            EmailConfirmed = true,
            Role = UserRole.Admin,
        };

        var result = await userManager.CreateAsync(user, dto.Password);
        if (!result.Succeeded)
            return BadRequest(new ErrorResponseDto { Message = string.Join(", ", result.Errors.Select(e => e.Description)) });

        await userManager.AddToRoleAsync(user, "Admin");
        logger.LogInformation("Initial admin created via setup wizard: {Email}", dto.Email);

        return NoContent();
    }
}
