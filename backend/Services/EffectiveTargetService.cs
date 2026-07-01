using Microsoft.EntityFrameworkCore;
using TimeManagementBackend.Data;

namespace TimeManagementBackend.Services;

public class EffectiveTargetService(AppDbContext db) : IEffectiveTargetService
{
    public async Task<decimal> GetEffectiveTargetAsync(string userId, DateOnly date, CancellationToken ct = default)
    {
        // Weekend → 0 (no target)
        if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            return 0;

        // Non-working public holiday → 0, all hours count as surplus
        var isNonWorkingHoliday = await db.PublicHolidays
            .AnyAsync(h => h.Date == date && !h.IsWorkingDay, ct);
        if (isNonWorkingHoliday) return 0;

        // Leave fraction: cap at 1.0 in case multiple entries sum above it
        var leaveTotal = await db.VacationDays
            .Where(v => v.UserId == userId && v.Date == date)
            .SumAsync(v => (decimal?)v.Amount, ct) ?? 0m;
        var leaveFraction = Math.Min(1.0m, leaveTotal);
        if (leaveFraction >= 1.0m) return 0;

        // Resolve target: per-user override takes priority over global default
        var candidates = await db.WorkdayTargets
            .Where(t => (t.UserId == userId || t.UserId == null) && t.DayOfWeek == date.DayOfWeek)
            .ToListAsync(ct);

        var effective = candidates.FirstOrDefault(t => t.UserId == userId)
                     ?? candidates.FirstOrDefault(t => t.UserId == null);

        if (effective == null || effective.Hours == 0) return 0;

        return effective.Hours * (1 - leaveFraction);
    }
}
