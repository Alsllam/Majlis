using System.Reflection;
using Majlis.Framework.Application.Security;
using Majlis.Knowledge.Application.Documents;
using Microsoft.AspNetCore.Mvc.Routing;

namespace Majlis.Knowledge.Tests.Application.AppServices;

public class PermissionAttributeTests
{
    [Fact]
    public void Endpoints_ShouldRequireAPermission_WhenExposedToUsers()
    {
        var endpoints = typeof(DocumentsAppService).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttributes<HttpMethodAttribute>().Any())
            .ToList();

        Assert.NotEmpty(endpoints);
        Assert.All(endpoints, m => Assert.NotNull(m.GetCustomAttribute<HasPermissionAttribute>()));
    }
}
