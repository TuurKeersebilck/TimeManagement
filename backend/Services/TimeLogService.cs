using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;
using TimeManagementBackend.Data;
using TimeManagementBackend.Models;
using TimeManagementBackend.Models.DTOs;

namespace TimeManagementBackend.Services;

public class TimeLogService(AppDbContext db, IMapper mapper) : ITimeLogService
{
    private readonly AppDbContext _db = db;
    private readonly IMapper _mapper = mapper;

    public async Task<IEnumerable<TimeLogDto>> GetForUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _db.TimeLogs
            .AsNoTracking()
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.Date)
            .ProjectTo<TimeLogDto>(_mapper.ConfigurationProvider)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsForDateAsync(string userId, DateOnly date, int? excludeId = null, CancellationToken cancellationToken = default)
    {
        return await _db.TimeLogs
            .AnyAsync(t => t.UserId == userId
                && t.Date == date
                && (excludeId == null || t.Id != excludeId.Value),
                cancellationToken);
    }

    public async Task<TimeLogDto?> GetByIdAsync(int id, string userId, CancellationToken cancellationToken = default)
    {
        var t = await _db.TimeLogs
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, cancellationToken);

        return t == null ? null : _mapper.Map<TimeLogDto>(t);
    }

    public async Task<TimeLogDto> CreateAsync(TimeLogCreateDto createDto, string userId, CancellationToken cancellationToken = default)
    {
        var timeLog = _mapper.Map<TimeLog>(createDto);
        timeLog.UserId = userId;

        _db.TimeLogs.Add(timeLog);
        await _db.SaveChangesAsync(cancellationToken);

        return _mapper.Map<TimeLogDto>(timeLog);
    }

    public async Task<bool> UpdateAsync(int id, TimeLogUpdateDto updateDto, string userId, CancellationToken cancellationToken = default)
    {
        var existing = await _db.TimeLogs.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, cancellationToken);
        if (existing == null) return false;

        _mapper.Map(updateDto, existing);

        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(int id, string userId, CancellationToken cancellationToken = default)
    {
        var existing = await _db.TimeLogs.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, cancellationToken);
        if (existing == null) return false;

        _db.TimeLogs.Remove(existing);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<MyTargetDto> GetMyTargetAsync(string userId, CancellationToken cancellationToken = default)
    {
        var config = await _db.AppConfigurations.FirstOrDefaultAsync(cancellationToken);
        var target = await _db.EmployeeTargets.FirstOrDefaultAsync(t => t.UserId == userId, cancellationToken);

        return new MyTargetDto
        {
            DailyHours = target?.DailyHours ?? config?.DefaultDailyHours,
            WeeklyHours = target?.WeeklyHours ?? config?.DefaultWeeklyHours,
        };
    }
}