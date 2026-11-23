using TimeManagementBackend.Config;

namespace TimeManagementBackend.Tests.Config;

/// <summary>
/// The revocation check guards production mail, so it must stay on unless someone explicitly
/// turns it off; a typo or an empty value must never disable it.
/// </summary>
public class SmtpConfigTests
{
    [Fact]
    public void RevocationCheck_IsOnByDefault() => Assert.True(new SmtpConfig().CheckCertificateRevocation);

    [Theory]
    [InlineData("false")]
    [InlineData("False")]
    [InlineData(" FALSE ")]
    public void RevocationCheck_OnlyAnExplicitFalseTurnsItOff(string value) =>
        Assert.False(SmtpConfig.ParseCheckCertificateRevocation(value));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("true")]
    [InlineData("0")]
    [InlineData("no")]
    [InlineData("fasle")]
    public void RevocationCheck_StaysOnForAnythingElse(string? value) =>
        Assert.True(SmtpConfig.ParseCheckCertificateRevocation(value));
}
