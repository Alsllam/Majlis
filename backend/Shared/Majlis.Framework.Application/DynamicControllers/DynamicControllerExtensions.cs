using System.Reflection;
using Majlis.Framework.Application.Services;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.DependencyInjection;

namespace Majlis.Framework.Application.DynamicControllers;

public static class DynamicControllerExtensions
{
    /// <summary>Exposes every <see cref="ApplicationService"/> in <paramref name="assembly"/> as a controller.</summary>
    public static IMvcBuilder AddDynamicControllers(this IMvcBuilder mvc, Assembly assembly)
    {
        mvc.PartManager.ApplicationParts.Add(new AssemblyPart(assembly));
        mvc.PartManager.FeatureProviders.Add(new AppServiceControllerFeatureProvider());
        return mvc;
    }

    private sealed class AppServiceControllerFeatureProvider : ControllerFeatureProvider
    {
        protected override bool IsController(TypeInfo typeInfo)
            => typeInfo is { IsClass: true, IsAbstract: false, IsPublic: true, ContainsGenericParameters: false }
               && typeof(ApplicationService).IsAssignableFrom(typeInfo);
    }
}
