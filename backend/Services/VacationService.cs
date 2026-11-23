using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;
using TimeManagementBackend.Data;
using TimeManagementBackend.Exceptions;
using TimeManagementBackend.Helpers;
using TimeManagementBackend.Models;
using TimeManagementBackend.Models.DTOs;

namespace TimeManagementBackend.Services;

public class VacationService(AppDbContext db, IMapper mapper) : IVacationService
{
    private readonly AppDbContext _db = db;
    private readonly IMapper _mapper = mapper;

    public async Task<IEnumerable<VacationBalanceDto>> GetMyBalancesAsync(string userId, int? year = null, CancellationToken ct = default, bool includeRequests = true)
    {
        // Use the caller-supplied year (the user's local year from the frontend) so that
        // users in UTC+13/UTC-12 don't see wrong balances around the Jan 1 boundary.
        var currentYear = year ?? DateTime.UtcNow.Year;

        var assigned = await _db.EmployeeVacationBalances
            .AsNoTracking()
            .Where(b => b.UserId == userId && !b.VacationType.IsUnlimited)
            .Select(b => new
            {
                b.VacationTypeId,
                b.VacationType.Name,
                b.VacationType.Color,
                b.VacationType.IsAdminOnly,
                YearlyBalance = (decimal?)b.YearlyBalance,
            })
            .ToListAsync(ct);

        // Unlimited types (e.g. sick leave) are available to everyone without being assigned.
        var unlimited = await _db.VacationTypes
            .AsNoTracking()
            .Where(t => t.IsUnlimited)
            .Select(t => new
            {
                VacationTypeId = t.Id,
                t.Name,
                t.Color,
                t.IsAdminOnly,
                YearlyBalance = (decimal?)null,
            })
            .ToListAsync(ct);

        var balances = assigned.Concat(unlimited);

        var usedByType = await _db.VacationDays
            .Where(d => d.UserId == userId && d.Date.Year == currentYear)
            .GroupBy(d => d.VacationTypeId)
            .Select(g => new { TypeId = g.Key, Used = g.Sum(d => d.Amount) })
            .ToDictionaryAsync(x => x.TypeId, x => x.Used, ct);

        // Requested (or drafted) but not yet reviewed; counted in the year the request starts, like bookings.
        var heldByType = !includeRequests ? [] : await _db.VacationRequests
            .Where(r => r.UserId == userId && r.StartDate.Year == currentYear &&
                        (r.Status == VacationRequestStatus.Pending || r.Status == VacationRequestStatus.Draft))
            .GroupBy(r => new { r.VacationTypeId, r.Status })
            .Select(g => new { g.Key.VacationTypeId, g.Key.Status, Days = g.Sum(r => r.TotalDays) })
            .ToListAsync(ct);

        decimal Held(int typeId, VacationRequestStatus status) =>
            heldByType.Where(h => h.VacationTypeId == typeId && h.Status == status).Sum(h => h.Days);

        return balances.Select(b =>
        {
            var used = usedByType.GetValueOrDefault(b.VacationTypeId, 0m);
            return new VacationBalanceDto
            {
                VacationTypeId = b.VacationTypeId,
                VacationTypeName = b.Name,
                VacationTypeColor = b.Color,
                IsUnlimited = b.YearlyBalance == null,
                IsAdminOnly = b.IsAdminOnly,
                YearlyBalance = b.YearlyBalance,
                UsedDays = used,
                PendingDays = Held(b.VacationTypeId, VacationRequestStatus.Pending),
                DraftDays = Held(b.VacationTypeId, VacationRequestStatus.Draft),
                RemainingDays = b.YearlyBalance - used,
            };
        }).OrderBy(b => b.VacationTypeName);
    }

    public async Task<IEnumerable<VacationDayDto>> GetMyVacationDaysAsync(string userId, DateOnly? dateFrom = null, DateOnly? dateTo = null, CancellationToken ct = default)
    {
        var query = _db.VacationDays.AsNoTracking().Where(d => d.UserId == userId);

        if (dateFrom.HasValue)
            query = query.Where(d => d.Date >= dateFrom.Value);
        if (dateTo.HasValue)
            query = query.Where(d => d.Date <= dateTo.Value);

        return await query
            .OrderByDescending(d => d.Date)
            .ProjectTo<VacationDayDto>(_mapper.ConfigurationProvider)
            .ToListAsync(ct);
    }

