using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using Ical.Net.Serialization;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using TimeManagementBackend.Data;
using TimeManagementBackend.Models;

namespace TimeManagementBackend.Services;

public class CalendarService(AppDbContext db) : ICalendarService
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromDays(365);

    public async Task<(bool HasToken, DateTimeOffset? ExpiresAt)> GetTokenInfoAsync(
        string userId, CancellationToken ct = default)
    {
        var user = await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new InvalidOperationException("User not found.");

        var hasToken = user.CalendarTokenHash != null && user.CalendarTokenExpiresAt > DateTimeOffset.UtcNow;
        return (hasToken, hasToken ? user.CalendarTokenExpiresAt : null);
    }

    public async Task<(string RawToken, DateTimeOffset ExpiresAt)> RegenerateTokenAsync(
        string userId, CancellationToken ct = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new InvalidOperationException("User not found.");

        return await IssueTokenAsync(user, ct);
    }

    public async Task<string?> GetIcsContentAsync(string rawToken, CancellationToken ct = default)
    {
        var inputHash = ComputeHash(rawToken);

        var user = await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.CalendarTokenHash == inputHash, ct);

        if (user == null)
            return null;

        // Constant-time comparison of stored hash bytes vs computed hash bytes
        var storedHashBytes = Convert.FromHexString(user.CalendarTokenHash!);
        var inputHashBytes = Convert.FromHexString(inputHash);
        if (!CryptographicOperations.FixedTimeEquals(storedHashBytes, inputHashBytes))
            return null;

        if (user.CalendarTokenExpiresAt <= DateTimeOffset.UtcNow)
            return null;

        var vacationDays = await db.VacationDays
            .AsNoTracking()
            .Where(d => d.UserId == user.Id)
            .OrderBy(d => d.Date)
            .Select(d => new
            {
                d.Id,
                d.Date,
                d.Amount,
                VacationTypeName = d.VacationType.Name,
            })
            .ToListAsync(ct);

        return BuildIcs(user.FullName, vacationDays.Select(d => (d.Id, d.Date, d.Amount, d.VacationTypeName)));
    }

    private async Task<(string RawToken, DateTimeOffset ExpiresAt)> IssueTokenAsync(
        User user, CancellationToken ct)
    {
        var rawBytes = RandomNumberGenerator.GetBytes(32);
        var rawToken = WebEncoders.Base64UrlEncode(rawBytes);
        var hash = ComputeHash(rawToken);
        var expiresAt = DateTimeOffset.UtcNow.Add(TokenLifetime);

        user.CalendarTokenHash = hash;
        user.CalendarTokenExpiresAt = expiresAt;

        await db.SaveChangesAsync(ct);

        return (rawToken, expiresAt);
    }

    private static string ComputeHash(string rawToken)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

    private static string BuildIcs(string calendarName, IEnumerable<(int Id, DateOnly Date, decimal Amount, string VacationTypeName)> days)
    {
        var calendar = new Calendar();
        calendar.AddProperty("X-WR-CALNAME", calendarName + "'s Vacations");

        foreach (var (id, date, amount, typeName) in days)
        {
            var summary = amount == 0.5m ? $"{typeName} (½ day)" : typeName;
            var endDate = date.AddDays(1);

            var ev = new CalendarEvent
            {
                Summary = summary,
                DtStart = new CalDateTime(date.Year, date.Month, date.Day),
                DtEnd = new CalDateTime(endDate.Year, endDate.Month, endDate.Day),
                Uid = $"vacation-{id}@timemanagement",
                Status = EventStatus.Confirmed,
                DtStamp = new CalDateTime(DateTime.UtcNow),
            };

            calendar.Events.Add(ev);
        }

        return new CalendarSerializer().SerializeToString(calendar) ?? string.Empty;
    }
}
