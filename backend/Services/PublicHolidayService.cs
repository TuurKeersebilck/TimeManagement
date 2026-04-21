using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using TimeManagementBackend.Data;
using TimeManagementBackend.Exceptions;
using TimeManagementBackend.Models;
using TimeManagementBackend.Models.DTOs;

namespace TimeManagementBackend.Services;

public class PublicHolidayService(AppDbContext db, HttpClient httpClient) : IPublicHolidayService
{
    private const string NagerBaseUrl = "https://date.nager.at/api/v3";

    // ─── Configuration ────────────────────────────────────────────────────────

    public async Task<AppConfigurationDto> GetConfigurationAsync(CancellationToken ct = default)
    {
        var config = await db.AppConfigurations.FirstOrDefaultAsync(ct);
        if (config == null)
            return new AppConfigurationDto { EnableAdjustmentRequestEmails = true, EnableMissedClockInEmails = true };
        return ToConfigDto(config);
    }

    public async Task<AppConfigurationDto> SetDefaultTargetsAsync(decimal? dailyHours, decimal? weeklyHours, CancellationToken ct = default)
    {
        var config = await db.AppConfigurations.FirstOrDefaultAsync(ct);
        if (config == null)
        {
            config = new AppConfiguration { DefaultDailyHours = dailyHours, DefaultWeeklyHours = weeklyHours };
            db.AppConfigurations.Add(config);
        }
        else
        {
            config.DefaultDailyHours = dailyHours;
            config.DefaultWeeklyHours = weeklyHours;
        }

        await db.SaveChangesAsync(ct);
        return ToConfigDto(config);
    }

    public async Task<AppConfigurationDto> SetNotificationEmailAsync(string? email, CancellationToken ct = default)
    {
        var normalized = string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();

        var config = await db.AppConfigurations.FirstOrDefaultAsync(ct);
        if (config == null)
        {
            config = new AppConfiguration { NotificationEmail = normalized };
            db.AppConfigurations.Add(config);
        }
        else
        {
            config.NotificationEmail = normalized;
        }

        await db.SaveChangesAsync(ct);
        return ToConfigDto(config);
    }

    public async Task<AppConfigurationDto> SetCountryAsync(string countryCode, CancellationToken ct = default)
    {
        var normalized = countryCode.Trim().ToUpperInvariant();

        var config = await db.AppConfigurations.FirstOrDefaultAsync(ct);
        if (config == null)
        {
            config = new AppConfiguration { CountryCode = normalized };
            db.AppConfigurations.Add(config);
        }
        else
        {
            config.CountryCode = normalized;
        }

        await db.SaveChangesAsync(ct);

        // Eagerly refresh holidays for current and next year
        var currentYear = DateTime.UtcNow.Year;
        await FetchAndStoreAsync(normalized, currentYear, ct);
        await FetchAndStoreAsync(normalized, currentYear + 1, ct);

        return ToConfigDto(config);
    }

    // ─── Holidays ─────────────────────────────────────────────────────────────

