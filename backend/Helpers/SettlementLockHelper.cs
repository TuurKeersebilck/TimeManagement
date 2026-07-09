using Microsoft.EntityFrameworkCore;
using TimeManagementBackend.Data;
using TimeManagementBackend.Models;

namespace TimeManagementBackend.Helpers;

public static class SettlementLockHelper
{
    /// <summary>True if <paramref name="date"/> falls in a month that has already been settled for the user.</summary>
    public static Task<bool> IsMonthSettledAsync(
        AppDbContext db, string userId, DateOnly date, CancellationToken ct = default)
        => db.MonthlySettlements.AnyAsync(
            s => s.UserId == userId
              && s.Year == date.Year
              && s.Month == date.Month
              && s.Status == SettlementStatus.Settled, ct);
}
