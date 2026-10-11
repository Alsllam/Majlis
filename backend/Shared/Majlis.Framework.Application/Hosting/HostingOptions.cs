namespace Majlis.Framework.Application.Hosting;

public sealed class CorsSettings
{
    public string[] AllowedOrigins { get; set; } = [];
}

public sealed class AuthSettings
{
    /// <summary>OpenIddict issuer (the Auth host), e.g. <c>http://localhost:7001/</c>.</summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>Audience this API accepts.</summary>
    public string Audience { get; set; } = "majlis-api";
}

public sealed class RabbitMqSettings
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string VirtualHost { get; set; } = "majlis";
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public sealed class RateLimiterSettings
{
    public int PermitLimit { get; set; } = 300;
    public int WindowSeconds { get; set; } = 60;
    public int SegmentsPerWindow { get; set; } = 6;
}
