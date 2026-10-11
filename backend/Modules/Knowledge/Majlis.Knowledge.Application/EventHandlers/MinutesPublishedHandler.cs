using System.Text;
using Majlis.Framework.Application.Storage;
using Majlis.Framework.Domain.Events;
using Majlis.Framework.Domain.Repositories;
using Majlis.Knowledge.Domain.Constants;
using Majlis.Knowledge.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Wolverine.Attributes;

namespace Majlis.Knowledge.Application.EventHandlers;

/// <summary>
/// Approved meeting minutes become a workspace document (FR-MTG-005): the markdown is stored by the host and queued
/// for indexing like an upload. One document per meeting; a redelivered message changes nothing.
/// </summary>
[WolverineHandler]
public static class MinutesPublishedHandler
{
    public static async Task Handle(MinutesPublished m, IRepository<Document, Guid> documents, IBlobStorage blobs, IEventPublisher publisher, IUnitOfWork unitOfWork, TimeProvider clock, CancellationToken cancellationToken)
    {
        if (await documents.Query().AnyAsync(d => d.MeetingId == m.MeetingId, cancellationToken))
        {
            return;
        }

        var content = Encoding.UTF8.GetBytes(m.Markdown);
        var documentId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var blobPath = $"{m.TenantId:D}/{m.WorkspaceId:D}/{documentId:D}/{versionId:D}/minutes.md";
        await blobs.UploadAsync(KnowledgeFieldDefinitions.Container, blobPath, content, "text/markdown", cancellationToken);
        var title = m.Title.Length > KnowledgeFieldDefinitions.MaxTitleLength ? m.Title[..KnowledgeFieldDefinitions.MaxTitleLength] : m.Title;
        var document = Document.CreateFromMinutes(documentId, m.TenantId, m.WorkspaceId, null, title, m.Language, m.MeetingId, versionId, "minutes.md", "text/markdown", content.Length, blobPath, clock.GetUtcNow().UtcDateTime);
        var version = document.LatestVersion!;
        await documents.InsertAsync(document, autoSave: false, cancellationToken);
        await publisher.PublishAsync(
            new DocumentUploaded(document.TenantId, document.WorkspaceId, document.RoomId, document.Id, version.Id, version.BlobPath, version.FileName, version.ContentType, document.Title, document.Type.ToString(), document.Language, document.AclGroups),
            cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
