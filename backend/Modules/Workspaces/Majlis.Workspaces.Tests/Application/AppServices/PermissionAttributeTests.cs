using System.Reflection;
using Majlis.Framework.Application.Security;
using Majlis.Workspaces.Application.Workspaces;
using Microsoft.AspNetCore.Mvc.Routing;

namespace Majlis.Workspaces.Tests.Application.AppServices;

public class PermissionAttributeTests
{
    [Fact]
    public void Endpoints_ShouldRequireAPermission_WhenExposedToUsers()
    {
        var endpoints = typeof(WorkspacesAppService).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttributes<HttpMethodAttribute>().Any())
            .ToList();

        Assert.NotEmpty(endpoints);
        Assert.All(endpoints, m => Assert.NotNull(m.GetCustomAttribute<HasPermissionAttribute>()));
    }
}
