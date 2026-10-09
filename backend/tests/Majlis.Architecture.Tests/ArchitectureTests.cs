using System.Reflection;
using NetArchTest.Rules;

namespace Majlis.Architecture.Tests;

/// <summary>Dependency rules from the backend skill (§2) and product invariant 8 (ADR-0006).</summary>
public class ArchitectureTests
{
    private static readonly Assembly[] Domains =
    [
        typeof(Majlis.Framework.Domain.Entities.Entity<>).Assembly,
        typeof(Majlis.Rooms.Domain.Entities.Room).Assembly,
        typeof(Majlis.Identity.Domain.Entities.Tenant).Assembly,
    ];

    private static readonly Assembly[] AllMajlis =
    [
        .. Domains,
        typeof(Majlis.Framework.EntityFrameworkCore.MajlisDbContext).Assembly,
        typeof(Majlis.Framework.Application.Services.ApplicationService).Assembly,
        typeof(Majlis.Rooms.EntityFrameworkCore.RoomsDbContext).Assembly,
        typeof(Majlis.Rooms.Application.RoomsApplicationModule).Assembly,
        typeof(Majlis.Identity.EntityFrameworkCore.MajlisIdentityDbContext).Assembly,
        typeof(Majlis.Identity.Application.IdentityApplicationModule).Assembly,
    ];

    public static TheoryData<string> DomainNames => new(Domains.Select(a => a.GetName().Name!));

    public static TheoryData<string> AllNames => new(AllMajlis.Select(a => a.GetName().Name!));

    private static Assembly Load(string name) => AllMajlis.First(a => a.GetName().Name == name);

    [Theory]
    [MemberData(nameof(DomainNames))]
    public void Domain_ShouldNotDependOnPersistenceOrWeb_WhenCompiled(string assembly)
    {
        var result = Types.InAssembly(Load(assembly))
            .ShouldNot()
            .HaveDependencyOnAny("Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore.Mvc", "Microsoft.AspNetCore.Http", "MassTransit")
            .GetResult();

        Assert.True(result.IsSuccessful, Failing(result));
    }

    [Fact]
    public void Rooms_ShouldNotReferenceOtherModules_WhenCompiled()
    {
        var rooms = AllMajlis.Where(a => a.GetName().Name!.StartsWith("Majlis.Rooms.", StringComparison.Ordinal)).ToArray();
        var result = Types.InAssemblies(rooms).ShouldNot().HaveDependencyOn("Majlis.Identity").GetResult();

        Assert.True(result.IsSuccessful, Failing(result));
    }

    [Fact]
    public void Identity_ShouldNotReferenceOtherModules_WhenCompiled()
    {
        var identity = AllMajlis.Where(a => a.GetName().Name!.StartsWith("Majlis.Identity.", StringComparison.Ordinal)).ToArray();
        var result = Types.InAssemblies(identity).ShouldNot().HaveDependencyOn("Majlis.Rooms").GetResult();

        Assert.True(result.IsSuccessful, Failing(result));
    }

    /// <summary>Invariant 8: Azure SDKs only inside adapter implementations, so the on-prem profile stays possible.</summary>
    [Theory]
    [MemberData(nameof(AllNames))]
    public void BusinessCode_ShouldNotUseAzureSdk_OutsideAdapters(string assembly)
    {
        var result = Types.InAssembly(Load(assembly))
            .That().DoNotResideInNamespaceContaining(".Adapters")
            .ShouldNot().HaveDependencyOnAny("Azure", "Microsoft.Azure")
            .GetResult();

        Assert.True(result.IsSuccessful, Failing(result));
    }

    private static string Failing(NetArchTest.Rules.TestResult result)
        => "Violations: " + string.Join(", ", result.FailingTypeNames ?? []);
}
