import { BaseFilterDto } from '@majlis/core';

/** Mirrors `Majlis.Tasks.Application.Tasks.DTOs` (enums as strings). */
export type TaskStatus = 'ToDo' | 'InProgress' | 'Done' | 'Cancelled';
export const TASK_STATUSES: readonly TaskStatus[] = ['ToDo', 'InProgress', 'Done', 'Cancelled'];
export type TaskPriority = 'Low' | 'Normal' | 'High' | 'Urgent';
export const TASK_PRIORITIES: readonly TaskPriority[] = ['Low', 'Normal', 'High', 'Urgent'];
export type TaskOrigin = 'Manual' | 'Agent';

export interface TaskDto {
  id: string;
  workspaceId: string;
  roomId: string | null;
  sessionId: string | null;
  title: string;
  description: string | null;
  assigneeUserId: string | null;
  assigneeDisplayName: string | null;
  dueDate: string | null;
  priority: TaskPriority;
  status: TaskStatus;
  origin: TaskOrigin;
  approvalRequestId: string | null;
  creationTime: string;
  creatorId: string | null;
}

export interface FilterTaskDto extends BaseFilterDto {
  workspaceId?: string | null;
  roomId?: string | null;
  status?: TaskStatus | null;
  assignedToMe?: boolean;
}

export interface CreateTaskDto {
  workspaceId: string;
  roomId?: string | null;
  title: string;
  description?: string | null;
  assigneeUserId?: string | null;
  dueDate?: string | null;
  priority: TaskPriority;
}

export interface UpdateTaskDto {
  id: string;
  title: string;
  description?: string | null;
  assigneeUserId?: string | null;
  dueDate?: string | null;
  priority: TaskPriority;
}

export interface SetTaskStatusDto {
  id: string;
  status: TaskStatus;
}
