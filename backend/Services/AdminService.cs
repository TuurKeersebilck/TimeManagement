using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TimeManagementBackend.Data;
using TimeManagementBackend.Models;
using TimeManagementBackend.Models.DTOs;

namespace TimeManagementBackend.Services;

public class AdminService(AppDbContext context, UserManager<User> userManager) : IAdminService
{
    private readonly AppDbContext _context = context;
    private readonly UserManager<User> _userManager = userManager;

    public async Task<IEnumerable<AdminTimeLogDto>> GetAllTimeLogsAsync(string? userId = null, CancellationToken ct = default)
    {
        var query = _context.TimeLogs
            .AsNoTracking()
            .Join(
                _context.Users,
                log => log.UserId,
                user => user.Id,
                (log, user) => new { log, user }
            );

        if (!string.IsNullOrEmpty(userId))
            query = query.Where(x => x.log.UserId == userId);

        var results = await query
            .OrderByDescending(x => x.log.Date)
            .Select(x => new AdminTimeLogDto
            {
                Id = x.log.Id,
                UserId = x.log.UserId!,
                EmployeeName = x.user.FullName,
                EmployeeEmail = x.user.Email!,
                Date = x.log.Date,
                StartTime = x.log.StartTime,
                EndTime = x.log.EndTime,
                BreakStart = x.log.BreakStart,
                BreakEnd = x.log.BreakEnd,
                Description = x.log.Description,
            })
            .ToListAsync(ct);

        return results;
    }

    public async Task<IEnumerable<EmployeeDto>> GetEmployeesAsync(CancellationToken ct = default)
    {
        var employees = await _userManager.GetUsersInRoleAsync("User");

        return employees
            .OrderBy(u => u.FullName)
            .Select(u => new EmployeeDto
            {
                Id = u.Id,
                FullName = u.FullName,
                Email = u.Email!,
            });
    }
}
