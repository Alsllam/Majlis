using System.Reflection;
using Majlis.Approvals.Application.Requests;
using Majlis.Framework.Application.Security;
using Microsoft.AspNetCore.Mvc.Routing;

namespace Majlis.Approvals.Tests.Application.AppServices;

public class PermissionAttributeTests
{
    [Fact]
    public void Endpoints_ShouldRequireAPermission_WhenExposedToUsers()
    {
        var endpoints = typeof(ApprovalRequestsAppService).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttributes<HttpMethodAttribute>().Any())
            .ToList();

        Assert.NotEmpty(endpoints);
        Assert.All(endpoints, m => Assert.NotNull(m.GetCustomAttribute<HasPermissionAttribute>()));
    }
}
