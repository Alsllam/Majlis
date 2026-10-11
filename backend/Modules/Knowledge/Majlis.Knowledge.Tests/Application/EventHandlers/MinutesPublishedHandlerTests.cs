using FakeItEasy;
using Majlis.Framework.Application.Storage;
using Majlis.Framework.Domain.Events;
using Majlis.Framework.Domain.Repositories;
using Majlis.Knowledge.Application.Documents.DTOs;
using Majlis.Knowledge.Application.EventHandlers;
using Majlis.Knowledge.Domain.Entities;
using Majlis.Knowledge.Domain.Enums;
using Majlis.Knowledge.Tests.Application.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;
using static Majlis.Knowledge.Tests.Application.TestInfrastructure.KnowledgeTestHost;

namespace Majlis.Knowledge.Tests.Application.EventHandlers;

public class MinutesPublishedHandlerTests : IDisposable
{
    private readonly KnowledgeTestHost _host = new();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _host.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Handle_ShouldStoreTheMinutesAsAnActiveDocumentOnce_AndQueueIndexing()
    {
        await _host.SeedMembershipsAsync();
        var meetingId = Guid.NewGuid();
        var message = new MinutesPublished(TenantId, WorkspaceId, Guid.NewGuid(), meetingId, "محضر اجتماع: المتابعة", "# المتابعة\n\n## الملخص\n\nتم.\n", "ar", Khalid.Id, Khalid.Name);

        async Task HandleAsync()
        {
            using var scope = _host.Scope(Sara);
            var sp = scope.ServiceProvider;
            await MinutesPublishedHandler.Handle(message, sp.GetRequiredService<IRepository<Document, Guid>>(), sp.GetRequiredService<IBlobStorage>(), sp.GetRequiredService<IEventPublisher>(), sp.GetRequiredService<IUnitOfWork>(), TimeProvider.System, Ct);
        }

        await HandleAsync();
        await HandleAsync(); // redelivered

        using var khalid = _host.Scope(Khalid);
        var list = await _host.Documents(khalid).GetListAsync(new FilterDocumentDto { WorkspaceId = WorkspaceId, SkipCount = 0, MaxResultCount = 10 }, Ct);
        var row = Assert.Single(list.Items);
        Assert.Equal((DocumentOrigin.Meeting, DocumentStatus.Active, IngestionStatus.Queued, "محضر اجتماع: المتابعة", "ar"), (row.Origin, row.DocumentStatus, row.Status, row.Title, row.Language));
        A.CallTo(() => _host.Blobs.UploadAsync("documents", A<string>.That.EndsWith("/minutes.md"), A<byte[]>._, "text/markdown", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _host.Publisher.PublishAsync(A<DocumentUploaded>.That.Matches(e => e.DocumentId == row.Id && e.ContentType == "text/markdown" && e.FileName == "minutes.md"), A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }
}
