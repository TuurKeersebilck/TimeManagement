using Microsoft.EntityFrameworkCore;
using TimeManagementBackend.Data;
using TimeManagementBackend.Exceptions;
using TimeManagementBackend.Models;
using TimeManagementBackend.Models.DTOs;

namespace TimeManagementBackend.Services;

public class VacationService(AppDbContext db) : IVacationService
{
    private readonly AppDbContext _db = db;

    public async Task<IEnumerable<VacationBalanceDto>> GetMyBalancesAsync(string userId, CancellationToken ct = default)
    {
        var currentYear = DateTime.UtcNow.Year;

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

    public async Task<VacationDayDto> CreateVacationDayAsync(string userId, CreateVacationDayDto dto, CancellationToken ct = default)
    {
        ValidateAmount(dto.Amount);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var balance = await _db.EmployeeVacationBalances
            .FirstOrDefaultAsync(b => b.UserId == userId && b.VacationTypeId == dto.VacationTypeId, ct)
            ?? throw new ResourceNotFoundException("This vacation type is not assigned to you.");

        var currentYear = DateTime.UtcNow.Year;
        var used = await _db.VacationDays
            .Where(d => d.UserId == userId && d.VacationTypeId == dto.VacationTypeId && d.Date.Year == currentYear)
            .SumAsync(d => d.Amount, ct);

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

        var currentYear = DateTime.UtcNow.Year;
        var used = await _db.VacationDays
            .Where(d => d.UserId == userId && d.VacationTypeId == dto.VacationTypeId && d.Date.Year == currentYear && d.Id != id)
            .SumAsync(d => d.Amount, ct);

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

        var workingDays = allDays
            .Where(d => d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday)
            .ToList();

        int skippedWeekends = allDays.Count - workingDays.Count;

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
                SkippedExisting = skippedExisting,
            };

        // Validate balance (only count days in the current year)
        var currentYear = DateTime.UtcNow.Year;
        var alreadyUsed = await _db.VacationDays
            .Where(d => d.UserId == userId && d.VacationTypeId == dto.VacationTypeId && d.Date.Year == currentYear)
            .SumAsync(d => d.Amount, ct);

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
            SkippedExisting = skippedExisting,
        };
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
