using Microsoft.Extensions.Caching.Memory;

namespace TimeManagementBackend.Services;

// Swap this interface's registration in Program.cs to scale out:
//   - Single node: current MemoryCache implementation (no extra infrastructure)
//   - Multi-node / cloud: replace with a Redis-backed implementation
//     e.g. services.AddSingleton<ITokenBlacklistService, RedisTokenBlacklistService>();
public interface ITokenBlacklistService
{
    void Revoke(string jti, DateTime tokenExpiry);
    bool IsRevoked(string jti);
}

// In-memory implementation — revoked tokens are lost on restart and not shared
// across multiple server instances. Acceptable for a single-node deployment.
// For horizontal scaling, replace with a Redis-backed implementation.
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
