namespace Majlis.Workspaces.Domain.Enums;

/// <summary>Workspace-level roles (SRS §3.2). Each maps to a fixed permission set in <c>WorkspaceRolePermissions</c>.</summary>
public enum WorkspaceRole
{
    /// <summary>Manages members and roles, settings, archive; approves actions.</summary>
    Owner = 0,

    /// <summary>Manages members, rooms and knowledge; approves actions.</summary>
    Admin = 1,

    /// <summary>Creates rooms, drives sessions, uploads documents.</summary>
    Contributor = 2,

    /// <summary>Read-only: watches sessions and reads knowledge.</summary>
    Viewer = 3,
}
