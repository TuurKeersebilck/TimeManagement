using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using TimeManagementBackend.Config;
using TimeManagementBackend.Models;
using TimeManagementBackend.Services;

namespace TimeManagementBackend.Tests.Services;

/// <summary>
/// The JWT is the only thing standing between a request and someone else's timesheet, so these
/// tests assert the exact claim shape the authorization pipeline reads, and that a token signed
/// with any other key is rejected outright.
/// </summary>
public class JwtServiceTests
{
    private const string Secret = "a-test-signing-key-of-at-least-32-characters";

    private static readonly JwtConfig Config = new()
    {
        Secret = Secret,
        Issuer = "TimeManagementAPI",
        Audience = "TimeManagementAPI",
        ExpiryInMinutes = 60,
    };

    private static readonly User Admin = new()
    {
        Id = "user-123",
        UserName = "emma@example.test",
        Email = "emma@example.test",
        FullName = "Emma Employee",
        Role = UserRole.Admin,
    };

    private static JwtSecurityToken Read(string token) => new JwtSecurityTokenHandler().ReadJwtToken(token);

    [Fact]
    public void Token_CarriesTheClaimsTheApiAuthorisesOn()
    {
        var token = Read(new JwtService(Config).GenerateToken(Admin));

        Assert.Equal("user-123", token.Claims.Single(c => c.Type == ClaimTypes.NameIdentifier).Value);
        Assert.Equal("emma@example.test", token.Claims.Single(c => c.Type == ClaimTypes.Name).Value);
        Assert.Equal("emma@example.test", token.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Email).Value);
        // The role claim is what [Authorize(Roles = "Admin")] matches on.
        Assert.Equal("Admin", token.Claims.Single(c => c.Type == ClaimTypes.Role).Value);
    }

    [Fact]
    public void Token_UsesTheConfiguredIssuerAndAudience()
    {
        var token = Read(new JwtService(Config).GenerateToken(Admin));

        Assert.Equal("TimeManagementAPI", token.Issuer);
        Assert.Contains("TimeManagementAPI", token.Audiences);
    }

    [Fact]
    public void Token_CarriesAUniqueJtiSoItCanBeRevoked()
    {
        // Logout revokes by jti; two tokens sharing one would revoke both sessions at once.
        var service = new JwtService(Config);

        var first = Read(service.GenerateToken(Admin)).Claims.Single(c => c.Type == JwtRegisteredClaimNames.Jti).Value;
        var second = Read(service.GenerateToken(Admin)).Claims.Single(c => c.Type == JwtRegisteredClaimNames.Jti).Value;

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Token_ExpiresAfterTheConfiguredLifetime()
    {
        var token = Read(new JwtService(Config).GenerateToken(Admin));

        Assert.InRange(token.ValidTo, DateTime.UtcNow.AddMinutes(58), DateTime.UtcNow.AddMinutes(62));
    }

    [Fact]
    public void Token_HonoursAnExplicitLifetimeForRememberMe()
    {
        var token = Read(new JwtService(Config).GenerateToken(Admin, expiryMinutes: 60 * 24 * 90));

        Assert.InRange(token.ValidTo, DateTime.UtcNow.AddDays(89), DateTime.UtcNow.AddDays(91));
    }

    [Fact]
    public void EmployeeTokens_DoNotCarryTheAdminRole()
    {
        var employee = new User { Id = "u2", Email = "e@example.test", UserName = "e@example.test" };

        var token = Read(new JwtService(Config).GenerateToken(employee));

        Assert.Equal("Employee", token.Claims.Single(c => c.Type == ClaimTypes.Role).Value);
    }

    [Fact]
    public void Token_ValidatesAgainstTheConfiguredSigningKey()
    {
        var raw = new JwtService(Config).GenerateToken(Admin);

        var principal = new JwtSecurityTokenHandler().ValidateToken(raw, new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = Config.Issuer,
            ValidAudience = Config.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)),
            ClockSkew = TimeSpan.Zero,
        }, out _);

        Assert.Equal("user-123", principal.FindFirst(ClaimTypes.NameIdentifier)?.Value);
    }

    [Fact]
    public void Token_IsRejectedWhenValidatedWithADifferentKey()
    {
        var raw = new JwtService(Config).GenerateToken(Admin);

        Assert.Throws<SecurityTokenSignatureKeyNotFoundException>(() =>
            new JwtSecurityTokenHandler().ValidateToken(raw, new TokenValidationParameters
            {
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = false,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes("a-completely-different-key-of-32-chars!")),
            }, out _));
    }
}
