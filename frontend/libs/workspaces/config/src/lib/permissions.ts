/** Identical to `WorkspacesPermissions` and `IdentityPermissions` in the backend. */
export const WORKSPACES_PERMISSIONS = {
  viewWorkspace: 'Permissions.Workspaces.ViewWorkspace',
  createWorkspace: 'Permissions.Workspaces.CreateWorkspace',
  updateWorkspace: 'Permissions.Workspaces.UpdateWorkspace',
  archiveWorkspace: 'Permissions.Workspaces.ArchiveWorkspace',
  manageMembers: 'Permissions.Workspaces.ManageMembers',
  viewUsers: 'Permissions.Identity.ViewUsers',
} as const;
