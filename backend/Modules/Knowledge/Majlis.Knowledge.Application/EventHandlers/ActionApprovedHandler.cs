using System.Text;
using System.Text.Json;
using Majlis.Framework.Application.Storage;
using Majlis.Framework.Domain.Events;
using Majlis.Framework.Domain.Repositories;
using Majlis.Knowledge.Domain.Constants;
using Majlis.Knowledge.Domain.Entities;
using Majlis.Knowledge.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Wolverine.Attributes;

namespace Majlis.Knowledge.Application.EventHandlers;

/// <summary>Arguments of the <c>draft_document</c> tool, as ai-service sends them (camelCase).</summary>
public sealed record DraftDocumentToolArgs
{
    public string? Title { get; init; }
    public string? ContentMarkdown { get; init; }
    public string? Type { get; init; }
    public string? Language { get; init; }
}

/// <summary>
/// Runs <c>draft_document</c> after approval (FR-KNW-009): stores the markdown as a draft document of the room's
/// workspace (one stored version, not indexed), idempotent on the approval id, and reports <c>ActionExecuted</c>.
/// A person later adds the draft to knowledge from the document page.
/// </summary>
[WolverineHandler]
public static class ActionApprovedHandler
{
    public const string Tool = "draft_document";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static async Task Handle(
        ActionApproved message,
        IRepository<Document, Guid> documents,
        IReadOnlyRepository<WorkspaceMembership, Guid> memberships,
        IBlobStorage blobs,
        IEventPublisher publisher,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        if (message.Tool != Tool)
        {
            return;
        }

        if (await documents.Query().AnyAsync(d => d.ApprovalRequestId == message.RequestId, cancellationToken))
        {
            return; // already executed (duplicate delivery)
        }

        var requester = await memberships.Query()
            .FirstOrDefaultAsync(m => m.WorkspaceId == message.WorkspaceId && m.UserId == message.RequestedByUserId, cancellationToken);
        if (requester is null || !requester.CanContribute)
        {
            await FailAsync(message, KnowledgeErrors.NotWorkspaceMember, null, publisher, unitOfWork, cancellationToken);
            return;
        }

        DraftDocumentToolArgs args;
        try
        {
            args = JsonSerializer.Deserialize<DraftDocumentToolArgs>(message.ArgsJson, Json) ?? new DraftDocumentToolArgs();
        }
        catch (JsonException)
        {
            args = new DraftDocumentToolArgs();
        }

        if (string.IsNullOrWhiteSpace(args.Title) || string.IsNullOrWhiteSpace(args.ContentMarkdown))
        {
            await FailAsync(message, "General:Fields:Required", string.IsNullOrWhiteSpace(args.Title) ? "title" : "contentMarkdown", publisher, unitOfWork, cancellationToken);
            return;
        }

        var title = args.Title.Trim();
        if (title.Length > KnowledgeFieldDefinitions.MaxTitleLength)
        {
            title = title[..KnowledgeFieldDefinitions.MaxTitleLength];
        }

        var type = Enum.TryParse<DocumentType>(args.Type, ignoreCase: true, out var t) ? t : DocumentType.Other;
        var language = args.Language is "ar" or "en" ? args.Language : null;
        var content = Encoding.UTF8.GetBytes(args.ContentMarkdown.Trim() + "\n");
        var documentId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var blobPath = $"{message.TenantId:D}/{message.WorkspaceId:D}/{documentId:D}/{versionId:D}/draft.md";
        await blobs.UploadAsync(KnowledgeFieldDefinitions.Container, blobPath, content, "text/markdown", cancellationToken);

        var document = Document.CreateDraft(
            documentId, message.TenantId, message.WorkspaceId, null, title, type, language, message.RequestId, message.SessionId,
            versionId, "draft.md", "text/markdown", content.Length, blobPath);
        await documents.InsertAsync(document, autoSave: false, cancellationToken);
        await publisher.PublishAsync(new ActionExecuted(message.TenantId, message.RequestId, Tool, document.Id, title), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static async Task FailAsync(ActionApproved message, string reasonKey, string? detail, IEventPublisher publisher, IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        await publisher.PublishAsync(new ActionExecutionFailed(message.TenantId, message.RequestId, Tool, reasonKey, detail), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
