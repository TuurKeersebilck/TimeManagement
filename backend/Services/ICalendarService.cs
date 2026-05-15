namespace TimeManagementBackend.Services;

public interface ICalendarService
{
    Task<(bool HasToken, DateTimeOffset? ExpiresAt)> GetTokenInfoAsync(string userId, CancellationToken ct = default);
    Task<(string RawToken, DateTimeOffset ExpiresAt)> RegenerateTokenAsync(string userId, CancellationToken ct = default);
    Task<string?> GetIcsContentAsync(string rawToken, CancellationToken ct = default);
}
