using Microsoft.EntityFrameworkCore;
using TimeManagementBackend.Data;
using TimeManagementBackend.Helpers;
using TimeManagementBackend.Models;

namespace TimeManagementBackend.Services;

// NOTE: If this backend is ever scaled to multiple replicas, each replica will run these jobs
// independently. A distributed lock (e.g. Postgres advisory lock) would be needed to prevent
// duplicate sends and double-invalidation.
public class MissedClockInReminderService(
    IServiceScopeFactory scopeFactory,
    ILogger<MissedClockInReminderService> logger) : BackgroundService
{
    private DateOnly _lastRunDate = DateOnly.MinValue;
    private DateOnly _lastCalendarExpiryWeekStart = DateOnly.MinValue;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Auto-invalidation runs on every pass, not just the daily one.
            await AutoInvalidateExpiredSessionsAsync(stoppingToken);

            var now = DateTime.UtcNow;
            if (now.Hour >= 8 && DateOnly.FromDateTime(now) != _lastRunDate)
            {
                _lastRunDate = DateOnly.FromDateTime(now);
                await SendRemindersAsync(stoppingToken);

                // Calendar token expiry only needs a weekly check — the 14-day warning
                // window comfortably absorbs the up-to-6-day gap between weekly runs.
                var currentWeekStart = TimeCalculationHelper.GetCurrentWeekBounds().Start;
                if (currentWeekStart != _lastCalendarExpiryWeekStart)
                {
                    _lastCalendarExpiryWeekStart = currentWeekStart;
                    await SendCalendarTokenExpiryRemindersAsync(stoppingToken);
                }

                if (now.Day == 1)
                    await GenerateMonthlySettlementsAsync(stoppingToken);
            }

            await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
        }
    }

    // ── Auto-invalidation ─────────────────────────────────────────────────────

    private async Task AutoInvalidateExpiredSessionsAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

            var config = await db.AppConfigurations.FirstOrDefaultAsync(ct);
            var maxHours = config?.MaxSessionHours ?? 13m;
            var cutoff = DateTimeOffset.UtcNow.AddHours(-(double)maxHours);

            var expiredSessions = await db.WorkSessions
                .Include(s => s.Breaks)
                .Where(s => s.Status == WorkSessionStatus.Open && s.ClockIn < cutoff)
                .ToListAsync(ct);

            if (expiredSessions.Count == 0)
                return;

            var invalidatedAt = DateTimeOffset.UtcNow;

            foreach (var session in expiredSessions)
            {
                session.Status = WorkSessionStatus.Invalidated;

                var openBreak = session.Breaks.FirstOrDefault(b => b.BreakEnd == null);
                if (openBreak != null)
                {
                    openBreak.BreakEnd = invalidatedAt;
                    openBreak.BreakEndServerStamp = invalidatedAt;
                }

                var msg = $"Your session from {session.ClockIn.ToUniversalTime():HH:mm UTC} on " +
                          $"{session.Date:d MMM yyyy} was auto-closed after {maxHours}h. " +
                          "Submit an adjustment request to record your actual hours.";
                try
                {
                    await notificationService.NotifyUserAsync(
                        session.UserId, msg, NotificationType.SessionInvalidated, ct);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex,
                        "Failed to send SessionInvalidated notification to {UserId} for session {SessionId}.",
                        session.UserId, session.Id);
                }
            }

            await db.SaveChangesAsync(ct);

            logger.LogInformation(
                "AutoInvalidate: invalidated {Count} expired session(s).", expiredSessions.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "AutoInvalidateExpiredSessionsAsync encountered an error.");
        }
    }

    // ── Missed clock-in reminder ──────────────────────────────────────────────

    private async Task SendRemindersAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

            var config = await db.AppConfigurations.FirstOrDefaultAsync(ct);
            if (!(config?.EnableMissedClockInEmails ?? true))
            {
                logger.LogInformation("MissedClockInReminder: skipped — disabled in app settings.");
                return;
            }

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            if (TimeCalculationHelper.IsWeekend(today))
            {
                logger.LogInformation("MissedClockInReminder: skipped — today is a weekend.");
                return;
            }

            var isTodayHoliday = await db.PublicHolidays.AnyAsync(h => h.Date == today && !h.IsWorkingDay, ct);
            if (isTodayHoliday)
            {
                logger.LogInformation("MissedClockInReminder: skipped — today is a public holiday.");
                return;
            }

            var yesterday = await GetPreviousWorkingDayAsync(today, db, ct);
            if (yesterday == null)
            {
                logger.LogInformation("MissedClockInReminder: skipped — no previous working day resolved.");
                return;
            }

            var targetDate = yesterday.Value;

            // Users who have at least one non-Invalidated WorkSession for that date.
            var usersWithSession = await db.WorkSessions
                .Where(s => s.Date == targetDate && s.Status != WorkSessionStatus.Invalidated)
                .Select(s => s.UserId)
                .Distinct()
                .ToListAsync(ct);

            var usersOnVacation = await db.VacationDays
                .Where(v => v.Date == targetDate && v.Amount >= 1.0m)
                .Select(v => v.UserId)
                .ToListAsync(ct);

            var allEmployees = await db.Users
                .Where(u => u.Role == UserRole.Employee && u.Email != null && !u.IsDisabled)
                .ToListAsync(ct);

            var missingUsers = allEmployees
                .Where(u => !usersWithSession.Contains(u.Id) && !usersOnVacation.Contains(u.Id))
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

    // ── Calendar token expiry reminder ────────────────────────────────────────

    private const int CalendarTokenExpiryWarningDays = 14;

    private async Task SendCalendarTokenExpiryRemindersAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

            var now = DateTimeOffset.UtcNow;
            var warningCutoff = now.AddDays(CalendarTokenExpiryWarningDays);

            var expiringUsers = await db.Users
                .Where(u => u.CalendarTokenHash != null
                         && u.CalendarTokenExpiresAt != null
                         && u.CalendarTokenExpiresAt > now
                         && u.CalendarTokenExpiresAt <= warningCutoff
                         && u.CalendarTokenExpiryNotifiedAt == null
                         && u.Email != null)
                .ToListAsync(ct);

            foreach (var user in expiringUsers)
            {
                try
                {
                    await emailService.SendCalendarTokenExpiringEmailAsync(
                        user.Email!, user.FullName, user.CalendarTokenExpiresAt!.Value);
                    user.CalendarTokenExpiryNotifiedAt = now;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to send calendar-token-expiring email to {UserId}", user.Id);
                }
            }

            await db.SaveChangesAsync(ct);

            logger.LogInformation(
                "CalendarTokenExpiryReminder: sent {Count} reminder(s).", expiringUsers.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SendCalendarTokenExpiryRemindersAsync encountered an error.");
        }
    }

    // ── Month-end settlement generation ───────────────────────────────────────

    private async Task GenerateMonthlySettlementsAsync(CancellationToken ct)
    {
        try
        {
            var now = DateTime.UtcNow;
            // Generate for the prior month
            var priorMonth = new DateTime(now.Year, now.Month, 1).AddMonths(-1);
            var year = priorMonth.Year;
            var month = priorMonth.Month;

            using var scope = scopeFactory.CreateScope();
            var settlementService = scope.ServiceProvider.GetRequiredService<ISettlementService>();

            logger.LogInformation("Generating MonthlySettlements for {Year}-{Month:00}.", year, month);
            await settlementService.GenerateForAllEmployeesAsync(year, month, ct);
            logger.LogInformation("MonthlySettlement generation complete for {Year}-{Month:00}.", year, month);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "GenerateMonthlySettlementsAsync encountered an error.");
        }
    }

    private static async Task<DateOnly?> GetPreviousWorkingDayAsync(DateOnly today, AppDbContext db, CancellationToken ct)
    {
        var candidate = today.AddDays(-1);

        for (var i = 0; i < 10; i++)
        {
            if (TimeCalculationHelper.IsWeekend(candidate))
            {
                candidate = candidate.AddDays(-1);
                continue;
            }

            var isHoliday = await db.PublicHolidays.AnyAsync(h => h.Date == candidate && !h.IsWorkingDay, ct);
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
