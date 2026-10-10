namespace Majlis.Framework.Domain.Events;

// Contracts shared by several modules and by ai-service. The JSON shape of every
// message is described in docs/architecture/events.schema.json.

/// <summary>Rooms appended an event to a session timeline. The Realtime host fans it out.</summary>
public sealed record SessionEventAppended(
    Guid TenantId,
    Guid WorkspaceId,
    Guid RoomId,
    Guid SessionId,
    long Seq,
    string Type,
    Guid? TurnId,
    DateTime At,
    string ActorKind,
    Guid? ActorId,
    string? ActorDisplayName,
    string DataJson) : IEvent;

/// <summary>Realtime: the last connection of a user left a session.</summary>
public sealed record SessionPresenceLost(Guid SessionId, Guid UserId, DateTime At) : IEvent;

/// <summary>Realtime: a user who had left a session is back.</summary>
public sealed record SessionPresenceRestored(Guid SessionId, Guid UserId, DateTime At) : IEvent;

/// <summary>Membership or role removed; Realtime drops the user's connections from the scope.</summary>
public sealed record AccessRevoked(Guid TenantId, Guid UserId, string ScopeType, Guid ScopeId) : IEvent;

/// <summary>ai-service finished a turn.</summary>
public sealed record TurnCompleted(
    Guid SessionId,
    Guid TurnId,
    string Text,
    IReadOnlyList<CitationContract> Citations,
    int InputTokens,
    int OutputTokens,
    int CachedTokens) : IEvent;

/// <summary>ai-service stopped a turn after a stop request.</summary>
public sealed record TurnStopped(Guid SessionId, Guid TurnId, string PartialText) : IEvent;

/// <summary>ai-service could not finish a turn.</summary>
public sealed record TurnFailed(Guid SessionId, Guid TurnId, string ReasonKey, string? PartialText) : IEvent;

public sealed record CitationContract(string Label, Guid DocumentId, Guid VersionId, string Title, int? Page, string Passage);

/// <summary>Knowledge → ai-service: a document version is in blob storage and should be indexed (idempotent on version + content hash).</summary>
public sealed record DocumentUploaded(
    Guid TenantId,
    Guid WorkspaceId,
    Guid? RoomId,
    Guid DocumentId,
    Guid VersionId,
    string BlobPath,
    string FileName,
    string ContentType,
    string Title,
    string DocType,
    string? Language,
    IReadOnlyList<string> AclGroups) : IEvent;

/// <summary>Knowledge → ai-service: remove every chunk of the document from the index.</summary>
public sealed record DocumentDeleted(Guid TenantId, Guid DocumentId) : IEvent;

/// <summary>ai-service → Knowledge: the version is searchable.</summary>
public sealed record DocumentIndexed(Guid TenantId, Guid DocumentId, Guid VersionId, int ChunkCount, string Sha256, string? Language, int PageCount) : IEvent;

/// <summary>ai-service → Knowledge: ingestion failed; <c>ReasonKey</c> is a localization key.</summary>
public sealed record DocumentIndexingFailed(Guid TenantId, Guid DocumentId, Guid VersionId, string ReasonKey, string? Detail) : IEvent;
