using System.Globalization;
using System.Text;
using System.Text.Json;
using Majlis.Audit.Application.Entries.DTOs;
using Majlis.Audit.Application.Verification;
using Majlis.Audit.Domain.Constants;
using Majlis.Audit.Domain.Entities;
using Majlis.Framework.Application.Dtos;
using Majlis.Framework.Application.Security;
using Majlis.Framework.Application.Services;
using Majlis.Framework.Domain.Exceptions;
using Majlis.Framework.Domain.Repositories;
using Majlis.Framework.Domain.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Majlis.Audit.Application.Entries;

[Route("entries")]
public class AuditAppService(
    IReadOnlyRepository<AuditEntry, Guid> entries,
    ChainVerifier verifier,
    ICurrentUser currentUser,
    TimeProvider clock,
    IOptions<AuditOptions> options) : ApplicationService, IAuditAppService
{
    /// <inheritdoc />
    [HttpPost("list")]
    [HasPermission(AuditPermissions.ViewAudit)]
    public async Task<PagedResultDto<AuditEntryDto>> GetListAsync(FilterAuditDto input, CancellationToken cancellationToken = default)
    {
        var query = Filter(input);
        var total = await query.CountAsync(cancellationToken);
        var page = await query.OrderByDescending(e => e.Seq).Skip(input.SkipCount).Take(Math.Clamp(input.MaxResultCount, 1, 200)).ToListAsync(cancellationToken);
        return new PagedResultDto<AuditEntryDto>(page.Select(ToDto).ToList(), total);
    }

    /// <inheritdoc />
    [HttpPost("verify")]
    [HasPermission(AuditPermissions.ViewAudit)]
    public async Task<ChainVerificationDto> VerifyAsync(CancellationToken cancellationToken = default)
    {
        var result = await verifier.VerifyAsync(currentUser.GetRequiredTenantId(), record: true, cancellationToken);
        return new ChainVerificationDto(result.Count, result.Valid, result.BrokenSeq, result.LastHash, result.VerifiedAt);
    }

    /// <inheritdoc />
    [HttpPost("export")]
    [HasPermission(AuditPermissions.ExportAudit)]
    public async Task<FileContentResult> ExportAsync(FilterAuditDto input, CancellationToken cancellationToken = default)
    {
        var query = Filter(input);
        if (await query.CountAsync(cancellationToken) > options.Value.ExportMaxRows)
        {
            throw new CustomValidationException(AuditErrors.ExportTooLarge, options.Value.ExportMaxRows);
        }

        var rows = await query.OrderByDescending(e => e.Seq).Take(options.Value.ExportMaxRows).ToListAsync(cancellationToken);
        var sb = new StringBuilder();
        sb.AppendLine("seq,occurredAt,category,action,actorKind,actorUserId,actorDisplayName,workspaceId,roomId,sessionId,entityType,entityId,details,hash");
        foreach (var e in rows)
        {
            sb.AppendLine(string.Join(',', new[]
            {
                e.Seq.ToString(CultureInfo.InvariantCulture), e.OccurredAt.ToString("O", CultureInfo.InvariantCulture), e.Category, e.Action, e.ActorKind,
                e.ActorUserId?.ToString("D") ?? "", e.ActorDisplayName ?? "", e.WorkspaceId?.ToString("D") ?? "", e.RoomId?.ToString("D") ?? "",
                e.SessionId?.ToString("D") ?? "", e.EntityType ?? "", e.EntityId?.ToString("D") ?? "", e.DetailsJson, e.Hash,
            }.Select(Csv)));
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return new FileContentResult(bytes, "text/csv; charset=utf-8") { FileDownloadName = $"majlis-audit-{clock.GetUtcNow():yyyyMMdd-HHmm}.csv" };
    }

    private IQueryable<AuditEntry> Filter(FilterAuditDto input)
    {
        var tenantId = currentUser.GetRequiredTenantId();
        var text = input.FilterText?.Trim();
        return entries.Query().Where(e => e.TenantId == tenantId
            && (input.WorkspaceId == null || e.WorkspaceId == input.WorkspaceId)
            && (string.IsNullOrEmpty(input.Category) || e.Category == input.Category)
            && (input.ActorUserId == null || e.ActorUserId == input.ActorUserId)
            && (input.From == null || e.OccurredAt >= input.From)
            && (input.To == null || e.OccurredAt <= input.To)
            && (string.IsNullOrEmpty(text) || e.Action.Contains(text) || (e.ActorDisplayName != null && e.ActorDisplayName.Contains(text)) || e.DetailsJson.Contains(text)));
    }

    private static string Csv(string value)
        => value.Contains(',') || value.Contains('"') || value.Contains('\n') ? $"\"{value.Replace("\"", "\"\"")}\"" : value;

    private static AuditEntryDto ToDto(AuditEntry e)
    {
        using var details = JsonDocument.Parse(e.DetailsJson);
        return new AuditEntryDto(e.Id, e.Seq, e.OccurredAt, e.Category, e.Action, e.ActorKind, e.ActorUserId, e.ActorDisplayName, e.WorkspaceId, e.RoomId, e.SessionId, e.EntityType, e.EntityId, details.RootElement.Clone(), e.Hash);
    }
}
