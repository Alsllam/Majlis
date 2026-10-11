using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace Majlis.Framework.Application.Security;

/// <summary>Client-credentials settings for service-to-service calls (secret from the environment, never appsettings).</summary>
public sealed class InternalAuthOptions
{
    public string TokenEndpoint { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string Scope { get; set; } = InternalServicePolicy.Scope;
}

/// <summary>Gets and caches a client-credentials access token from the Auth host.</summary>
public sealed class ServiceTokenProvider(HttpClient http, IOptions<InternalAuthOptions> options, TimeProvider clock) : IDisposable
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private string? _token;
    private DateTimeOffset _expiresAt;

    public async Task<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        if (_token is not null && clock.GetUtcNow() < _expiresAt)
        {
            return _token;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_token is not null && clock.GetUtcNow() < _expiresAt)
            {
                return _token;
            }

            var o = options.Value;
            using var response = await http.PostAsync(new Uri(o.TokenEndpoint), new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = o.ClientId,
                ["client_secret"] = o.ClientSecret,
                ["scope"] = o.Scope,
            }), cancellationToken);
            response.EnsureSuccessStatusCode();
            var token = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken)
                ?? throw new InvalidOperationException("Empty token response.");
            _token = token.AccessToken;
            _expiresAt = clock.GetUtcNow().AddSeconds(Math.Max(30, token.ExpiresIn - 60));
            return _token;
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Dispose() => _lock.Dispose();

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}

/// <summary>Adds the service token to outgoing internal calls.</summary>
public sealed class ServiceTokenHandler(ServiceTokenProvider tokens) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await tokens.GetTokenAsync(cancellationToken));
        return await base.SendAsync(request, cancellationToken);
    }
}
