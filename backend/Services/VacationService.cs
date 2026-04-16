using Microsoft.EntityFrameworkCore;
using TimeManagementBackend.Data;
using TimeManagementBackend.Exceptions;
using TimeManagementBackend.Models;
using TimeManagementBackend.Models.DTOs;

namespace TimeManagementBackend.Services;

public class VacationService(AppDbContext db) : IVacationService
{
    private readonly AppDbContext _db = db;

    public async Task<IEnumerable<VacationBalanceDto>> GetMyBalancesAsync(string userId, int? year = null, CancellationToken ct = default)
    {
        // Use the caller-supplied year (the user's local year from the frontend) so that
        // users in UTC+13/UTC-12 don't see wrong balances around the Jan 1 boundary.
        var currentYear = year ?? DateTime.UtcNow.Year;

        var balances = await _db.EmployeeVacationBalances
            .AsNoTracking()
            .Where(b => b.UserId == userId)
            .Select(b => new
            {
                b.VacationTypeId,
                b.VacationType.Name,
                b.VacationType.Color,
                b.YearlyBalance,
            })
            .ToListAsync(ct);

        var usedByType = await _db.VacationDays
            .Where(d => d.UserId == userId && d.Date.Year == currentYear)
            .GroupBy(d => d.VacationTypeId)
            .Select(g => new { TypeId = g.Key, Used = g.Sum(d => d.Amount) })
            .ToDictionaryAsync(x => x.TypeId, x => x.Used, ct);

        return balances.Select(b =>
        {
            var used = usedByType.GetValueOrDefault(b.VacationTypeId, 0m);
            return new VacationBalanceDto
            {
                VacationTypeId = b.VacationTypeId,
                VacationTypeName = b.Name,
                VacationTypeColor = b.Color,
                YearlyBalance = b.YearlyBalance,
                UsedDays = used,
                RemainingDays = b.YearlyBalance - used,
            };
        }).OrderBy(b => b.VacationTypeName);
    }

    public async Task<IEnumerable<VacationDayDto>> GetMyVacationDaysAsync(string userId, CancellationToken ct = default)
    {
        return await _db.VacationDays
            .AsNoTracking()
            .Where(d => d.UserId == userId)
            .OrderByDescending(d => d.Date)
            .Select(d => new VacationDayDto
            {
                Id = d.Id,
                VacationTypeId = d.VacationTypeId,
                VacationTypeName = d.VacationType.Name,
                VacationTypeColor = d.VacationType.Color,
                Date = d.Date,
                Amount = d.Amount,
                Note = d.Note,
            })
            .ToListAsync(ct);
    }

    public Task<bool> ExistsForDateAndTypeAsync(string userId, DateOnly date, int vacationTypeId, CancellationToken ct = default)
        => _db.VacationDays.AnyAsync(d => d.UserId == userId && d.Date == date && d.VacationTypeId == vacationTypeId, ct);

