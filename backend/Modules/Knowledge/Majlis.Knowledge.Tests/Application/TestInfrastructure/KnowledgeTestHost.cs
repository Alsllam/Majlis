using FakeItEasy;
using FluentValidation;
using Majlis.Framework.Application.Storage;
using Majlis.Framework.Domain.Events;
using Majlis.Framework.Domain.Security;
using Majlis.Framework.EntityFrameworkCore.Extensions;
using Majlis.Knowledge.Application;
using Majlis.Knowledge.Application.Documents;
using Majlis.Knowledge.Domain.Entities;
using Majlis.Knowledge.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Majlis.Knowledge.Tests.Application.TestInfrastructure;

public sealed class TestCurrentUser : ICurrentUser
{
    public Guid? Id { get; set; }
    public Guid? TenantId { get; set; }
    public string? DisplayName { get; set; }
    public bool IsAuthenticated => Id is not null;
    public IReadOnlyCollection<string> Roles { get; set; } = [];
    public Guid GetRequiredId() => Id ?? throw new InvalidOperationException();
    public Guid GetRequiredTenantId() => TenantId ?? throw new InvalidOperationException();
}

/// <summary>Documents app service over EF Core InMemory with blob storage and the publisher faked.</summary>
public sealed class KnowledgeTestHost : IDisposable
{
    public static readonly Guid TenantId = Guid.NewGuid();
    public static readonly Guid WorkspaceId = Guid.NewGuid();
    public static readonly (Guid Id, string Name) Sara = (Guid.NewGuid(), "سارة");
    public static readonly (Guid Id, string Name) Khalid = (Guid.NewGuid(), "خالد");
    public static readonly (Guid Id, string Name) Noura = (Guid.NewGuid(), "نورة");
    public static readonly (Guid Id, string Name) Outsider = (Guid.NewGuid(), "زائر");

    private readonly ServiceProvider _provider;

    public KnowledgeTestHost()
    {
        var services = new ServiceCollection();
        var dbName = Guid.NewGuid().ToString();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<TestCurrentUser>();
        services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<TestCurrentUser>());
        services.AddDbContext<KnowledgeDbContext>(o => o.UseInMemoryDatabase(dbName));
        services.AddMajlisRepositories<KnowledgeDbContext>();
        services.AddSingleton(Publisher);
        services.AddSingleton(Blobs);
        services.AddSingleton(Options.Create(new KnowledgeOptions { MaxFileSizeMb = 1 }));
        services.AddValidatorsFromAssembly(typeof(KnowledgeApplicationModule).Assembly);
        services.AddScoped<DocumentsAppService>();
        _provider = services.BuildServiceProvider();

        A.CallTo(() => Blobs.CreateUploadUrlAsync(A<string>._, A<string>._, A<string>._, A<TimeSpan>._, A<CancellationToken>._))
            .ReturnsLazily((string c, string p, string _, TimeSpan _, CancellationToken _) => new PresignedUrl(new Uri($"http://blob.local/{c}/{p}?sig=x"), DateTimeOffset.UtcNow.AddMinutes(15)));
        A.CallTo(() => Blobs.CreateDownloadUrlAsync(A<string>._, A<string>._, A<string>._, A<TimeSpan>._, A<CancellationToken>._))
            .ReturnsLazily((string c, string p, string _, TimeSpan _, CancellationToken _) => new PresignedUrl(new Uri($"http://blob.local/{c}/{p}?sig=r"), DateTimeOffset.UtcNow.AddMinutes(10)));
    }

    public IEventPublisher Publisher { get; } = A.Fake<IEventPublisher>();

    public IBlobStorage Blobs { get; } = A.Fake<IBlobStorage>();

    public IServiceScope Scope((Guid Id, string Name) user)
    {
        var scope = _provider.CreateScope();
        var current = scope.ServiceProvider.GetRequiredService<TestCurrentUser>();
        current.Id = user.Id;
        current.TenantId = TenantId;
        current.DisplayName = user.Name;
        return scope;
    }

    public DocumentsAppService Documents(IServiceScope scope) => scope.ServiceProvider.GetRequiredService<DocumentsAppService>();

    /// <summary>Sara owns the workspace, Khalid contributes, Noura only watches.</summary>
    public async Task SeedMembershipsAsync()
    {
        using var scope = Scope(Sara);
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeDbContext>();
        db.WorkspaceMemberships.AddRange(
            new WorkspaceMembership(TenantId, WorkspaceId, Sara.Id, Sara.Name, "Owner"),
            new WorkspaceMembership(TenantId, WorkspaceId, Khalid.Id, Khalid.Name, "Contributor"),
            new WorkspaceMembership(TenantId, WorkspaceId, Noura.Id, Noura.Name, "Viewer"));
        await db.SaveChangesAsync();
    }

    public void Dispose() => _provider.Dispose();
}
