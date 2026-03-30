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
            .Include(b => b.VacationType)
            .Where(b => b.UserId == userId)
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
                VacationTypeName = b.VacationType.Name,
                VacationTypeColor = b.VacationType.Color,
                YearlyBalance = b.YearlyBalance,
                UsedDays = used,
                RemainingDays = b.YearlyBalance - used,
            };
        }).OrderBy(b => b.VacationTypeName);
    }

    public async Task<IEnumerable<VacationDayDto>> GetMyVacationDaysAsync(string userId, CancellationToken ct = default)
    {
        return await _db.VacationDays
            .Include(d => d.VacationType)
            .Where(d => d.UserId == userId)
            .OrderByDescending(d => d.Date)
            .Select(d => ToDto(d))
            .ToListAsync(ct);
    }

    public Task<bool> ExistsForDateAndTypeAsync(string userId, DateOnly date, int vacationTypeId, CancellationToken ct = default)
        => _db.VacationDays.AnyAsync(d => d.UserId == userId && d.Date == date && d.VacationTypeId == vacationTypeId, ct);

    public async Task<VacationDayDto> CreateVacationDayAsync(string userId, CreateVacationDayDto dto, CancellationToken ct = default)
    {
        ValidateAmount(dto.Amount);

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
