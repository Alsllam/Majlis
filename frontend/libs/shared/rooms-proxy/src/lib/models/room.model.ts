import { BaseFilterDto } from '@majlis/core';

/** Mirrors `Majlis.Rooms.Application.Rooms.DTOs` (enums are serialized as strings). */
export type RoomVisibility = 'Private' | 'Open';
export type ParticipantRole = 'Contributor' | 'Observer';

export interface RoomParticipantDto {
  userId: string;
  displayName: string;
  role: ParticipantRole;
}

export interface RoomDto {
  id: string;
  workspaceId: string;
  name: string;
  purpose: string | null;
  visibility: RoomVisibility;
  isArchived: boolean;
  activeSessionId: string | null;
  participants: RoomParticipantDto[];
}

export interface RoomListDto {
  id: string;
  workspaceId: string;
  name: string;
  purpose: string | null;
  activeSessionId: string | null;
  participantCount: number;
  creationTime: string;
}

export interface RoomIdDto {
  id: string;
}

export interface CreateRoomDto {
  workspaceId: string;
  name: string;
  purpose?: string | null;
  visibility: RoomVisibility;
}

export interface AddRoomParticipantDto {
  roomId: string;
  userId: string;
  displayName: string;
  role: ParticipantRole;
}

export interface FilterRoomDto extends BaseFilterDto {
  workspaceId?: string;
}
