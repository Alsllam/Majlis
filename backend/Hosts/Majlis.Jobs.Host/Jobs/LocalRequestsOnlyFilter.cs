using Hangfire.Dashboard;

namespace Majlis.Jobs.Host.Jobs;

/// <summary>The dashboard is a development tool: reachable from the machine itself only (the BFF never routes it).</summary>
public sealed class LocalRequestsOnlyFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var http = context.GetHttpContext();
        var remote = http.Connection.RemoteIpAddress;
        return remote is not null && (System.Net.IPAddress.IsLoopback(remote) || remote.Equals(http.Connection.LocalIpAddress));
    }
}
