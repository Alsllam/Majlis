using Majlis.Audit.Application.Entries.DTOs;
using Majlis.Framework.Application.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace Majlis.Audit.Application.Entries;

/// <summary>The tenant's audit log (SRS §4.9), for tenant admins: read, verify the chain, export. Nothing here writes.</summary>
public interface IAuditAppService
{
    Task<PagedResultDto<AuditEntryDto>> GetListAsync(FilterAuditDto input, CancellationToken cancellationToken = default);

    Task<ChainVerificationDto> VerifyAsync(CancellationToken cancellationToken = default);

    /// <summary>CSV (UTF-8 with BOM, so Excel opens Arabic correctly) of the filtered entries, newest first.</summary>
    Task<FileContentResult> ExportAsync(FilterAuditDto input, CancellationToken cancellationToken = default);
}
