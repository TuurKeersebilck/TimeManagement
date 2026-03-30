using Microsoft.Extensions.Caching.Memory;

namespace TimeManagementBackend.Services;

public interface ITokenBlacklistService
{
    void Revoke(string jti, DateTime tokenExpiry);
    bool IsRevoked(string jti);
}

public class TokenBlacklistService : ITokenBlacklistService
{
    private readonly IMemoryCache _cache;

    public TokenBlacklistService(IMemoryCache cache)
    {
        _cache = cache;
    }

    public void Revoke(string jti, DateTime tokenExpiry)
    {
        var ttl = tokenExpiry - DateTime.UtcNow;
        if (ttl > TimeSpan.Zero)
            _cache.Set($"revoked:{jti}", true, ttl);
    }

    public bool IsRevoked(string jti) =>
        _cache.TryGetValue($"revoked:{jti}", out _);
}
