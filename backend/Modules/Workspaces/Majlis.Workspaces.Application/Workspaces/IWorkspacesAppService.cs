using Majlis.Framework.Application.Dtos;
using Majlis.Workspaces.Application.Workspaces.DTOs;

namespace Majlis.Workspaces.Application.Workspaces;

/// <summary>Workspaces the caller belongs to, their members and settings (SRS §4.2).</summary>
public interface IWorkspacesAppService
{
    /// <summary>Workspaces the caller is a member of, newest first.</summary>
    Task<PagedResultDto<WorkspaceListDto>> GetListAsync(FilterWorkspaceDto input, CancellationToken cancellationToken = default);

    /// <summary>One workspace with its members and the caller's role.</summary>
    Task<WorkspaceDto> GetAsync(WorkspaceIdDto input, CancellationToken cancellationToken = default);

    /// <summary>Creates a workspace; the creator becomes its first owner. Returns the new id.</summary>
    Task<Guid> CreateAsync(CreateWorkspaceDto input, CancellationToken cancellationToken = default);

    /// <summary>Name, description, icon and color (owner or admin).</summary>
    Task UpdateAsync(UpdateWorkspaceDto input, CancellationToken cancellationToken = default);

    /// <summary>Agent instructions added to every session in the workspace (owner or admin).</summary>
    Task UpdateAgentInstructionsAsync(UpdateAgentInstructionsDto input, CancellationToken cancellationToken = default);

    /// <summary>Archives (read-only) or restores a workspace (owner).</summary>
    Task ArchiveAsync(WorkspaceIdDto input, CancellationToken cancellationToken = default);

    Task RestoreAsync(WorkspaceIdDto input, CancellationToken cancellationToken = default);

    /// <summary>Adds a member or changes a role (owner or admin; owners only by an owner).</summary>
    Task SetMemberAsync(SetWorkspaceMemberDto input, CancellationToken cancellationToken = default);

    /// <summary>Removes a member; the last owner cannot be removed.</summary>
    Task RemoveMemberAsync(RemoveWorkspaceMemberDto input, CancellationToken cancellationToken = default);
}
