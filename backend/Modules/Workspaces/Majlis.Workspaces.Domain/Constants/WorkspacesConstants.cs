using Majlis.Workspaces.Domain.Enums;

namespace Majlis.Workspaces.Domain.Constants;

/// <summary>Permission names; identical strings are used by the web and mobile apps.</summary>
public static class WorkspacesPermissions
{
    public const string ViewWorkspace = "Permissions.Workspaces.ViewWorkspace";
    public const string CreateWorkspace = "Permissions.Workspaces.CreateWorkspace";
    public const string UpdateWorkspace = "Permissions.Workspaces.UpdateWorkspace";
    public const string ArchiveWorkspace = "Permissions.Workspaces.ArchiveWorkspace";
    public const string ManageMembers = "Permissions.Workspaces.ManageMembers";
}

/// <summary>
/// Effective permissions a workspace role grants, in this and other modules. Workspaces writes the union over a
/// user's memberships to the permission cache (<c>majlis:permissions:{userId}</c>) that every host's
/// <c>PermissionChecker</c> reads; the owning app service still checks the role for the specific workspace.
/// </summary>
public static class WorkspaceRolePermissions
{
    private static readonly string[] OwnerAndAdmin =
    [
        WorkspacesPermissions.ViewWorkspace,
        WorkspacesPermissions.UpdateWorkspace,
        WorkspacesPermissions.ManageMembers,
        "Permissions.Rooms.ViewRoom",
        "Permissions.Rooms.CreateRoom",
        "Permissions.Rooms.ManageParticipants",
        "Permissions.Rooms.DriveSession",
        "Permissions.Rooms.TakeOverSession",
        "Permissions.Knowledge.ViewDocument",
        "Permissions.Knowledge.UploadDocument",
        "Permissions.Knowledge.DeleteDocument",
        "Permissions.Approvals.ViewApproval",
        "Permissions.Approvals.RequestAction",
        "Permissions.Approvals.ApproveAction",
        "Permissions.Tasks.ViewTask",
        "Permissions.Tasks.CreateTask",
        "Permissions.Tasks.UpdateTask",
    ];

    public static IReadOnlyList<string> For(WorkspaceRole role) => role switch
    {
        WorkspaceRole.Owner => [.. OwnerAndAdmin, WorkspacesPermissions.ArchiveWorkspace],
        WorkspaceRole.Admin => OwnerAndAdmin,
        WorkspaceRole.Contributor =>
        [
            WorkspacesPermissions.ViewWorkspace,
            "Permissions.Rooms.ViewRoom",
            "Permissions.Rooms.CreateRoom",
            "Permissions.Rooms.DriveSession",
            "Permissions.Knowledge.ViewDocument",
            "Permissions.Knowledge.UploadDocument",
            "Permissions.Approvals.ViewApproval",
            "Permissions.Approvals.RequestAction",
            "Permissions.Approvals.ApproveAction",
            "Permissions.Tasks.ViewTask",
            "Permissions.Tasks.CreateTask",
            "Permissions.Tasks.UpdateTask",
        ],
        WorkspaceRole.Viewer =>
        [
            WorkspacesPermissions.ViewWorkspace,
            "Permissions.Rooms.ViewRoom",
            "Permissions.Knowledge.ViewDocument",
            "Permissions.Approvals.ViewApproval",
            "Permissions.Tasks.ViewTask",
        ],
        _ => [],
    };

    /// <summary>Roles that may manage members and settings.</summary>
    public static bool CanManage(WorkspaceRole role) => role is WorkspaceRole.Owner or WorkspaceRole.Admin;
}

/// <summary>Localization keys for business errors in this module.</summary>
public static class WorkspacesErrors
{
    public const string NotMember = "Workspaces:Workspace:NotMember";
    public const string NotManager = "Workspaces:Workspace:NotManager";
    public const string Archived = "Workspaces:Workspace:Archived";
    public const string LastOwner = "Workspaces:Workspace:LastOwner";
    public const string MemberNotFound = "Workspaces:Workspace:MemberNotFound";
    public const string OnlyOwnerChangesOwner = "Workspaces:Workspace:OnlyOwnerChangesOwner";
    public const string ColorInvalid = "Workspaces:Workspace:ColorInvalid";
}

public static class WorkspacesFieldDefinitions
{
    public const int MaxNameLength = Framework.Domain.Localization.FieldDefinitions.MaxNameLength;
    public const int MaxDescriptionLength = Framework.Domain.Localization.FieldDefinitions.MaxDescriptionLength;
    public const int MaxInstructionsLength = Framework.Domain.Localization.FieldDefinitions.MaxMessageLength;
    public const int MaxDisplayNameLength = Framework.Domain.Localization.FieldDefinitions.MaxNameLength;
    public const int MaxIconLength = 32;
    public const int MaxColorLength = 16;

    /// <summary>Brand token names allowed as a workspace color (never raw hex, so every theme renders it).</summary>
    public static readonly string[] Colors = ["brand", "accent", "success", "warning", "danger", "info", "neutral"];
}
