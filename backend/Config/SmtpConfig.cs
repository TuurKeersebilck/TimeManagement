namespace TimeManagementBackend.Config;

public class SmtpConfig
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string User { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string From { get; set; } = string.Empty;

    /// <summary>
    /// Ask whether the mail server's certificate was revoked before connecting. On by default.
    /// macOS can't finish that check for certificates that only publish a revocation list (no OCSP),
    /// which includes Gmail's since mid-2026, so MailKit refuses to connect there. Turn it off
    /// (SMTP_CHECK_CERTIFICATE_REVOCATION=false) only for local development on a Mac: the connection
    /// stays encrypted and the certificate is still validated, only the revocation lookup is skipped.
    /// </summary>
    public bool CheckCertificateRevocation { get; set; } = true;

    /// <summary>Only an explicit "false" turns the check off; anything else, or nothing, keeps it on.</summary>
    public static bool ParseCheckCertificateRevocation(string? value) =>
        !string.Equals(value?.Trim(), "false", StringComparison.OrdinalIgnoreCase);
}
