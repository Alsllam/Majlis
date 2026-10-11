using System.Net.Http.Headers;
using System.Net.Http.Json;
using Majlis.Framework.Application.Security;
using Microsoft.Extensions.Options;

namespace Majlis.Notifications.Application.Dispatch;

/// <summary>Who a notification goes to by email: address and language per user, from the Auth host.</summary>
public interface IRecipientLookup
{
    Task<List<RecipientDto>> LookupAsync(Guid tenantId, IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken);
}

public sealed record RecipientDto(Guid Id, string DisplayName, string Email, string PreferredLanguage, bool IsActive);

/// <summary>
/// Calls <c>POST /internal/users/lookup-by-ids</c> on the Auth host with a client-credentials token. Built without
/// <c>IHttpClientFactory</c> or Refit on purpose: Wolverine generates the handler code and refuses factory-registered
/// dependencies; a singleton with one long-lived <see cref="HttpClient"/> is the supported alternative.
/// </summary>
public sealed class IdentityRecipientLookup : IRecipientLookup, IDisposable
{
    private readonly HttpClient _http;
    private readonly ServiceTokenProvider _tokens;

    public IdentityRecipientLookup(IOptions<NotificationsOptions> options, IOptions<InternalAuthOptions> auth, TimeProvider clock)
    {
        _http = new HttpClient(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(5) })
        {
            BaseAddress = new Uri(options.Value.IdentityUrl.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromSeconds(10),
        };
        _tokens = new ServiceTokenProvider(new HttpClient(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(5) }), auth, clock);
    }

    public async Task<List<RecipientDto>> LookupAsync(Guid tenantId, IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "internal/users/lookup-by-ids")
        {
            Content = JsonContent.Create(new { tenantId, ids = userIds }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await _tokens.GetTokenAsync(cancellationToken));
        using var response = await _http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<RecipientDto>>(cancellationToken) ?? [];
    }

    public void Dispose()
    {
        _http.Dispose();
        _tokens.Dispose();
    }
}
