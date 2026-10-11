using Majlis.Framework.Domain.Events;
using Majlis.Framework.Domain.Repositories;
using Majlis.Knowledge.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Wolverine.Attributes;

namespace Majlis.Knowledge.Application.EventHandlers;

/// <summary>ai-service finished indexing a version (idempotent: marking an indexed version again changes nothing).</summary>
[WolverineHandler]
public static class DocumentIndexedHandler
{
    public static async Task Handle(DocumentIndexed message, IRepository<Document, Guid> documents, IUnitOfWork unitOfWork, TimeProvider clock, CancellationToken cancellationToken)
    {
        var document = await documents.QueryTracked().IgnoreQueryFilters().Include(d => d.Versions)
            .FirstOrDefaultAsync(d => d.Id == message.DocumentId && !d.IsDeleted, cancellationToken);
        if (document is null || document.Versions.All(v => v.Id != message.VersionId))
        {
            return;
        }

        document.MarkIndexed(message.VersionId, message.ChunkCount, message.Sha256, message.Language, message.PageCount, clock.GetUtcNow().UtcDateTime);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

[WolverineHandler]
public static class DocumentIndexingFailedHandler
{
    public static async Task Handle(DocumentIndexingFailed message, IRepository<Document, Guid> documents, IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        var document = await documents.QueryTracked().IgnoreQueryFilters().Include(d => d.Versions)
            .FirstOrDefaultAsync(d => d.Id == message.DocumentId && !d.IsDeleted, cancellationToken);
        if (document is null || document.Versions.All(v => v.Id != message.VersionId))
        {
            return;
        }

        document.MarkFailed(message.VersionId, message.ReasonKey, message.Detail);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
