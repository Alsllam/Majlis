using System.Security.Cryptography;
using System.Text.Json;
using StackExchange.Redis;

namespace Majlis.Framework.Application.Security;

/// <summary>Who a real-time ticket was issued to. Built from the validated access token.</summary>
public sealed record RealtimeTicket(Guid UserId, Guid TenantId, string DisplayName, IReadOnlyList<string> Roles, string? ClientIp);

/// <summary>
/// Short-lived (30 s), IP-bound tickets for the hub handshake, so access tokens never appear in URLs
/// (docs/architecture/realtime-collaboration.md §7). A ticket can be presented several times within its 30 seconds,
/// because SignalR repeats the URL on negotiate, connect and long-polling requests. Stored in Redis.
/// </summary>
public sealed class RealtimeTicketStore(IConnectionMultiplexer redis)
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(30);

    public async Task<string> IssueAsync(RealtimeTicket ticket)
    {
        var value = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
        await redis.GetDatabase().StringSetAsync(Key(value), JsonSerializer.Serialize(ticket), Lifetime);
        return value;
    }

    /// <summary>Returns the ticket when it exists, has not expired and was issued to <paramref name="clientIp"/>.</summary>
    public async Task<RealtimeTicket?> ValidateAsync(string value, string? clientIp)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length != 64)
        {
            return null;
        }

        var json = await redis.GetDatabase().StringGetAsync(Key(value));
        if (json.IsNullOrEmpty)
        {
            return null;
        }

        var ticket = JsonSerializer.Deserialize<RealtimeTicket>(json.ToString());
        return ticket is not null && (ticket.ClientIp is null || clientIp is null || ticket.ClientIp == clientIp) ? ticket : null;
    }

    private static string Key(string value) => $"majlis:ticket:{value}";
}
