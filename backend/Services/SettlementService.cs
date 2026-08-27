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
        var exists = await db.MonthlySettlements.AnyAsync(
            s => s.UserId == userId && s.Year == year && s.Month == month, ct);

        if (exists)
        {
            logger.LogDebug("Settlement for {UserId} {Year}-{Month:00} already exists — skipping.", userId, year, month);
            return;
        }

        var overtime = await overtimeService.CalculateAsync(userId, year, month, ct);

        var netBalance = overtime.RunningBalanceHours;
        var overtimeHours = netBalance > 0 ? netBalance : 0m;
        var deficitHours = netBalance < 0 ? -netBalance : 0m;

        var settlement = new MonthlySettlement
        {
            UserId = userId,
            Year = year,
            Month = month,
            NetBalanceHours = netBalance,
            OvertimeHours = overtimeHours,
            DeficitHours = deficitHours,
            Status = SettlementStatus.PendingReview,
            GeneratedAt = DateTimeOffset.UtcNow,
        };

        db.MonthlySettlements.Add(settlement);

        try
        {
            await db.SaveChangesAsync(ct);
            logger.LogInformation(
                "Generated MonthlySettlement for {UserId} {Year}-{Month:00}: net={Net}h.",
                userId, year, month, netBalance);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            // Concurrent run already inserted — safe to ignore
            logger.LogDebug(
                "Unique constraint on MonthlySettlement for {UserId} {Year}-{Month:00} — concurrent creation, ignored.",
                userId, year, month);
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

        var msg = BuildEmployeeMessage(settlement, paidOut, carried);

        try { await notificationService.NotifyUserAsync(settlement.UserId, msg, NotificationType.MonthlySettlement, ct); }
        catch (Exception ex) { logger.LogError(ex, "Failed to send MonthlySettlement notification for settlement {Id}.", id); }
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
        var balanceSign = settlement.NetBalanceHours >= 0 ? "+" : "";
        var prefix = $"Your {settlement.Year}-{settlement.Month:00} time settlement has been confirmed: " +
                     $"{balanceSign}{settlement.NetBalanceHours}h — ";

        var parts = new List<string>();
        if (paidOut > 0) parts.Add($"{paidOut}h paid out");
        if (carried > 0) parts.Add($"{carried}h carried over to next month's flex balance");
        if (carried < 0) parts.Add($"next month starts at {carried}h");

        return prefix + (parts.Count > 0 ? string.Join(", ", parts) : "settled as unpaid") + ".";
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
        => ex.InnerException?.Message.Contains("unique", StringComparison.OrdinalIgnoreCase) is true
        || ex.InnerException?.Message.Contains("23505") is true;
}
