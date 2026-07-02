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
            .Include(s => s.User)
            .Include(s => s.ReviewedByUser)
            .Where(s => s.Year == year && s.Month == month)
            .OrderBy(s => s.User.FullName)
            .Select(s => MapToDto(s))
            .ToListAsync(ct);
    }

    public async Task<MonthlySettlementDto> GetSettlementDetailAsync(int id, CancellationToken ct = default)
    {
        var settlement = await db.MonthlySettlements
            .AsNoTracking()
            .Include(s => s.User)
            .Include(s => s.ReviewedByUser)
            .FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new ResourceNotFoundException("Settlement not found.");

        return MapToDto(settlement);
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

        settlement.Outcome = dto.Outcome;
        settlement.OvertimeHours = dto.OvertimeHoursOverride ?? settlement.OvertimeHours;
        settlement.DeficitHours = dto.DeficitHoursOverride ?? settlement.DeficitHours;
        settlement.Notes = dto.Notes;
        settlement.Status = SettlementStatus.Settled;
        settlement.ReviewedByUserId = adminUserId;
        settlement.ReviewedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);

        var outcomeLabel = dto.Outcome switch
        {
            SettlementOutcome.Paid => "marked as paid",
            SettlementOutcome.LeaveDeducted => "settled via leave deduction",
            SettlementOutcome.Unpaid => "settled as unpaid",
            _ => "settled",
        };
        var balanceSign = settlement.NetBalanceHours >= 0 ? "+" : "";
        var msg = $"Your {settlement.Year}-{settlement.Month:00} time settlement has been confirmed: " +
                  $"{balanceSign}{settlement.NetBalanceHours}h {outcomeLabel}.";

        try { await notificationService.NotifyUserAsync(settlement.UserId, msg, NotificationType.MonthlySettlement, ct); }
        catch (Exception ex) { logger.LogError(ex, "Failed to send MonthlySettlement notification for settlement {Id}.", id); }
    }

    public async Task<IEnumerable<MonthlySettlementDto>> GetEmployeeHistoryAsync(string userId, CancellationToken ct = default)
    {
        return await db.MonthlySettlements
            .AsNoTracking()
            .Include(s => s.ReviewedByUser)
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.Year)
            .ThenByDescending(s => s.Month)
            .Select(s => MapToDto(s))
            .ToListAsync(ct);
    }

    private static MonthlySettlementDto MapToDto(MonthlySettlement s) => new()
    {
        Id = s.Id,
        UserId = s.UserId,
        EmployeeName = s.User?.FullName ?? "",
        Year = s.Year,
        Month = s.Month,
        NetBalanceHours = s.NetBalanceHours,
        OvertimeHours = s.OvertimeHours,
        DeficitHours = s.DeficitHours,
        Outcome = s.Outcome,
        Status = s.Status,
        ReviewedByUserId = s.ReviewedByUserId,
        ReviewedByName = s.ReviewedByUser?.FullName,
        ReviewedAt = s.ReviewedAt,
        Notes = s.Notes,
        GeneratedAt = s.GeneratedAt,
    };

    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
        => ex.InnerException?.Message.Contains("unique", StringComparison.OrdinalIgnoreCase) is true
        || ex.InnerException?.Message.Contains("23505") is true;
}
