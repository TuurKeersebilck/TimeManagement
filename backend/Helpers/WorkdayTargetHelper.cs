using Microsoft.EntityFrameworkCore;
using TimeManagementBackend.Data;
using TimeManagementBackend.Models;
using TimeManagementBackend.Models.DTOs;

namespace TimeManagementBackend.Helpers;

public static class WorkdayTargetHelper
{
    /// <summary>
    /// Replaces all workday-target rows for the given scope — a specific employee via
    /// <paramref name="userId"/>, or the global default when <paramref name="userId"/> is null —
    /// with the submitted set. The submitted set is authoritative: a day missing from it means
    /// "no override for that day" (it inherits the global default again, for the per-employee
    /// case), so any existing row for that day not present in the submission is removed.
    /// </summary>
    public static async Task<List<WorkdayTarget>> UpsertWorkdayTargetsAsync(
        AppDbContext db, string? userId, IEnumerable<WorkdayTargetDto> targets, CancellationToken ct = default)
    {
        var targetList = targets.ToList();
        var submittedDays = targetList.Select(t => t.DayOfWeek).ToHashSet();

        var existing = await db.WorkdayTargets
            .Where(t => t.UserId == userId)
            .ToListAsync(ct);

        db.WorkdayTargets.RemoveRange(existing.Where(t => !submittedDays.Contains(t.DayOfWeek)));

        foreach (var dto in targetList)
        {
            var row = existing.FirstOrDefault(t => t.DayOfWeek == dto.DayOfWeek);
            if (row == null)
            {
                row = new WorkdayTarget { UserId = userId, DayOfWeek = dto.DayOfWeek };
                db.WorkdayTargets.Add(row);
            }
            row.Hours = dto.Hours;
        }

        await db.SaveChangesAsync(ct);

        return await db.WorkdayTargets
            .Where(t => t.UserId == userId)
            .OrderBy(t => t.DayOfWeek)
            .ToListAsync(ct);
    }
}
