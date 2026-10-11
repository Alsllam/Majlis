using System.Reflection;
using Majlis.Framework.Application.Security;
using Majlis.Rooms.Application.Rooms;
using Majlis.Rooms.Application.Sessions;
using Microsoft.AspNetCore.Mvc.Routing;

namespace Majlis.Rooms.Tests.Application.AppServices;

public class PermissionAttributeTests
{
    public static TheoryData<Type> PublicAppServices => new() { typeof(RoomsAppService), typeof(SessionsAppService) };

    [Theory]
    [MemberData(nameof(PublicAppServices))]
    public void Endpoints_ShouldRequireAPermission_WhenExposedToUsers(Type appService)
    {
        var endpoints = appService.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttributes<HttpMethodAttribute>().Any())
            .ToList();

        Assert.NotEmpty(endpoints);
        Assert.All(endpoints, m => Assert.NotNull(m.GetCustomAttribute<HasPermissionAttribute>()));
    }
}
