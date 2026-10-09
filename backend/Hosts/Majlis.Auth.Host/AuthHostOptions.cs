namespace Majlis.Auth.Host;

/// <summary>Token signing/encryption certificates. Required outside Development (PFX path + password from the secret store).</summary>
public sealed class AuthCertificateOptions
{
    public string? SigningPath { get; set; }
    public string? SigningPassword { get; set; }
    public string? EncryptionPath { get; set; }
    public string? EncryptionPassword { get; set; }
}

public sealed class AuthServerOptions
{
    /// <summary>Public issuer (the BFF), e.g. <c>http://localhost:7000/</c>.</summary>
    public string Issuer { get; set; } = string.Empty;

    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 14;
    public AuthCertificateOptions Certificates { get; set; } = new();
}
