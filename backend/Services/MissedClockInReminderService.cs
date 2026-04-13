using Microsoft.EntityFrameworkCore;
using TimeManagementBackend.Data;
using TimeManagementBackend.Models;

namespace TimeManagementBackend.Services;

/// <summary>
/// Runs daily at 08:00 server time and emails any employee
/// who had no clock-in event the previous working day.
/// </summary>
public class MissedClockInReminderService(
    IServiceScopeFactory scopeFactory,
    ILogger<MissedClockInReminderService> logger) : BackgroundService
{
    private DateOnly _lastRunDate = DateOnly.MinValue;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.Now;

            // Run once per day at 08:00
            if (now.Hour >= 8 && DateOnly.FromDateTime(now) != _lastRunDate)
            {
                _lastRunDate = DateOnly.FromDateTime(now);
                await SendRemindersAsync(stoppingToken);
            }

            // Check again in 15 minutes
            await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
        }
    }

    private async Task SendRemindersAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

            var yesterday = await GetPreviousWorkingDayAsync(DateOnly.FromDateTime(DateTime.Now), db, ct);
            if (yesterday == null)
            {
                logger.LogInformation("MissedClockInReminder: skipped — no previous working day resolved.");
                return;
            }

            var targetDate = yesterday.Value;

            // Find users who had no ClockIn for that date
            var usersWithClockIn = await db.ClockEvents
                .Where(e => e.Date == targetDate && e.Type == ClockEventType.ClockIn)
                .Select(e => e.UserId)
                .ToListAsync(ct);

            var allEmployees = await db.Users
                .Where(u => u.Role == UserRole.Employee && u.Email != null)
                .ToListAsync(ct);

            var missingUsers = allEmployees
                .Where(u => !usersWithClockIn.Contains(u.Id))
                .ToList();

            foreach (var user in missingUsers)
            {
                try
                {
                    await emailService.SendMissedClockInReminderAsync(user.Email!, user.FullName, targetDate);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to send missed clock-in reminder to {UserId}", user.Id);
                }
            }

            logger.LogInformation(
                "MissedClockInReminder: sent {Count} reminder(s) for {Date}.",
                missingUsers.Count, targetDate);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "MissedClockInReminderService encountered an error.");
        }
    }

    /// <summary>
    /// Returns the most recent working day before today, skipping weekends and public holidays.
    /// </summary>
    private static async Task<DateOnly?> GetPreviousWorkingDayAsync(DateOnly today, AppDbContext db, CancellationToken ct)
    {
        var candidate = today.AddDays(-1);

        for (var i = 0; i < 10; i++)
        {
            if (candidate.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            {
                candidate = candidate.AddDays(-1);
                continue;
            }

            var isHoliday = await db.PublicHolidays.AnyAsync(h => h.Date == candidate, ct);
            if (isHoliday)
            {
                candidate = candidate.AddDays(-1);
                continue;
            }

            return candidate;
        }

        return null;
    }
}
