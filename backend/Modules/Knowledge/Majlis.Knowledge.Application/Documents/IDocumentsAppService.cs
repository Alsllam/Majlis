using Majlis.Framework.Application.Dtos;
using Majlis.Knowledge.Application.Documents.DTOs;

namespace Majlis.Knowledge.Application.Documents;

/// <summary>Documents of a workspace: upload in two steps, status, download, delete (SRS §4.4).</summary>
public interface IDocumentsAppService
{
    /// <summary>Documents the caller may read in a workspace (room-only ones only when <c>RoomId</c> is given), newest first.</summary>
    Task<PagedResultDto<DocumentListDto>> GetListAsync(FilterDocumentDto input, CancellationToken cancellationToken = default);

    /// <summary>One document with its versions.</summary>
    Task<DocumentDto> GetAsync(DocumentIdDto input, CancellationToken cancellationToken = default);

    /// <summary>Creates the document (or a new version) and returns a pre-signed upload url.</summary>
    Task<UploadTicketDto> BeginUploadAsync(BeginUploadDto input, CancellationToken cancellationToken = default);

    /// <summary>Checks the file reached storage, then publishes <c>DocumentUploaded</c> for ingestion.</summary>
    Task<DocumentDto> ConfirmUploadAsync(ConfirmUploadDto input, CancellationToken cancellationToken = default);

    /// <summary>A short-lived download url for one version.</summary>
    Task<DownloadUrlDto> GetDownloadUrlAsync(VersionIdDto input, CancellationToken cancellationToken = default);

    /// <summary>Title, type, effective date, tags.</summary>
    Task UpdateAsync(UpdateDocumentDto input, CancellationToken cancellationToken = default);

    /// <summary>Soft-deletes the document and asks ai-service to drop its chunks (FR-KNW-007).</summary>
    Task DeleteAsync(DocumentIdDto input, CancellationToken cancellationToken = default);
}
