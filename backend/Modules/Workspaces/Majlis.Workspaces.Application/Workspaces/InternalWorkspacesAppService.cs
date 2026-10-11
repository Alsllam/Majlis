using Majlis.Framework.Application.Security;
using Majlis.Framework.Application.Services;
using Majlis.Framework.Domain.Repositories;
using Majlis.Workspaces.Application.Workspaces.DTOs;
using Majlis.Workspaces.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Majlis.Workspaces.Application.Workspaces;

/// <summary>Service-to-service endpoints (client-credentials tokens with the internal scope only).</summary>
[Route("internal/workspaces")]
[Authorize(Policy = InternalServicePolicy.Name)]
public class InternalWorkspacesAppService(IReadOnlyRepository<Workspace, Guid> workspaces) : ApplicationService
{
    /// <summary>Membership and role of a user in a workspace, for hosts without a membership read model.</summary>
    [HttpPost("access")]
    public async Task<WorkspaceAccessDto> GetAccessAsync(WorkspaceAccessQueryDto input, CancellationToken cancellationToken = default)
    {
        var row = await workspaces.Query()
            .Where(w => w.Id == input.WorkspaceId)
            .Select(w => new { w.TenantId, w.IsArchived, Role = w.Members.Where(m => m.UserId == input.UserId).Select(m => (Domain.Enums.WorkspaceRole?)m.Role).FirstOrDefault() })
            .FirstOrDefaultAsync(cancellationToken);

        return row is null
            ? new WorkspaceAccessDto(false, null, null, false)
            : new WorkspaceAccessDto(row.Role is not null && !row.IsArchived, row.TenantId, row.Role, row.IsArchived);
    }
}
