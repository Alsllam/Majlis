import { BaseFilterDto } from '@majlis/core';

/** Mirrors `Majlis.Notifications.Application.Notifications.DTOs` and the hub `notification` message. */
export type NotificationType = 'approval.requested' | 'approval.decided' | 'approval.executed' | 'task.assigned' | 'handoff.offered';
export const NOTIFICATION_TYPES: readonly NotificationType[] = ['approval.requested', 'approval.decided', 'approval.executed', 'task.assigned', 'handoff.offered'];

export interface NotificationDto {
  id: string;
  type: NotificationType | string;
  params: Record<string, string>;
  link: string | null;
  createdAt: string;
  readAt: string | null;
}

export interface FilterNotificationDto extends BaseFilterDto {
  unreadOnly?: boolean;
}

export interface UnreadCountDto {
  count: number;
}

export interface NotificationSettingsDto {
  emailEnabled: boolean;
  mutedTypes: string[];
}
