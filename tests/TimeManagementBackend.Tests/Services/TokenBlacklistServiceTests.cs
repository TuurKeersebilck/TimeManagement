using Microsoft.Extensions.Caching.Memory;
using TimeManagementBackend.Services;

namespace TimeManagementBackend.Tests.Services;

/// <summary>Logout revokes the presented token by its jti until that token would have expired.</summary>
public class TokenBlacklistServiceTests
{
    private static TokenBlacklistService NewService() =>
        new(new MemoryCache(new MemoryCacheOptions()));

    [Fact]
    public void AnUnknownTokenIsNotRevoked()
        => Assert.False(NewService().IsRevoked("never-seen"));

    [Fact]
    public void ARevokedTokenIsReportedAsRevoked()
    {
        var service = NewService();

        service.Revoke("jti-1", DateTime.UtcNow.AddMinutes(30));

        Assert.True(service.IsRevoked("jti-1"));
    }

    [Fact]
    public void RevokingOneTokenLeavesOtherSessionsAlone()
    {
        var service = NewService();

        service.Revoke("jti-1", DateTime.UtcNow.AddMinutes(30));

        Assert.False(service.IsRevoked("jti-2"));
    }

    [Fact]
    public void AnAlreadyExpiredTokenIsNotStored()
    {
        // It can never be presented successfully again, so caching it would only leak memory.
        var service = NewService();

        service.Revoke("jti-expired", DateTime.UtcNow.AddMinutes(-1));

        Assert.False(service.IsRevoked("jti-expired"));
    }
}
