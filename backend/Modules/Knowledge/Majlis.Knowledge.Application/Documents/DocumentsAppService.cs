using FluentValidation;
using Majlis.Framework.Application.Dtos;
using Majlis.Framework.Application.Security;
using Majlis.Framework.Application.Services;
using Majlis.Framework.Application.Storage;
using Majlis.Framework.Domain.Events;
using Majlis.Framework.Domain.Exceptions;
using Majlis.Framework.Domain.Repositories;
using Majlis.Framework.Domain.Security;
using Majlis.Knowledge.Application.Documents.DTOs;
using Majlis.Knowledge.Domain.Constants;
using Majlis.Knowledge.Domain.Entities;
using Majlis.Knowledge.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Majlis.Knowledge.Application.Documents;

[Route("documents")]
public class DocumentsAppService(
    IRepository<Document, Guid> documents,
    IReadOnlyRepository<WorkspaceMembership, Guid> memberships,
    IBlobStorage blobs,
    IUnitOfWork unitOfWork,
    IEventPublisher publisher,
    ICurrentUser currentUser,
    TimeProvider clock,
    IOptions<KnowledgeOptions> options,
    IValidator<BeginUploadDto> beginValidator,
    IValidator<UpdateDocumentDto> updateValidator) : ApplicationService, IDocumentsAppService
{
    private KnowledgeOptions Options => options.Value;

    /// <inheritdoc />
    [HttpPost("list")]
    [HasPermission(KnowledgePermissions.ViewDocument)]
    public async Task<PagedResultDto<DocumentListDto>> GetListAsync(FilterDocumentDto input, CancellationToken cancellationToken = default)
    {
        await RequireMemberAsync(input.WorkspaceId, cancellationToken);
        var query = documents.Query()
            .Where(d => d.WorkspaceId == input.WorkspaceId
                && (input.RoomId == null ? d.RoomId == null : d.RoomId == input.RoomId)
                && (string.IsNullOrEmpty(input.FilterText) || d.Title.Contains(input.FilterText)));

        var total = await query.CountAsync(cancellationToken);
        var page = await query
            .OrderByDescending(d => d.CreationTime)
            .Skip(input.SkipCount)
            .Take(Math.Clamp(input.MaxResultCount, 1, 100))
            .Select(d => new
            {
                d.Id, d.WorkspaceId, d.RoomId, d.Title, d.Type, d.Language, d.CreationTime, d.CreatorId,
                Latest = d.Versions.OrderByDescending(v => v.Number).Select(v => new { v.Status, v.FailureReasonKey, v.FileName, v.SizeBytes, v.PageCount }).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        var rows = page
            .Select(d => new DocumentListDto(
                d.Id, d.WorkspaceId, d.RoomId, d.Title, d.Type, d.Language,
                d.Latest?.Status ?? IngestionStatus.Pending, d.Latest?.FailureReasonKey,
                d.Latest?.FileName ?? string.Empty, d.Latest?.SizeBytes ?? 0, d.Latest?.PageCount ?? 0, d.CreationTime, d.CreatorId))
            .Where(r => input.Status == null || r.Status == input.Status)
            .ToList();
        return new PagedResultDto<DocumentListDto>(rows, total);
    }

    /// <inheritdoc />
    [HttpPost("getbyid")]
    [HasPermission(KnowledgePermissions.ViewDocument)]
    public async Task<DocumentDto> GetAsync(DocumentIdDto input, CancellationToken cancellationToken = default)
    {
        var document = await LoadAsync(input.Id, tracked: false, cancellationToken);
        await RequireMemberAsync(document.WorkspaceId, cancellationToken);
        return document.ToDto();
    }

    /// <inheritdoc />
    [HttpPost("begin-upload")]
    [HasPermission(KnowledgePermissions.UploadDocument)]
    public async Task<UploadTicketDto> BeginUploadAsync(BeginUploadDto input, CancellationToken cancellationToken = default)
    {
        await ValidateAsync(beginValidator, input, cancellationToken);
        var member = await RequireMemberAsync(input.WorkspaceId, cancellationToken);
        if (!member.CanContribute)
        {
            throw new ForbiddenException(KnowledgeErrors.NotWorkspaceMember);
        }

        if (input.SizeBytes > Options.MaxFileSizeMb * 1024L * 1024L)
        {
            throw new CustomValidationException(KnowledgeErrors.FileTooLarge, Options.MaxFileSizeMb);
        }

        var tenantId = currentUser.GetRequiredTenantId();
        Document document;
        if (input.DocumentId is { } existingId)
        {
            document = await LoadAsync(existingId, tracked: true, cancellationToken);
            if (document.WorkspaceId != input.WorkspaceId)
            {
                throw new EntityNotFoundException();
            }
        }
        else
        {
            var title = string.IsNullOrWhiteSpace(input.Title) ? Path.GetFileNameWithoutExtension(input.FileName) : input.Title.Trim();
            document = new Document(Guid.NewGuid(), tenantId, input.WorkspaceId, input.RoomId, title, input.Type, input.Language, DocumentOrigin.Upload);
            await documents.InsertAsync(document, autoSave: false, cancellationToken);
        }

        var safeName = Path.GetFileName(input.FileName);
        var versionId = Guid.NewGuid();
        var blobPath = $"{tenantId:D}/{document.WorkspaceId:D}/{document.Id:D}/{versionId:D}/{safeName}";
        var version = document.AddVersion(versionId, safeName, input.ContentType, input.SizeBytes, blobPath);
        await documents.UpdateAsync(document, autoSave: true, cancellationToken);

        var url = await blobs.CreateUploadUrlAsync(KnowledgeFieldDefinitions.Container, blobPath, input.ContentType, TimeSpan.FromMinutes(Options.UploadUrlMinutes), cancellationToken);
        var headers = new Dictionary<string, string> { ["x-ms-blob-type"] = "BlockBlob", ["Content-Type"] = input.ContentType };
        return new UploadTicketDto(document.Id, version.Id, url.Url, url.ExpiresAt, headers);
    }

    /// <inheritdoc />
    [HttpPost("confirm")]
    [HasPermission(KnowledgePermissions.UploadDocument)]
    public async Task<DocumentDto> ConfirmUploadAsync(ConfirmUploadDto input, CancellationToken cancellationToken = default)
    {
        var document = await documents.QueryTracked().Include(d => d.Versions)
            .FirstOrDefaultAsync(d => d.Versions.Any(v => v.Id == input.VersionId), cancellationToken)
            ?? throw new EntityNotFoundException();
        await RequireMemberAsync(document.WorkspaceId, cancellationToken);
        var version = document.GetVersion(input.VersionId);

        var stored = await blobs.GetPropertiesAsync(KnowledgeFieldDefinitions.Container, version.BlobPath, cancellationToken)
            ?? throw new CustomValidationException(KnowledgeErrors.FileNotUploaded);
        version.Confirm(stored.SizeBytes, clock.GetUtcNow().UtcDateTime);
        await documents.UpdateAsync(document, autoSave: false, cancellationToken);
        await publisher.PublishAsync(
            new DocumentUploaded(
                document.TenantId, document.WorkspaceId, document.RoomId, document.Id, version.Id, version.BlobPath, version.FileName,
                version.ContentType, document.Title, document.Type.ToString(), document.Language, document.AclGroups),
            cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return document.ToDto();
    }

    /// <inheritdoc />
    [HttpPost("download-url")]
    [HasPermission(KnowledgePermissions.ViewDocument)]
    public async Task<DownloadUrlDto> GetDownloadUrlAsync(VersionIdDto input, CancellationToken cancellationToken = default)
    {
        var document = await documents.Query().Include(d => d.Versions)
            .FirstOrDefaultAsync(d => d.Versions.Any(v => v.Id == input.VersionId), cancellationToken)
            ?? throw new EntityNotFoundException();
        await RequireMemberAsync(document.WorkspaceId, cancellationToken);
        var version = document.GetVersion(input.VersionId);
        var url = await blobs.CreateDownloadUrlAsync(KnowledgeFieldDefinitions.Container, version.BlobPath, version.FileName, TimeSpan.FromMinutes(Options.DownloadUrlMinutes), cancellationToken);
        return new DownloadUrlDto(url.Url, url.ExpiresAt, version.FileName);
    }

    /// <inheritdoc />
    [HttpPut("")]
    [HasPermission(KnowledgePermissions.UploadDocument)]
    public async Task UpdateAsync(UpdateDocumentDto input, CancellationToken cancellationToken = default)
    {
        await ValidateAsync(updateValidator, input, cancellationToken);
        var document = await LoadAsync(input.Id, tracked: true, cancellationToken);
        var member = await RequireMemberAsync(document.WorkspaceId, cancellationToken);
        if (!member.CanManage && document.CreatorId != currentUser.GetRequiredId())
        {
            throw new ForbiddenException();
        }

        document.UpdateMetadata(input.Title.Trim(), input.Type, input.EffectiveDate, string.IsNullOrWhiteSpace(input.Tags) ? null : input.Tags.Trim());
        await documents.UpdateAsync(document, autoSave: true, cancellationToken);
    }

    /// <inheritdoc />
    [HttpDelete("")]
    [HasPermission(KnowledgePermissions.DeleteDocument)]
    public async Task DeleteAsync(DocumentIdDto input, CancellationToken cancellationToken = default)
    {
        var document = await LoadAsync(input.Id, tracked: true, cancellationToken);
        var member = await RequireMemberAsync(document.WorkspaceId, cancellationToken);
        if (!member.CanManage && document.CreatorId != currentUser.GetRequiredId())
        {
            throw new ForbiddenException();
        }

        await documents.DeleteAsync(document, autoSave: false, cancellationToken);
        await publisher.PublishAsync(new DocumentDeleted(document.TenantId, document.Id), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<Document> LoadAsync(Guid id, bool tracked, CancellationToken cancellationToken)
    {
        var query = tracked ? documents.QueryTracked() : documents.Query();
        return await query.Include(d => d.Versions).FirstOrDefaultAsync(d => d.Id == id, cancellationToken)
            ?? throw new EntityNotFoundException();
    }

    private async Task<WorkspaceMembership> RequireMemberAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        var userId = currentUser.GetRequiredId();
        return await memberships.Query().FirstOrDefaultAsync(m => m.WorkspaceId == workspaceId && m.UserId == userId, cancellationToken)
            ?? throw new ForbiddenException(KnowledgeErrors.NotWorkspaceMember);
    }
}