    public async Task<VacationDayDto?> GetVacationForDateAsync(string userId, DateOnly date, CancellationToken ct = default)
    {
        return await _db.VacationDays
            .AsNoTracking()
            .Where(v => v.UserId == userId && v.Date == date)
            .Select(v => new VacationDayDto
            {
                Id = v.Id,
                VacationTypeId = v.VacationTypeId,
                VacationTypeName = v.VacationType.Name,
                VacationTypeColor = v.VacationType.Color,
                Date = v.Date,
                Amount = v.Amount,
                Note = v.Note,
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<VacationDayDto> CreateVacationDayAsync(string userId, CreateVacationDayDto dto, CancellationToken ct = default)
    {
        ValidateAmount(dto.Amount);

        var isHoliday = await _db.PublicHolidays
            .AnyAsync(h => h.Date == dto.Date && !h.IsWorkingDay, ct);
        if (isHoliday)
            throw new ArgumentException("Cannot plan vacation on a public holiday.");

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var balance = await _db.EmployeeVacationBalances
            .FirstOrDefaultAsync(b => b.UserId == userId && b.VacationTypeId == dto.VacationTypeId, ct)
            ?? throw new ResourceNotFoundException("This vacation type is not assigned to you.");

        // Use the year of the submitted date — not the server's UTC year — so that
        // balance checks are always scoped to the correct year for the vacation being planned.
        var currentYear = dto.Date.Year;
        var used = await GetUsedVacationDaysAsync(userId, dto.VacationTypeId, currentYear, ct: ct);

        if (used + dto.Amount > balance.YearlyBalance)
            throw new InsufficientVacationBalanceException(
                $"Insufficient balance. Remaining: {balance.YearlyBalance - used} day(s).");

        var day = new VacationDay
        {
            UserId = userId,
            VacationTypeId = dto.VacationTypeId,
            Date = dto.Date,
            Amount = dto.Amount,
            Note = dto.Note?.Trim(),
        };

        _db.VacationDays.Add(day);
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        await _db.Entry(day).Reference(d => d.VacationType).LoadAsync(ct);
        return ToDto(day);
    }

    public async Task<VacationDayDto> UpdateVacationDayAsync(string userId, int id, CreateVacationDayDto dto, CancellationToken ct = default)
    {
        ValidateAmount(dto.Amount);

        var day = await _db.VacationDays
            .Include(d => d.VacationType)
            .FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId, ct)
            ?? throw new ResourceNotFoundException("Vacation day not found.");

        var balance = await _db.EmployeeVacationBalances
            .FirstOrDefaultAsync(b => b.UserId == userId && b.VacationTypeId == dto.VacationTypeId, ct)
            ?? throw new ResourceNotFoundException("This vacation type is not assigned to you.");

        var currentYear = dto.Date.Year;
        var used = await GetUsedVacationDaysAsync(userId, dto.VacationTypeId, currentYear, excludeId: id, ct: ct);

        if (used + dto.Amount > balance.YearlyBalance)
            throw new InsufficientVacationBalanceException(
                $"Insufficient balance. Remaining: {balance.YearlyBalance - used} day(s).");

        day.VacationTypeId = dto.VacationTypeId;
        day.Date = dto.Date;
        day.Amount = dto.Amount;
        day.Note = dto.Note?.Trim();

        await _db.SaveChangesAsync(ct);

        if (day.VacationType.Id != dto.VacationTypeId)
            await _db.Entry(day).Reference(d => d.VacationType).LoadAsync(ct);

        return ToDto(day);
    }

    public async Task DeleteVacationDayAsync(string userId, int id, CancellationToken ct = default)
    {
        var day = await _db.VacationDays
            .FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId, ct)
            ?? throw new ResourceNotFoundException("Vacation day not found.");

        _db.VacationDays.Remove(day);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<VacationRangeResultDto> CreateVacationRangeAsync(string userId, CreateVacationRangeDto dto, CancellationToken ct = default)
    {
        ValidateAmount(dto.Amount);

        if (dto.EndDate < dto.StartDate)
            throw new ArgumentException("End date must be on or after start date.");

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var balance = await _db.EmployeeVacationBalances
            .FirstOrDefaultAsync(b => b.UserId == userId && b.VacationTypeId == dto.VacationTypeId, ct)
            ?? throw new ResourceNotFoundException("This vacation type is not assigned to you.");

        // Enumerate all calendar days in the range
        var allDays = new List<DateOnly>();
        for (var d = dto.StartDate; d <= dto.EndDate; d = d.AddDays(1))
            allDays.Add(d);

        // Fetch public holidays that fall within the range and are actual days off for the company
        var holidayDates = (await _db.PublicHolidays
            .Where(h => h.Date >= dto.StartDate && h.Date <= dto.EndDate && !h.IsWorkingDay)
            .Select(h => h.Date)
            .ToListAsync(ct))
            .ToHashSet();

        var workingDays = allDays
            .Where(d => d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday
                     && !holidayDates.Contains(d))
            .ToList();

        int skippedWeekends = allDays.Count(d => d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday);
        int skippedHolidays = allDays.Count(d => holidayDates.Contains(d)
            && d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday);

        // Filter out dates that already have an entry for this type
        var existingDates = await _db.VacationDays
            .Where(d => d.UserId == userId && d.VacationTypeId == dto.VacationTypeId
                        && d.Date >= dto.StartDate && d.Date <= dto.EndDate)
            .Select(d => d.Date)
            .ToListAsync(ct);

        var newDays = workingDays.Where(d => !existingDates.Contains(d)).ToList();
        int skippedExisting = workingDays.Count - newDays.Count;

        if (newDays.Count == 0)
            return new VacationRangeResultDto
            {
                Created = [],
                SkippedWeekends = skippedWeekends,
                SkippedHolidays = skippedHolidays,
                SkippedExisting = skippedExisting,
            };

        // Validate balance — scope to the start date's year. Ranges that straddle a
        // year boundary are unusual; the balance check uses the start year as the
        // reference so the user isn't blocked by the following year's (empty) balance.
        var currentYear = dto.StartDate.Year;
        var alreadyUsed = await GetUsedVacationDaysAsync(userId, dto.VacationTypeId, currentYear, ct: ct);

        decimal totalNewAmount = newDays.Count(d => d.Year == currentYear) * dto.Amount;

        if (alreadyUsed + totalNewAmount > balance.YearlyBalance)
            throw new InsufficientVacationBalanceException(
                $"Insufficient balance. Remaining: {balance.YearlyBalance - alreadyUsed} day(s), needed: {totalNewAmount}.");

        var entities = newDays.Select(d => new VacationDay
        {
            UserId = userId,
            VacationTypeId = dto.VacationTypeId,
            Date = d,
            Amount = dto.Amount,
            Note = dto.Note?.Trim(),
        }).ToList();

        _db.VacationDays.AddRange(entities);
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        var type = await _db.VacationTypes.FindAsync([dto.VacationTypeId], ct)
            ?? throw new ResourceNotFoundException("Vacation type not found.");

        return new VacationRangeResultDto
        {
            Created = entities.Select(e => new VacationDayDto
            {
                Id = e.Id,
                VacationTypeId = e.VacationTypeId,
                VacationTypeName = type.Name,
                VacationTypeColor = type.Color,
                Date = e.Date,
                Amount = e.Amount,
                Note = e.Note,
            }).ToList(),
            SkippedWeekends = skippedWeekends,
            SkippedHolidays = skippedHolidays,
            SkippedExisting = skippedExisting,
        };
    }

    private Task<decimal> GetUsedVacationDaysAsync(
        string userId, int vacationTypeId, int year, int? excludeId = null, CancellationToken ct = default)
    {
        var query = _db.VacationDays
            .Where(d => d.UserId == userId && d.VacationTypeId == vacationTypeId && d.Date.Year == year);

        if (excludeId.HasValue)
            query = query.Where(d => d.Id != excludeId.Value);

        return query.SumAsync(d => d.Amount, ct);
    }

    private static void ValidateAmount(decimal amount)
    {
        if (amount != 0.5m && amount != 1.0m)
            throw new InvalidVacationAmountException("Amount must be 0.5 (half day) or 1.0 (full day).");
    }

    private static VacationDayDto ToDto(VacationDay d) => new()
    {
        Id = d.Id,
        VacationTypeId = d.VacationTypeId,
        VacationTypeName = d.VacationType.Name,
        VacationTypeColor = d.VacationType.Color,
        Date = d.Date,
        Amount = d.Amount,
        Note = d.Note,
    };
}
