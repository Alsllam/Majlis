namespace Majlis.Knowledge.Domain.Enums;

/// <summary>Document type (FR-KNW-005); also a retrieval filter.</summary>
public enum DocumentType
{
    Other = 0,
    Regulation = 1,
    Contract = 2,
    Policy = 3,
    Sop = 4,
    Report = 5,
}

public enum DocumentOrigin
{
    Upload = 0,
    Agent = 1,

    /// <summary>Approved meeting minutes published by a person (FR-MTG-005).</summary>
    Meeting = 2,
}

/// <summary>FR-KNW-009: an agent draft is stored but not part of the knowledge until a person adds it.</summary>
public enum DocumentStatus
{
    Active = 0,
    Draft = 1,
}

/// <summary>Ingestion state of one version (FR-KNW-002).</summary>
public enum IngestionStatus
{
    /// <summary>Upload url issued; the file is not confirmed yet.</summary>
    Pending = 0,
    Queued = 1,
    Processing = 2,
    Indexed = 3,
    Failed = 4,
    Superseded = 5,

    /// <summary>Content is in storage but deliberately not indexed (an agent draft); indexing starts when the draft is added to knowledge.</summary>
    Stored = 6,
}
