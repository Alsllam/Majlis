using System.Text;
using FakeItEasy;
using Majlis.Framework.Application.Storage;
using Majlis.Framework.Domain.Events;
using Majlis.Framework.Domain.Exceptions;
using Majlis.Framework.Domain.Repositories;
using Majlis.Knowledge.Application.Documents.DTOs;
using Majlis.Knowledge.Application.EventHandlers;
using Majlis.Knowledge.Domain.Entities;
using Majlis.Knowledge.Domain.Enums;
using Majlis.Knowledge.Tests.Application.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;
using static Majlis.Knowledge.Tests.Application.TestInfrastructure.KnowledgeTestHost;

namespace Majlis.Knowledge.Tests.Application.EventHandlers;

public class ActionApprovedHandlerTests : IDisposable
{
    private readonly KnowledgeTestHost _host = new();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly Guid SessionId = Guid.NewGuid();

    public void Dispose()
    {
        _host.Dispose();
        GC.SuppressFinalize(this);
    }

    private static ActionApproved Approved(Guid requestId, (Guid Id, string Name) requester, string args, string tool = "draft_document")
        => new(TenantId, WorkspaceId, Guid.NewGuid(), SessionId, Guid.NewGuid(), requestId, tool, args, requester.Id, requester.Name, Sara.Id, Sara.Name);

    private async Task HandleAsync(ActionApproved message)
    {
        using var scope = _host.Scope(Sara);
        var sp = scope.ServiceProvider;
        await ActionApprovedHandler.Handle(message, sp.GetRequiredService<IRepository<Document, Guid>>(), sp.GetRequiredService<IReadOnlyRepository<WorkspaceMembership, Guid>>(), sp.GetRequiredService<IBlobStorage>(), sp.GetRequiredService<IEventPublisher>(), sp.GetRequiredService<IUnitOfWork>(), Ct);
    }

    [Fact]
    public async Task Handle_ShouldStoreADraftOnce_ThenAPersonAddsItToKnowledge()
    {
        await _host.SeedMembershipsAsync();
        var requestId = Guid.NewGuid();
        var message = Approved(requestId, Khalid, """{"title":"سياسة الحضور","contentMarkdown":"# سياسة الحضور\n\nيلتزم الموظف بالحضور.","type":"Policy","language":"ar"}""");
        byte[]? stored = null;
        A.CallTo(() => _host.Blobs.UploadAsync("documents", A<string>._, A<byte[]>._, "text/markdown", A<CancellationToken>._))
            .Invokes((string _, string _, byte[] content, string _, CancellationToken _) => stored = content);
        A.CallTo(() => _host.Blobs.DownloadAsync("documents", A<string>._, A<CancellationToken>._)).ReturnsLazily(() => stored);

        await HandleAsync(message);
        await HandleAsync(message); // redelivered

        using var scope = _host.Scope(Khalid);
        var list = await _host.Documents(scope).GetListAsync(new FilterDocumentDto { WorkspaceId = WorkspaceId, SkipCount = 0, MaxResultCount = 10 }, Ct);
        var row = Assert.Single(list.Items);
        Assert.Equal((DocumentOrigin.Agent, DocumentStatus.Draft, IngestionStatus.Stored, DocumentType.Policy), (row.Origin, row.DocumentStatus, row.Status, row.Type));
        var document = await _host.Documents(scope).GetAsync(new DocumentIdDto(row.Id), Ct);
        Assert.Equal((requestId, SessionId), (document.ApprovalRequestId, document.SessionId));
        A.CallTo(() => _host.Publisher.PublishAsync(A<ActionExecuted>.That.Matches(e => e.RequestId == requestId && e.EntityId == row.Id), A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _host.Publisher.PublishAsync(A<DocumentUploaded>._, A<CancellationToken>._)).MustNotHaveHappened();

        // The viewer reads the markdown back.
        var version = Assert.Single(document.Versions);
        var content = await _host.Documents(scope).GetContentAsync(new VersionIdDto { VersionId = version.Id }, Ct);
        Assert.StartsWith("# سياسة الحضور", content.Text, StringComparison.Ordinal);
        Assert.Equal(Encoding.UTF8.GetByteCount(content.Text), version.SizeBytes);

        // Noura (viewer) may not add it; Khalid may. Indexing starts only then, and only once.
        using (var noura = _host.Scope(Noura))
        {
            await Assert.ThrowsAsync<ForbiddenException>(() => _host.Documents(noura).AddToKnowledgeAsync(new DocumentIdDto(row.Id), Ct));
        }

        var added = await _host.Documents(scope).AddToKnowledgeAsync(new DocumentIdDto(row.Id), Ct);
        Assert.Equal(DocumentStatus.Active, added.DocumentStatus);
        Assert.Equal(IngestionStatus.Queued, Assert.Single(added.Versions).Status);
        A.CallTo(() => _host.Publisher.PublishAsync(A<DocumentUploaded>.That.Matches(e => e.DocumentId == row.Id && e.ContentType == "text/markdown"), A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        await Assert.ThrowsAsync<ConflictException>(() => _host.Documents(scope).AddToKnowledgeAsync(new DocumentIdDto(row.Id), Ct));
    }

    [Fact]
    public async Task Handle_ShouldReportFailure_WhenContentMissingOrRequesterCannotContribute()
    {
        await _host.SeedMembershipsAsync();
        var noContent = Guid.NewGuid();
        var byViewer = Guid.NewGuid();

        await HandleAsync(Approved(noContent, Khalid, """{"title":"بدون محتوى"}"""));
        await HandleAsync(Approved(byViewer, Noura, """{"title":"x","contentMarkdown":"y"}"""));
        await HandleAsync(Approved(Guid.NewGuid(), Khalid, """{"title":"x"}""", tool: "create_task"));

        using var scope = _host.Scope(Sara);
        Assert.Equal(0, (await _host.Documents(scope).GetListAsync(new FilterDocumentDto { WorkspaceId = WorkspaceId, SkipCount = 0, MaxResultCount = 10 }, Ct)).TotalCount);
        A.CallTo(() => _host.Publisher.PublishAsync(A<ActionExecutionFailed>.That.Matches(e => e.RequestId == noContent && e.Detail == "contentMarkdown"), A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _host.Publisher.PublishAsync(A<ActionExecutionFailed>.That.Matches(e => e.RequestId == byViewer), A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _host.Blobs.UploadAsync(A<string>._, A<string>._, A<byte[]>._, A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
    }
}
