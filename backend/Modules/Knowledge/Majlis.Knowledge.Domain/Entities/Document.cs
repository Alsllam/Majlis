using Majlis.Framework.Domain.Entities;
using Majlis.Framework.Domain.Exceptions;
using Majlis.Knowledge.Domain.Constants;
using Majlis.Knowledge.Domain.Enums;

namespace Majlis.Knowledge.Domain.Entities;

/// <summary>A document in a workspace (or one room). Content lives in blob storage as versions (FR-KNW-004).</summary>
public class Document : FullAuditedEntity<Guid>, IMultiTenant
{
    private readonly List<DocumentVersion> _versions = [];

    protected Document()
    {
    }

    public Document(Guid id, Guid tenantId, Guid workspaceId, Guid? roomId, string title, DocumentType type, string? language, DocumentOrigin origin)
        : base(id)
    {
        TenantId = tenantId;
        WorkspaceId = workspaceId;
        RoomId = roomId;
        Title = title;
        Type = type;
        Language = language;
        Origin = origin;
    }

    public Guid TenantId { get; private set; }
    public Guid WorkspaceId { get; private set; }

    /// <summary>Set for room-only documents; null when the whole workspace may read it.</summary>
    public Guid? RoomId { get; private set; }

    public string Title { get; private set; } = string.Empty;
    public DocumentType Type { get; private set; }

    /// <summary>Detected during ingestion when not given.</summary>
    public string? Language { get; private set; }

    public DateOnly? EffectiveDate { get; private set; }

    /// <summary>Comma-separated tags (FR-KNW-005).</summary>
    public string? Tags { get; private set; }

    public DocumentOrigin Origin { get; private set; }

    /// <summary>The version the index serves; null until the first version is indexed.</summary>
    public Guid? CurrentVersionId { get; private set; }

    public IReadOnlyCollection<DocumentVersion> Versions => _versions;

    /// <summary>ACL groups the index stores for every chunk; retrieval filters on them (AI-RAG-004).</summary>
    public IReadOnlyList<string> AclGroups => RoomId is { } room ? [$"room:{room:D}"] : [$"ws:{WorkspaceId:D}"];

    public DocumentVersion AddVersion(Guid versionId, string fileName, string contentType, long sizeBytes, string blobPath)
    {
        var number = _versions.Count == 0 ? 1 : _versions.Max(v => v.Number) + 1;
        var version = new DocumentVersion(versionId, Id, number, fileName, contentType, sizeBytes, blobPath);
        _versions.Add(version);
        return version;
    }

    public DocumentVersion GetVersion(Guid versionId)
        => _versions.FirstOrDefault(v => v.Id == versionId) ?? throw new EntityNotFoundException();

    public void UpdateMetadata(string title, DocumentType type, DateOnly? effectiveDate, string? tags)
    {
        Title = title;
        Type = type;
        EffectiveDate = effectiveDate;
        Tags = tags;
    }

    /// <summary>Marks a version indexed and makes it current; older versions become superseded (FR-KNW-004).</summary>
    public void MarkIndexed(Guid versionId, int chunkCount, string sha256, string? language, int pageCount, DateTime at)
    {
        var version = GetVersion(versionId);
        version.MarkIndexed(chunkCount, sha256, pageCount, at);
        foreach (var older in _versions.Where(v => v.Id != versionId && v.Status == IngestionStatus.Indexed))
        {
            older.MarkSuperseded();
        }

        CurrentVersionId = versionId;
        Language ??= language;
    }

    public void MarkFailed(Guid versionId, string reasonKey, string? detail)
        => GetVersion(versionId).MarkFailed(reasonKey, detail);

    /// <summary>The latest version's status drives the list (FR-KNW-002).</summary>
    public DocumentVersion? LatestVersion => _versions.OrderByDescending(v => v.Number).FirstOrDefault();
}

public class DocumentVersion : CreationAuditedEntity<Guid>
{
    protected DocumentVersion()
    {
    }

    internal DocumentVersion(Guid id, Guid documentId, int number, string fileName, string contentType, long sizeBytes, string blobPath)
        : base(id)
    {
        DocumentId = documentId;
        Number = number;
        FileName = fileName;
        ContentType = contentType;
        SizeBytes = sizeBytes;
        BlobPath = blobPath;
        Status = IngestionStatus.Pending;
    }

    public Guid DocumentId { get; private set; }
    public int Number { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long SizeBytes { get; private set; }
    public string BlobPath { get; private set; } = string.Empty;
    public string? Sha256 { get; private set; }
    public IngestionStatus Status { get; private set; }
    public string? FailureReasonKey { get; private set; }
    public string? FailureDetail { get; private set; }
    public int ChunkCount { get; private set; }
    public int PageCount { get; private set; }
    public DateTime? ConfirmedAt { get; private set; }
    public DateTime? IndexedAt { get; private set; }

    /// <summary>The file is in storage; ingestion may start.</summary>
    public void Confirm(long sizeBytes, DateTime at)
    {
        if (Status != IngestionStatus.Pending)
        {
            throw new ConflictException(KnowledgeErrors.VersionNotPending);
        }

        SizeBytes = sizeBytes;
        ConfirmedAt = at;
        Status = IngestionStatus.Queued;
    }

    internal void MarkIndexed(int chunkCount, string sha256, int pageCount, DateTime at)
    {
        ChunkCount = chunkCount;
        Sha256 = sha256;
        PageCount = pageCount;
        IndexedAt = at;
        FailureReasonKey = null;
        FailureDetail = null;
        Status = IngestionStatus.Indexed;
    }

    internal void MarkFailed(string reasonKey, string? detail)
    {
        FailureReasonKey = reasonKey;
        FailureDetail = detail;
        Status = IngestionStatus.Failed;
    }

    internal void MarkSuperseded() => Status = IngestionStatus.Superseded;
}
