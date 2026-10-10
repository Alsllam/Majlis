using Majlis.Knowledge.Application.Documents.DTOs;
using Majlis.Knowledge.Domain.Entities;

namespace Majlis.Knowledge.Application;

/// <summary>Explicit entity → DTO mapping (no AutoMapper; ADR-0008).</summary>
public static class KnowledgeMappings
{
    public static DocumentVersionDto ToDto(this DocumentVersion v) => new(
        v.Id, v.Number, v.FileName, v.ContentType, v.SizeBytes, v.Status, v.FailureReasonKey, v.ChunkCount, v.PageCount, v.CreationTime, v.IndexedAt);

    public static DocumentDto ToDto(this Document d) => new(
        d.Id,
        d.WorkspaceId,
        d.RoomId,
        d.Title,
        d.Type,
        d.Language,
        d.EffectiveDate,
        d.Tags,
        d.Origin,
        d.CurrentVersionId,
        d.CreationTime,
        d.CreatorId,
        d.Versions.OrderByDescending(v => v.Number).Select(v => v.ToDto()).ToList());
}
