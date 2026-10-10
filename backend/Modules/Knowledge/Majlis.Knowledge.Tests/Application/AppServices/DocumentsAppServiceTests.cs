using FakeItEasy;
using Majlis.Framework.Application.Storage;
using Majlis.Framework.Domain.Events;
using Majlis.Framework.Domain.Exceptions;
using Majlis.Knowledge.Application.Documents.DTOs;
using Majlis.Knowledge.Domain.Constants;
using Majlis.Knowledge.Domain.Enums;
using Majlis.Knowledge.Tests.Application.TestInfrastructure;
using static Majlis.Knowledge.Tests.Application.TestInfrastructure.KnowledgeTestHost;

namespace Majlis.Knowledge.Tests.Application.AppServices;

public class DocumentsAppServiceTests : IDisposable
{
    private readonly KnowledgeTestHost _host = new();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _host.Dispose();
        GC.SuppressFinalize(this);
    }

    private static BeginUploadDto Pdf(long size = 1024) => new()
    {
        WorkspaceId = WorkspaceId, FileName = "عقد المورد.pdf", ContentType = "application/pdf", SizeBytes = size, Type = DocumentType.Contract,
    };

    [Fact]
    public async Task BeginUploadAsync_ShouldCreateDocumentAndPendingVersion_WithAPresignedUrl()
    {
        await _host.SeedMembershipsAsync();
        using var scope = _host.Scope(Khalid);

        var ticket = await _host.Documents(scope).BeginUploadAsync(Pdf(), Ct);

        Assert.Contains($"{TenantId:D}/{WorkspaceId:D}/{ticket.DocumentId:D}/{ticket.VersionId:D}/", ticket.UploadUrl.ToString());
        Assert.Equal("BlockBlob", ticket.Headers["x-ms-blob-type"]);
        var document = await _host.Documents(scope).GetAsync(new DocumentIdDto(ticket.DocumentId), Ct);
        Assert.Equal("عقد المورد", document.Title);
        Assert.Equal(IngestionStatus.Pending, Assert.Single(document.Versions).Status);
        A.CallTo(() => _host.Publisher.PublishAsync(A<DocumentUploaded>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task BeginUploadAsync_ShouldRejectViewersOutsidersBigFilesAndUnknownTypes()
    {
        await _host.SeedMembershipsAsync();
        using (var viewer = _host.Scope(Noura))
        {
            var ex = await Assert.ThrowsAsync<ForbiddenException>(() => _host.Documents(viewer).BeginUploadAsync(Pdf(), Ct));
            Assert.Equal(KnowledgeErrors.NotWorkspaceMember, ex.MessageKey);
        }

        using (var outsider = _host.Scope(Outsider))
        {
            await Assert.ThrowsAsync<ForbiddenException>(() => _host.Documents(outsider).BeginUploadAsync(Pdf(), Ct));
        }

        using var khalid = _host.Scope(Khalid);
        var big = await Assert.ThrowsAsync<CustomValidationException>(() => _host.Documents(khalid).BeginUploadAsync(Pdf(2 * 1024 * 1024), Ct));
        Assert.Equal(KnowledgeErrors.FileTooLarge, big.MessageKey);
        await Assert.ThrowsAsync<FluentValidation.ValidationException>(() => _host.Documents(khalid).BeginUploadAsync(Pdf() with { ContentType = "application/zip" }, Ct));
    }

    [Fact]
    public async Task ConfirmUploadAsync_ShouldQueueIngestionAndPublishDocumentUploaded_WhenBlobExists()
    {
        await _host.SeedMembershipsAsync();
        using var scope = _host.Scope(Khalid);
        var ticket = await _host.Documents(scope).BeginUploadAsync(Pdf(), Ct);
        A.CallTo(() => _host.Blobs.GetPropertiesAsync(KnowledgeFieldDefinitions.Container, A<string>._, A<CancellationToken>._))
            .Returns(new StoredBlobInfo(2048, "application/pdf"));

        var document = await _host.Documents(scope).ConfirmUploadAsync(new ConfirmUploadDto { VersionId = ticket.VersionId }, Ct);

        var version = Assert.Single(document.Versions);
        Assert.Equal(IngestionStatus.Queued, version.Status);
        Assert.Equal(2048, version.SizeBytes);
        A.CallTo(() => _host.Publisher.PublishAsync(
                A<DocumentUploaded>.That.Matches(e => e.DocumentId == ticket.DocumentId && e.VersionId == ticket.VersionId && e.AclGroups.Contains($"ws:{WorkspaceId:D}") && e.DocType == "Contract"),
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();

        var again = await Assert.ThrowsAsync<ConflictException>(() => _host.Documents(scope).ConfirmUploadAsync(new ConfirmUploadDto { VersionId = ticket.VersionId }, Ct));
        Assert.Equal(KnowledgeErrors.VersionNotPending, again.MessageKey);
    }

    [Fact]
    public async Task ConfirmUploadAsync_ShouldThrowValidation_WhenBlobIsMissing()
    {
        await _host.SeedMembershipsAsync();
        using var scope = _host.Scope(Khalid);
        var ticket = await _host.Documents(scope).BeginUploadAsync(Pdf(), Ct);
        A.CallTo(() => _host.Blobs.GetPropertiesAsync(A<string>._, A<string>._, A<CancellationToken>._)).Returns((StoredBlobInfo?)null);

        var ex = await Assert.ThrowsAsync<CustomValidationException>(() => _host.Documents(scope).ConfirmUploadAsync(new ConfirmUploadDto { VersionId = ticket.VersionId }, Ct));
        Assert.Equal(KnowledgeErrors.FileNotUploaded, ex.MessageKey);
    }

    [Fact]
    public async Task DeleteAsync_ShouldPublishDocumentDeleted_AndHideTheDocument()
    {
        await _host.SeedMembershipsAsync();
        using var scope = _host.Scope(Khalid);
        var ticket = await _host.Documents(scope).BeginUploadAsync(Pdf(), Ct);

        await _host.Documents(scope).DeleteAsync(new DocumentIdDto(ticket.DocumentId), Ct);

        await Assert.ThrowsAsync<EntityNotFoundException>(() => _host.Documents(scope).GetAsync(new DocumentIdDto(ticket.DocumentId), Ct));
        A.CallTo(() => _host.Publisher.PublishAsync(A<DocumentDeleted>.That.Matches(e => e.DocumentId == ticket.DocumentId), A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task DeleteAsync_ShouldThrowForbidden_WhenAContributorDeletesSomeoneElsesDocument()
    {
        await _host.SeedMembershipsAsync();
        Guid id;
        using (var sara = _host.Scope(Sara))
        {
            id = (await _host.Documents(sara).BeginUploadAsync(Pdf(), Ct)).DocumentId;
        }

        using var khalid = _host.Scope(Khalid);
        await Assert.ThrowsAsync<ForbiddenException>(() => _host.Documents(khalid).DeleteAsync(new DocumentIdDto(id), Ct));
    }

    [Fact]
    public async Task GetListAsync_ShouldShowLatestVersionStatus_AndOnlyWorkspaceDocuments()
    {
        await _host.SeedMembershipsAsync();
        using var scope = _host.Scope(Sara);
        var ticket = await _host.Documents(scope).BeginUploadAsync(Pdf(), Ct);
        await _host.Documents(scope).BeginUploadAsync(Pdf() with { RoomId = Guid.NewGuid(), FileName = "room.md", ContentType = "text/markdown" }, Ct);

        var page = await _host.Documents(scope).GetListAsync(new FilterDocumentDto { WorkspaceId = WorkspaceId }, Ct);

        var row = Assert.Single(page.Items);
        Assert.Equal(ticket.DocumentId, row.Id);
        Assert.Equal(IngestionStatus.Pending, row.Status);
        Assert.Equal("عقد المورد.pdf", row.FileName);
    }
}