    public Task<bool> ExistsForDateAndTypeAsync(string userId, DateOnly date, int vacationTypeId, CancellationToken ct = default)
        => _db.VacationDays.AnyAsync(d => d.UserId == userId && d.Date == date && d.VacationTypeId == vacationTypeId, ct);

    public async Task<VacationDayDto?> GetVacationForDateAsync(string userId, DateOnly date, CancellationToken ct = default)
    {
        return await _db.VacationDays
            .AsNoTracking()
            .Where(v => v.UserId == userId && v.Date == date)
            .ProjectTo<VacationDayDto>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<VacationDayDto> CreateVacationDayAsync(string userId, CreateVacationDayDto dto, bool asAdmin = false, CancellationToken ct = default)
    {
        ValidateAmount(dto.Amount);

        var isHoliday = await _db.PublicHolidays
            .AnyAsync(h => h.Date == dto.Date && !h.IsWorkingDay, ct);
        if (isHoliday)
            throw new ArgumentException("Cannot plan vacation on a public holiday.");

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var yearlyBalance = await GetYearlyBalanceAsync(userId, dto.VacationTypeId, asAdmin, ct);

        if (yearlyBalance is decimal limit)
        {
            // Use the year of the submitted date — not the server's UTC year — so that
            // balance checks are always scoped to the correct year for the vacation being planned.
            var currentYear = dto.Date.Year;
            var used = await GetUsedVacationDaysAsync(userId, dto.VacationTypeId, currentYear, ct: ct);

            if (used + dto.Amount > limit)
                throw new InsufficientVacationBalanceException(
                    $"Insufficient balance. Remaining: {FormatDays(limit - used)} day(s).");
        }

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
        return _mapper.Map<VacationDayDto>(day);
    }

    public async Task<VacationDayDto> UpdateVacationDayAsync(string userId, int id, CreateVacationDayDto dto, bool asAdmin = false, CancellationToken ct = default)
    {
        ValidateAmount(dto.Amount);

        var day = await _db.VacationDays
            .Include(d => d.VacationType)
            .FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId, ct)
            ?? throw new ResourceNotFoundException("Vacation day not found.");

        if (day.VacationType.IsAdminOnly && !asAdmin)
            throw AdminOnly(day.VacationType.Name);

        var yearlyBalance = await GetYearlyBalanceAsync(userId, dto.VacationTypeId, asAdmin, ct);

        if (yearlyBalance is decimal limit)
        {
            var currentYear = dto.Date.Year;
            var used = await GetUsedVacationDaysAsync(userId, dto.VacationTypeId, currentYear, excludeId: id, ct: ct);

            if (used + dto.Amount > limit)
                throw new InsufficientVacationBalanceException(
                    $"Insufficient balance. Remaining: {FormatDays(limit - used)} day(s).");
        }

        var typeChanged = day.VacationTypeId != dto.VacationTypeId;

        day.VacationTypeId = dto.VacationTypeId;
        day.Date = dto.Date;
        day.Amount = dto.Amount;
        day.Note = dto.Note?.Trim();

        await _db.SaveChangesAsync(ct);

        if (typeChanged)
            await _db.Entry(day).Reference(d => d.VacationType).LoadAsync(ct);

        return _mapper.Map<VacationDayDto>(day);
    }

    public async Task DeleteVacationDayAsync(string userId, int id, bool asAdmin = false, CancellationToken ct = default)
    {
        var day = await _db.VacationDays
            .Include(d => d.VacationType)
            .FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId, ct)
            ?? throw new ResourceNotFoundException("Vacation day not found.");

        if (day.VacationType.IsAdminOnly && !asAdmin)
            throw AdminOnly(day.VacationType.Name);

