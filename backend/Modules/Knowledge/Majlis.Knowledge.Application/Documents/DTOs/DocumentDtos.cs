using Majlis.Framework.Application.Dtos;
using Majlis.Knowledge.Domain.Enums;

namespace Majlis.Knowledge.Application.Documents.DTOs;

public sealed record DocumentVersionDto(
    Guid Id,
    int Number,
    string FileName,
    string ContentType,
    long SizeBytes,
    IngestionStatus Status,
    string? FailureReasonKey,
    int ChunkCount,
    int PageCount,
    DateTime CreationTime,
    DateTime? IndexedAt);

public sealed record DocumentDto(
    Guid Id,
    Guid WorkspaceId,
    Guid? RoomId,
    string Title,
    DocumentType Type,
    string? Language,
    DateOnly? EffectiveDate,
    string? Tags,
    DocumentOrigin Origin,
    Guid? CurrentVersionId,
    DateTime CreationTime,
    Guid? CreatorId,
    IReadOnlyList<DocumentVersionDto> Versions);

public sealed record DocumentListDto(
    Guid Id,
    Guid WorkspaceId,
    Guid? RoomId,
    string Title,
    DocumentType Type,
    string? Language,
    IngestionStatus Status,
    string? FailureReasonKey,
    string FileName,
    long SizeBytes,
    int PageCount,
    DateTime CreationTime,
    Guid? CreatorId);

public sealed record DocumentIdDto(Guid Id);

public sealed record FilterDocumentDto : BaseFilterRequestDto
{
    public Guid WorkspaceId { get; init; }
    public Guid? RoomId { get; init; }
    public IngestionStatus? Status { get; init; }
}

/// <summary>Step 1 of an upload: metadata + file facts → a pre-signed url (FR-KNW-001).</summary>
public sealed record BeginUploadDto
{
    public Guid WorkspaceId { get; init; }
    public Guid? RoomId { get; init; }

    /// <summary>Re-upload: a new version of this document instead of a new document (FR-KNW-004).</summary>
    public Guid? DocumentId { get; init; }

    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
    public string? Title { get; init; }
    public DocumentType Type { get; init; } = DocumentType.Other;
    public string? Language { get; init; }
}

public sealed record UploadTicketDto(Guid DocumentId, Guid VersionId, Uri UploadUrl, DateTimeOffset ExpiresAt, IReadOnlyDictionary<string, string> Headers);

/// <summary>Step 2: the browser finished the PUT; Knowledge checks the blob and queues ingestion.</summary>
public sealed record ConfirmUploadDto
{
    public Guid VersionId { get; init; }
}

public sealed record VersionIdDto
{
    public Guid VersionId { get; init; }
}

public sealed record DownloadUrlDto(Uri Url, DateTimeOffset ExpiresAt, string FileName);

public sealed record UpdateDocumentDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public DocumentType Type { get; init; }
    public DateOnly? EffectiveDate { get; init; }
    public string? Tags { get; init; }
}