    public async Task<IEnumerable<PublicHolidayDto>> GetHolidaysAsync(int year, CancellationToken ct = default)
    {
        var config = await db.AppConfigurations.FirstOrDefaultAsync(ct);
        if (config?.CountryCode == null) return [];

        // Auto-fetch if nothing stored yet for this year
        var exists = await db.PublicHolidays
            .AnyAsync(h => h.CountryCode == config.CountryCode && h.Year == year, ct);
        if (!exists)
            await FetchAndStoreAsync(config.CountryCode, year, ct);

        return await db.PublicHolidays
            .Where(h => h.CountryCode == config.CountryCode && h.Year == year)
            .OrderBy(h => h.Date)
            .Select(h => ToDto(h))
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<PublicHolidayDto>> RefreshHolidaysAsync(int year, CancellationToken ct = default)
    {
        var config = await db.AppConfigurations.FirstOrDefaultAsync(ct)
            ?? throw new ResourceNotFoundException("No country configured.");

        if (config.CountryCode == null)
            throw new ResourceNotFoundException("No country configured.");

        await FetchAndStoreAsync(config.CountryCode, year, ct, preserveOverrides: false);
        return await GetHolidaysAsync(year, ct);
    }

    public async Task<PublicHolidayDto> AddCustomHolidayAsync(CreateHolidayDto dto, CancellationToken ct = default)
    {
        var config = await db.AppConfigurations.FirstOrDefaultAsync(ct)
            ?? throw new ResourceNotFoundException("No country configured.");

        if (!DateOnly.TryParse(dto.Date, out var date))
            throw new ArgumentException("Invalid date format. Use YYYY-MM-DD.");

        var holiday = new PublicHoliday
        {
            Date = date,
            Name = dto.Name.Trim(),
            CountryCode = config.CountryCode ?? "CUSTOM",
            Year = date.Year,
            IsCustom = true,
        };

        db.PublicHolidays.Add(holiday);
        await db.SaveChangesAsync(ct);
        return ToDto(holiday);
    }

    public async Task<PublicHolidayDto> SetIsWorkingDayAsync(int id, bool isWorkingDay, CancellationToken ct = default)
    {
        var holiday = await db.PublicHolidays.FindAsync([id], ct)
            ?? throw new ResourceNotFoundException("Holiday not found.");

        holiday.IsWorkingDay = isWorkingDay;
        await db.SaveChangesAsync(ct);
        return ToDto(holiday);
    }

    public async Task DeleteHolidayAsync(int id, CancellationToken ct = default)
    {
        var holiday = await db.PublicHolidays.FindAsync([id], ct)
            ?? throw new ResourceNotFoundException("Holiday not found.");

        db.PublicHolidays.Remove(holiday);
        await db.SaveChangesAsync(ct);
    }

    // ─── Available countries ──────────────────────────────────────────────────

    public async Task<IEnumerable<AvailableCountryDto>> GetAvailableCountriesAsync(CancellationToken ct = default)
    {
        try
        {
            var countries = await httpClient.GetFromJsonAsync<List<NagerCountryDto>>(
                $"{NagerBaseUrl}/AvailableCountries", ct);

            if (countries == null) return [];
            return countries.Select(c => new AvailableCountryDto
            {
                CountryCode = c.CountryCode,
                Name = c.Name,
            }).OrderBy(c => c.Name);
        }
        catch
        {
            return [];
        }
    }

    // ─── Internals ────────────────────────────────────────────────────────────

    /// <param name="preserveOverrides">
    /// When true (auto-fetch), existing IsWorkingDay values are preserved so admin changes survive.
    /// When false (explicit refresh), always re-derive from the API Types field.
    /// </param>
    private async Task FetchAndStoreAsync(string countryCode, int year, CancellationToken ct, bool preserveOverrides = true)
    {
        try
        {
            var nagerHolidays = await httpClient.GetFromJsonAsync<List<NagerHolidayDto>>(
                $"{NagerBaseUrl}/PublicHolidays/{year}/{countryCode}", ct);

            if (nagerHolidays == null) return;

            var existing = await db.PublicHolidays
                .Where(h => h.CountryCode == countryCode && h.Year == year && !h.IsCustom)
                .ToListAsync(ct);

            // On explicit refresh, always use the API-derived defaults.
            // On auto-fetch, preserve any admin overrides so they survive incidental re-fetches.
            var overrides = preserveOverrides
                ? existing.ToDictionary(h => h.Date, h => h.IsWorkingDay)
                : [];

            db.PublicHolidays.RemoveRange(existing);

            var entities = nagerHolidays.Select(h =>
            {
                var date = DateOnly.Parse(h.Date);
                // Non-public holidays (Optional, Bank, Observance, etc.) default to working days.
                var apiDefault = !h.Types.Contains("Public", StringComparer.OrdinalIgnoreCase);
                return new PublicHoliday
                {
                    Date = date,
                    Name = h.LocalName,
                    CountryCode = countryCode,
                    Year = year,
                    IsCustom = false,
                    IsWorkingDay = overrides.TryGetValue(date, out var ov) ? ov : apiDefault,
                };
            });

            db.PublicHolidays.AddRange(entities);
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            // Don't throw — holiday fetch failure shouldn't break the app
        }
    }

    public async Task<AppConfigurationDto> SetNotificationTogglesAsync(bool enableAdjustmentRequestEmails, bool enableMissedClockInEmails, CancellationToken ct = default)
    {
        var config = await db.AppConfigurations.FirstOrDefaultAsync(ct);
        if (config == null)
        {
            config = new AppConfiguration
            {
                EnableAdjustmentRequestEmails = enableAdjustmentRequestEmails,
                EnableMissedClockInEmails = enableMissedClockInEmails,
            };
            db.AppConfigurations.Add(config);
        }
        else
        {
            config.EnableAdjustmentRequestEmails = enableAdjustmentRequestEmails;
            config.EnableMissedClockInEmails = enableMissedClockInEmails;
        }

        await db.SaveChangesAsync(ct);
        return ToConfigDto(config);
    }

    private static AppConfigurationDto ToConfigDto(AppConfiguration c) => new()
    {
        CountryCode = c.CountryCode,
        DefaultDailyHours = c.DefaultDailyHours,
        DefaultWeeklyHours = c.DefaultWeeklyHours,
        NotificationEmail = c.NotificationEmail,
        EnableAdjustmentRequestEmails = c.EnableAdjustmentRequestEmails,
        EnableMissedClockInEmails = c.EnableMissedClockInEmails,
    };

    private static PublicHolidayDto ToDto(PublicHoliday h) => new()
    {
        Id = h.Id,
        Date = h.Date.ToString("yyyy-MM-dd"),
        Name = h.Name,
        IsCustom = h.IsCustom,
        IsWorkingDay = h.IsWorkingDay,
    };
}
