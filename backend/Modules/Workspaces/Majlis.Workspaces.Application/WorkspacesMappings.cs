using Majlis.Workspaces.Application.Workspaces.DTOs;
using Majlis.Workspaces.Domain.Entities;
using Majlis.Workspaces.Domain.Enums;

namespace Majlis.Workspaces.Application;

/// <summary>Explicit entity → DTO mapping (no AutoMapper; ADR-0008).</summary>
public static class WorkspacesMappings
{
    public static WorkspaceDto ToDto(this Workspace workspace, WorkspaceRole myRole) => new(
        workspace.Id,
        workspace.Name,
        workspace.Description,
        workspace.Icon,
        workspace.Color,
        workspace.AgentInstructions,
        workspace.IsArchived,
        myRole,
        workspace.Members
            .OrderBy(m => m.Role)
            .ThenBy(m => m.DisplayName)
            .Select(m => new WorkspaceMemberDto(m.UserId, m.DisplayName, m.Role, m.CreationTime, m.ViaGroupId, m.ViaGroupId == null ? null : workspace.Groups.FirstOrDefault(g => g.GroupId == m.ViaGroupId)?.GroupName))
            .ToList(),
        workspace.Groups
            .OrderBy(g => g.GroupName)
            .Select(g => new WorkspaceGroupDto(g.GroupId, g.GroupName, g.Role, workspace.Members.Count(m => m.ViaGroupId == g.GroupId)))
            .ToList());
}
