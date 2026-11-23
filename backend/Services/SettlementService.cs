using System.Globalization;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;
using TimeManagementBackend.Data;
using TimeManagementBackend.Exceptions;
using TimeManagementBackend.Models;
using TimeManagementBackend.Models.DTOs;

namespace TimeManagementBackend.Services;

public class SettlementService(
    AppDbContext db,
    IOvertimeCalculationService overtimeService,
    INotificationService notificationService,
    IEmailService emailService,
    IMapper mapper,
    ILogger<SettlementService> logger) : ISettlementService
{
    public async Task GenerateForAllEmployeesAsync(int year, int month, CancellationToken ct = default)
    {
        var employees = await db.Users
            .Where(u => u.Role == UserRole.Employee && !u.IsDisabled)
            .Select(u => u.Id)
            .ToListAsync(ct);

        foreach (var userId in employees)
        {
            try
            {
                await GenerateForEmployeeAsync(userId, year, month, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Failed to generate MonthlySettlement for user {UserId} ({Year}-{Month:00}).",
                    userId, year, month);
            }
        }
    }

    private async Task GenerateForEmployeeAsync(string userId, int year, int month, CancellationToken ct)
    {
        var existing = await db.MonthlySettlements.FirstOrDefaultAsync(
            s => s.UserId == userId && s.Year == year && s.Month == month, ct);

        if (existing is { Status: SettlementStatus.Settled })
        {
            logger.LogDebug("Settlement for {UserId} {Year}-{Month:00} is confirmed — skipping.", userId, year, month);
            return;
        }

        // Not confirmed yet, so nothing was decided on it: refresh it with whatever changed since
        // (e.g. a sick day registered afterwards).
        if (existing != null)
        {
            await RecalculateAsync(existing, ct);
            await db.SaveChangesAsync(ct);
            return;
        }

        var settlement = new MonthlySettlement
        {
            UserId = userId,
            Year = year,
            Month = month,
            Status = SettlementStatus.PendingReview,
        };
        await RecalculateAsync(settlement, ct);

        db.MonthlySettlements.Add(settlement);

        try
        {
            await db.SaveChangesAsync(ct);
            logger.LogInformation(
                "Generated MonthlySettlement for {UserId} {Year}-{Month:00}: net={Net}h.",
                userId, year, month, settlement.NetBalanceHours);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            // Concurrent run already inserted — safe to ignore
            logger.LogDebug(
                "Unique constraint on MonthlySettlement for {UserId} {Year}-{Month:00} — concurrent creation, ignored.",
                userId, year, month);
        }
    }

    public async Task SendReviewEmailAsync(string appUrl, bool isReminder, CancellationToken ct = default)
    {
        var config = await db.AppConfigurations.AsNoTracking().FirstOrDefaultAsync(ct);
        var toEmail = config?.NotificationEmail;
        if (string.IsNullOrEmpty(toEmail) || !(config?.EnableSettlementEmails ?? true))
        {
            logger.LogInformation("Settlement review email skipped — no notification email or disabled in app settings.");
            return;
        }

        var pending = await db.MonthlySettlements
            .AsNoTracking()
            .Where(s => s.Status == SettlementStatus.PendingReview)
            .GroupBy(s => new { s.Year, s.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
            .ToListAsync(ct);

        // Nothing waiting — the Monday reminder stops on its own once everything is confirmed.
        if (pending.Count == 0)
            return;

        var pendingByMonth = pending
            .Select(p => (Month: new DateOnly(p.Year, p.Month, 1), PendingCount: p.Count))
            .OrderBy(p => p.Month)
            .ToList();

        try
        {
            await emailService.SendSettlementReviewEmailAsync(
                toEmail, pendingByMonth, $"{appUrl.TrimEnd('/')}/admin/settlements", isReminder);
            logger.LogInformation(
                "Sent settlement review email ({Kind}) to {Email} for {Count} pending settlement(s).",
                isReminder ? "reminder" : "ready", toEmail, pendingByMonth.Sum(p => p.PendingCount));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send settlement review email to {Email}.", toEmail);
        }
    }

    public async Task<IEnumerable<MonthlySettlementDto>> GetSettlementsAsync(int year, int month, CancellationToken ct = default)
    {
        return await db.MonthlySettlements
            .AsNoTracking()
            .Where(s => s.Year == year && s.Month == month)
            .OrderBy(s => s.User.FullName)
            .ProjectTo<MonthlySettlementDto>(mapper.ConfigurationProvider)
            .ToListAsync(ct);
    }

    public async Task<MonthlySettlementDto> GetSettlementDetailAsync(int id, CancellationToken ct = default)
    {
        return await db.MonthlySettlements
            .AsNoTracking()
            .Where(s => s.Id == id)
            .ProjectTo<MonthlySettlementDto>(mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(ct)
            ?? throw new ResourceNotFoundException("Settlement not found.");
    }

    public async Task ConfirmAsync(int id, ConfirmSettlementDto dto, string adminUserId, CancellationToken ct = default)
    {
        var settlement = await db.MonthlySettlements
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new ResourceNotFoundException("Settlement not found.");

        if (settlement.Status == SettlementStatus.Settled)
            throw new ValidationException("This settlement has already been confirmed and is read-only.");

        // Check for unresolved sessions in the settlement month
        var from = new DateOnly(settlement.Year, settlement.Month, 1);
        var to = new DateOnly(settlement.Year, settlement.Month,
            DateTime.DaysInMonth(settlement.Year, settlement.Month));

        var blockers = new List<BlockerDto>();

        var unresolvedSessions = await db.WorkSessions
            .Where(s => s.UserId == settlement.UserId &&
                        s.Date >= from && s.Date <= to &&
                        (s.Status == WorkSessionStatus.Open || s.Status == WorkSessionStatus.Invalidated))
            .Select(s => new { s.Date, s.Status })
            .ToListAsync(ct);

        foreach (var session in unresolvedSessions)
        {
            blockers.Add(new BlockerDto
            {
                Type = session.Status == WorkSessionStatus.Open ? "OpenSession" : "InvalidatedSession",
                Description = $"Session on {session.Date:yyyy-MM-dd} has status {session.Status} — must be resolved before confirming.",
            });
        }

        var pendingRequests = await db.TimeAdjustmentRequests
            .Where(r => r.UserId == settlement.UserId &&
                        r.Date >= from && r.Date <= to &&
                        r.Status == AdjustmentRequestStatus.Pending)
            .Select(r => r.Date)
            .ToListAsync(ct);

        foreach (var date in pendingRequests)
        {
            blockers.Add(new BlockerDto
            {
                Type = "PendingAdjustmentRequest",
                Description = $"Pending time adjustment request for {date:yyyy-MM-dd} must be reviewed before confirming.",
            });
        }

        if (blockers.Count > 0)
            throw new SettlementBlockedException(blockers);

        var (paidOut, carried) = ValidateAllocation(dto);

        settlement.PaidOutHours = paidOut;
        settlement.CarriedForwardHours = carried;
        settlement.Outcome = paidOut > 0 ? SettlementOutcome.Paid : SettlementOutcome.Unpaid;
        settlement.Notes = dto.Notes;
        settlement.Status = SettlementStatus.Settled;
        settlement.ReviewedByUserId = adminUserId;
        settlement.ReviewedAt = DateTimeOffset.UtcNow;

        // Materialize the carry-forward as a linked adjustment on next month's flex balance
        if (carried != 0)
        {
            var nextMonth = new DateOnly(settlement.Year, settlement.Month, 1).AddMonths(1);
            db.TimeBankAdjustments.Add(new TimeBankAdjustment
            {
                UserId = settlement.UserId,
                EffectiveDate = nextMonth,
                Hours = carried,
                Reason = $"Carry-over from {settlement.Year}-{settlement.Month:00} settlement",
                SourceSettlementId = settlement.Id,
                CreatedByUserId = adminUserId,
                CreatedAt = DateTimeOffset.UtcNow,
            });
        }

        await db.SaveChangesAsync(ct);

        // Next month's balance opens with this carry-over; refresh it if it was generated already.
        // The confirmation is saved by now, so a failure here must not turn it into an error.
        try { await RecalculateNextMonthAsync(settlement, ct); }
        catch (Exception ex) { logger.LogError(ex, "Failed to recalculate the settlement after {Id}.", id); }

        var msg = BuildEmployeeMessage(settlement, paidOut, carried);

        try { await notificationService.NotifyUserAsync(settlement.UserId, msg, NotificationType.MonthlySettlement, ct); }
        catch (Exception ex) { logger.LogError(ex, "Failed to send MonthlySettlement notification for settlement {Id}.", id); }
    }

    public async Task<MonthlySettlementDto> RecalculateAsync(int id, CancellationToken ct = default)
    {
        var settlement = await db.MonthlySettlements.FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new ResourceNotFoundException("Settlement not found.");

        if (settlement.Status == SettlementStatus.PendingReview)
        {
            await RecalculateAsync(settlement, ct);
            await db.SaveChangesAsync(ct);
        }

        return await GetSettlementDetailAsync(id, ct);
    }

    public async Task<MonthlySettlementDto> ReopenAsync(int id, CancellationToken ct = default)
    {
        var settlement = await db.MonthlySettlements.FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new ResourceNotFoundException("Settlement not found.");

        if (settlement.Status != SettlementStatus.Settled)
            throw new ValidationException("Only a confirmed settlement can be reopened.");

        // A confirmed next month already opened with this month's carry-over, so it has to be
        // reopened first; otherwise its numbers would silently stop adding up.
        var next = new DateOnly(settlement.Year, settlement.Month, 1).AddMonths(1);
        var nextSettled = await db.MonthlySettlements.AnyAsync(s =>
            s.UserId == settlement.UserId && s.Year == next.Year && s.Month == next.Month &&
            s.Status == SettlementStatus.Settled, ct);
        if (nextSettled)
            throw new ValidationException(
                $"The {next:yyyy-MM} settlement is already confirmed and builds on this one. Reopen that one first.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // The carry-over belongs to the confirmation being undone; confirming again recreates it.
        await db.TimeBankAdjustments.Where(a => a.SourceSettlementId == settlement.Id).ExecuteDeleteAsync(ct);

        settlement.Status = SettlementStatus.PendingReview;
        settlement.PaidOutHours = null;
        settlement.CarriedForwardHours = null;
        settlement.Outcome = null;
        settlement.ReviewedByUserId = null;
        settlement.ReviewedAt = null;
        await RecalculateAsync(settlement, ct);
        await db.SaveChangesAsync(ct);

        await RecalculateNextMonthAsync(settlement, ct);
        await tx.CommitAsync(ct);

        logger.LogInformation("Reopened MonthlySettlement {Id} ({Year}-{Month:00}).", id, settlement.Year, settlement.Month);
        return await GetSettlementDetailAsync(id, ct);
    }

    public async Task<IEnumerable<MonthlySettlementDto>> GetEmployeeHistoryAsync(string userId, CancellationToken ct = default)
    {
        return await db.MonthlySettlements
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.Year)
            .ThenByDescending(s => s.Month)
            .ProjectTo<MonthlySettlementDto>(mapper.ConfigurationProvider)
            .ToListAsync(ct);
    }

    /// <summary>Recomputes a settlement's balance from the live flex numbers of its month.</summary>
    private async Task RecalculateAsync(MonthlySettlement settlement, CancellationToken ct)
    {
        var overtime = await overtimeService.CalculateAsync(settlement.UserId, settlement.Year, settlement.Month, ct);
        var netBalance = overtime.RunningBalanceHours;
        settlement.NetBalanceHours = netBalance;
        settlement.OvertimeHours = netBalance > 0 ? netBalance : 0m;
        settlement.DeficitHours = netBalance < 0 ? -netBalance : 0m;
        settlement.GeneratedAt = DateTimeOffset.UtcNow;
    }

    private async Task RecalculateNextMonthAsync(MonthlySettlement settlement, CancellationToken ct)
    {
        var next = new DateOnly(settlement.Year, settlement.Month, 1).AddMonths(1);
        var nextPending = await db.MonthlySettlements.FirstOrDefaultAsync(s =>
            s.UserId == settlement.UserId && s.Year == next.Year && s.Month == next.Month &&
            s.Status == SettlementStatus.PendingReview, ct);
        if (nextPending == null) return;

        await RecalculateAsync(nextPending, ct);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Sanity-checks the confirm allocation and returns (paidOut, carried), both rounded to
    /// 2 decimals. Carried is signed: positive adds to next month's flex balance, negative starts
    /// it in deficit. The allocation is deliberately NOT forced to reconcile with the computed
    /// balance — the admin has full control; the UI surfaces any mismatch as a hint.
    /// </summary>
    private static (decimal PaidOut, decimal Carried) ValidateAllocation(ConfirmSettlementDto dto)
    {
        var paidOut = Math.Round(dto.PaidOutHours, 2);
        var carried = Math.Round(dto.CarryForwardHours, 2);

        if (paidOut < 0)
            throw new ValidationException("Paid-out hours cannot be negative.");

        return (paidOut, carried);
    }

    private static string BuildEmployeeMessage(MonthlySettlement settlement, decimal paidOut, decimal carried)
    {
        // Same hour notation as the rest of the app and the export (2.5h, +6.5h), independent of
        // the server's culture — a comma-decimal locale would otherwise write "2,5h".
        var prefix = $"Your {settlement.Year}-{settlement.Month:00} time settlement has been confirmed: " +
                     $"{FormatSignedHours(settlement.NetBalanceHours)} — ";

        var parts = new List<string>();
        if (paidOut > 0) parts.Add($"{FormatHours(paidOut)} paid out");
        if (carried > 0) parts.Add($"{FormatHours(carried)} carried over to next month's flex balance");
        if (carried < 0) parts.Add($"next month starts at {FormatHours(carried)}");

        return prefix + (parts.Count > 0 ? string.Join(", ", parts) : "settled as unpaid") + ".";
    }

    private static string FormatHours(decimal hours) =>
        Math.Round(hours, 2, MidpointRounding.AwayFromZero).ToString("0.##", CultureInfo.InvariantCulture) + "h";

    private static string FormatSignedHours(decimal hours) =>
        (Math.Round(hours, 2, MidpointRounding.AwayFromZero) > 0 ? "+" : "") + FormatHours(hours);

    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
        => ex.InnerException?.Message.Contains("unique", StringComparison.OrdinalIgnoreCase) is true
        || ex.InnerException?.Message.Contains("23505") is true;
}
