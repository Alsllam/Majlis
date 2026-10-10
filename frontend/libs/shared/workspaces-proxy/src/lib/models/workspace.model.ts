import { BaseFilterDto } from '@majlis/core';

/** Mirrors `Majlis.Workspaces.Application.Workspaces.DTOs` (enums are serialized as strings). */
export type WorkspaceRole = 'Owner' | 'Admin' | 'Contributor' | 'Viewer';
export const WORKSPACE_ROLES: readonly WorkspaceRole[] = ['Owner', 'Admin', 'Contributor', 'Viewer'];

/** Brand token names a workspace may use as its color (`WorkspacesFieldDefinitions.Colors`). */
export type WorkspaceColor = 'brand' | 'accent' | 'success' | 'warning' | 'danger' | 'info' | 'neutral';
export const WORKSPACE_COLORS: readonly WorkspaceColor[] = ['brand', 'accent', 'success', 'warning', 'danger', 'info', 'neutral'];

export interface WorkspaceMemberDto {
  userId: string;
  displayName: string;
  role: WorkspaceRole;
  joinedAt: string;
}

export interface WorkspaceDto {
  id: string;
  name: string;
  description: string | null;
  icon: string | null;
  color: WorkspaceColor | null;
  agentInstructions: string | null;
  isArchived: boolean;
  myRole: WorkspaceRole;
  members: WorkspaceMemberDto[];
}

export interface WorkspaceListDto {
  id: string;
  name: string;
  description: string | null;
  icon: string | null;
  color: WorkspaceColor | null;
  isArchived: boolean;
  myRole: WorkspaceRole;
  memberCount: number;
  creationTime: string;
}

export interface WorkspaceIdDto {
  id: string;
}

export interface CreateWorkspaceDto {
  name: string;
  description?: string | null;
  icon?: string | null;
  color?: WorkspaceColor | null;
}

export interface UpdateWorkspaceDto extends CreateWorkspaceDto {
  id: string;
}

export interface UpdateAgentInstructionsDto {
  id: string;
  agentInstructions: string | null;
}

export interface FilterWorkspaceDto extends BaseFilterDto {
  includeArchived?: boolean;
}

export interface SetWorkspaceMemberDto {
  workspaceId: string;
  userId: string;
  displayName: string;
  role: WorkspaceRole;
}

export interface RemoveWorkspaceMemberDto {
  workspaceId: string;
  userId: string;
}

/** Roles that may manage members and settings (`WorkspaceRolePermissions.CanManage`). */
export function canManageWorkspace(role: WorkspaceRole | null | undefined): boolean {
  return role === 'Owner' || role === 'Admin';
}
