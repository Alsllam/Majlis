using Majlis.Framework.Application.Dtos;
using Majlis.Workspaces.Domain.Enums;

namespace Majlis.Workspaces.Application.Workspaces.DTOs;

public sealed record WorkspaceMemberDto(Guid UserId, string DisplayName, WorkspaceRole Role, DateTime JoinedAt);

public sealed record WorkspaceDto(
    Guid Id,
    string Name,
    string? Description,
    string? Icon,
    string? Color,
    string? AgentInstructions,
    bool IsArchived,
    WorkspaceRole MyRole,
    IReadOnlyList<WorkspaceMemberDto> Members);

public sealed record WorkspaceListDto(
    Guid Id,
    string Name,
    string? Description,
    string? Icon,
    string? Color,
    bool IsArchived,
    WorkspaceRole MyRole,
    int MemberCount,
    DateTime CreationTime);

public sealed record WorkspaceIdDto(Guid Id);

public sealed record CreateWorkspaceDto
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? Icon { get; init; }
    public string? Color { get; init; }
}

public sealed record UpdateWorkspaceDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? Icon { get; init; }
    public string? Color { get; init; }
}

public sealed record UpdateAgentInstructionsDto
{
    public Guid Id { get; init; }
    public string? AgentInstructions { get; init; }
}

public sealed record FilterWorkspaceDto : BaseFilterRequestDto
{
    /// <summary>Include archived workspaces (default: active only).</summary>
    public bool IncludeArchived { get; init; }
}

/// <summary>Adds a member or changes an existing member's role. <c>DisplayName</c> is the name snapshot (from the Identity user lookup).</summary>
public sealed record SetWorkspaceMemberDto
{
    public Guid WorkspaceId { get; init; }
    public Guid UserId { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public WorkspaceRole Role { get; init; } = WorkspaceRole.Contributor;
}

public sealed record RemoveWorkspaceMemberDto
{
    public Guid WorkspaceId { get; init; }
    public Guid UserId { get; init; }
}

/// <summary>Internal: may this user act in this workspace? Asked by other hosts when their read model is not enough.</summary>
public sealed record WorkspaceAccessQueryDto
{
    public Guid WorkspaceId { get; init; }
    public Guid UserId { get; init; }
}

public sealed record WorkspaceAccessDto(bool Allowed, Guid? TenantId, WorkspaceRole? Role, bool IsArchived);
