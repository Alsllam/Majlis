using System.Reflection;
using Majlis.Framework.Application.Security;
using Majlis.Meetings.Application.Meetings;
using Microsoft.AspNetCore.Mvc.Routing;

namespace Majlis.Meetings.Tests.Application.AppServices;

public class PermissionAttributeTests
{
    [Fact]
    public void Endpoints_ShouldRequireAPermission_WhenExposedToUsers()
    {
        var endpoints = typeof(MeetingsAppService).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttributes<HttpMethodAttribute>().Any())
            .ToList();

        Assert.NotEmpty(endpoints);
        Assert.All(endpoints, m => Assert.NotNull(m.GetCustomAttribute<HasPermissionAttribute>()));
    }
}