        _db.VacationDays.Remove(day);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<VacationRangeResultDto> CreateVacationRangeAsync(string userId, CreateVacationRangeDto dto, bool asAdmin = false, CancellationToken ct = default)
    {
        ValidateAmount(dto.Amount);

        if (dto.EndDate < dto.StartDate)
            throw new ArgumentException("End date must be on or after start date.");

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var yearlyBalance = await GetYearlyBalanceAsync(userId, dto.VacationTypeId, asAdmin, ct);

        var plan = await PlanRangeAsync(userId, dto.VacationTypeId, dto.StartDate, dto.EndDate, ct);
        var newDays = plan.NewDays;
        int skippedWeekends = plan.SkippedWeekends;
        int skippedHolidays = plan.SkippedHolidays;
        int skippedExisting = plan.SkippedExisting;

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
        if (yearlyBalance is decimal limit)
        {
            var currentYear = dto.StartDate.Year;
            var alreadyUsed = await GetUsedVacationDaysAsync(userId, dto.VacationTypeId, currentYear, ct: ct);

            decimal totalNewAmount = newDays.Count(d => d.Year == currentYear) * dto.Amount;

            if (alreadyUsed + totalNewAmount > limit)
                throw new InsufficientVacationBalanceException(
                    $"Insufficient balance. Remaining: {FormatDays(limit - alreadyUsed)} day(s), needed: {FormatDays(totalNewAmount)}.");
        }

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

        // The bulk-inserted entities don't have VacationType loaded — attach the
        // already-fetched type in-memory so the mapper can resolve it without a query per row.
        foreach (var e in entities) e.VacationType = type;

        return new VacationRangeResultDto
        {
            Created = _mapper.Map<List<VacationDayDto>>(entities),
            SkippedWeekends = skippedWeekends,
            SkippedHolidays = skippedHolidays,
            SkippedExisting = skippedExisting,
        };
    }

    public async Task<VacationRangePlan> PlanRangeAsync(
        string userId, int vacationTypeId, DateOnly startDate, DateOnly endDate, CancellationToken ct = default)
    {
        var allDays = new List<DateOnly>();
        for (var d = startDate; d <= endDate; d = d.AddDays(1))
            allDays.Add(d);

        // Public holidays in the range that are actual days off for the company
        var holidayDates = (await _db.PublicHolidays
            .Where(h => h.Date >= startDate && h.Date <= endDate && !h.IsWorkingDay)
            .Select(h => h.Date)
            .ToListAsync(ct))
            .ToHashSet();

        var workingDays = allDays
            .Where(d => !TimeCalculationHelper.IsWeekend(d) && !holidayDates.Contains(d))
            .ToList();

        var existingDates = (await _db.VacationDays
            .Where(d => d.UserId == userId && d.VacationTypeId == vacationTypeId
                        && d.Date >= startDate && d.Date <= endDate)
            .Select(d => d.Date)
            .ToListAsync(ct))
            .ToHashSet();

        var newDays = workingDays.Where(d => !existingDates.Contains(d)).ToList();

        return new VacationRangePlan(
            newDays,
            SkippedWeekends: allDays.Count(TimeCalculationHelper.IsWeekend),
            SkippedHolidays: allDays.Count(d => holidayDates.Contains(d) && !TimeCalculationHelper.IsWeekend(d)),
            SkippedExisting: workingDays.Count - newDays.Count);
    }

    /// <summary>
    /// The employee's yearly balance for a type, or null when the type is unlimited.
    /// Throws when a limited type isn't assigned to the employee, or when an employee
    /// books an admin-only type.
    /// </summary>
    public async Task<decimal?> GetYearlyBalanceAsync(string userId, int vacationTypeId, bool asAdmin, CancellationToken ct = default)
    {
        var type = await _db.VacationTypes
            .Where(t => t.Id == vacationTypeId)
            .Select(t => new { t.Name, t.IsUnlimited, t.IsAdminOnly })
            .FirstOrDefaultAsync(ct);
        if (type is { IsAdminOnly: true } && !asAdmin) throw AdminOnly(type.Name);
        if (type is { IsUnlimited: true }) return null;

        var balance = await _db.EmployeeVacationBalances
            .FirstOrDefaultAsync(b => b.UserId == userId && b.VacationTypeId == vacationTypeId, ct)
            ?? throw new ResourceNotFoundException("This vacation type is not assigned to you.");
        return balance.YearlyBalance;
    }

    public Task<decimal> GetUsedVacationDaysAsync(
        string userId, int vacationTypeId, int year, int? excludeId = null, CancellationToken ct = default)
    {
        var query = _db.VacationDays
            .Where(d => d.UserId == userId && d.VacationTypeId == vacationTypeId && d.Date.Year == year);

        if (excludeId.HasValue)
            query = query.Where(d => d.Id != excludeId.Value);

        return query.SumAsync(d => d.Amount, ct);
    }

    private static ValidationException AdminOnly(string typeName) =>
        new($"Only an admin can register or change \"{typeName}\" days.");

    public static void ValidateAmount(decimal amount)
    {
        if (amount != 0.5m && amount != 1.0m)
            throw new InvalidVacationAmountException("Amount must be 0.5 (half day) or 1.0 (full day).");
    }

    /// <summary>Days with a dot and no trailing zeros (0.5, 10), independent of the server's culture.</summary>
    public static string FormatDays(decimal days) =>
        days.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
}
