import { BaseFilterDto } from '@majlis/core';

/** Mirrors `Majlis.Meetings.Application.Meetings.DTOs` (enums as strings). */
export type MeetingSource = 'Notes' | 'Transcript' | 'Recording';
export const MEETING_SOURCES: readonly MeetingSource[] = ['Notes', 'Transcript', 'Recording'];
export type MeetingStatus = 'Created' | 'Transcribing' | 'Transcribed' | 'Summarizing' | 'MinutesReady' | 'Failed';
export type MinutesStatus = 'Draft' | 'Approved';

/** Recording types the backend accepts (`MeetingsFieldDefinitions.RecordingContentTypes`). */
export const RECORDING_CONTENT_TYPES: Readonly<Record<string, string>> = {
  'audio/mpeg': '.mp3',
  'audio/mp4': '.m4a',
  'audio/x-m4a': '.m4a',
  'audio/wav': '.wav',
  'audio/webm': '.webm',
};

export interface ActionItemDto {
  id: string;
  order: number;
  text: string;
  ownerName: string | null;
  dueDate: string | null;
  approvalRequestId: string | null;
  taskId: string | null;
}

export interface MinutesDto {
  id: string;
  summary: string;
  decisions: string[];
  openQuestions: string[];
  actionItems: ActionItemDto[];
  language: string;
  status: MinutesStatus;
  generatedAt: string;
  editedAt: string | null;
  approvedByDisplayName: string | null;
  approvedAt: string | null;
  actionItemsProposedAt: string | null;
  publishedAt: string | null;
}

export interface MeetingDto {
  id: string;
  workspaceId: string;
  roomId: string;
  title: string;
  heldAt: string;
  language: string;
  attendees: string[];
  source: MeetingSource;
  status: MeetingStatus;
  transcriptText: string | null;
  hasRecording: boolean;
  failureReasonKey: string | null;
  minutes: MinutesDto | null;
  creationTime: string;
  creatorId: string | null;
}

export interface MeetingListDto {
  id: string;
  workspaceId: string;
  roomId: string;
  title: string;
  heldAt: string;
  source: MeetingSource;
  status: MeetingStatus;
  minutesStatus: MinutesStatus | null;
  actionItemCount: number;
  creationTime: string;
}

export interface FilterMeetingDto extends BaseFilterDto {
  workspaceId: string;
  roomId?: string | null;
}

export interface CreateMeetingDto {
  workspaceId: string;
  roomId: string;
  title: string;
  heldAt: string;
  language: 'ar' | 'en';
  attendees: string[];
  source: MeetingSource;
  text?: string | null;
}

export interface BeginRecordingUploadDto {
  meetingId: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
}

export interface RecordingUploadTicketDto {
  meetingId: string;
  uploadUrl: string;
  expiresAt: string;
  headers: Record<string, string>;
}

export interface ActionItemInputDto {
  id?: string | null;
  text: string;
  ownerName?: string | null;
  dueDate?: string | null;
}

export interface UpdateMinutesDto {
  meetingId: string;
  summary: string;
  decisions: string[];
  openQuestions: string[];
  actionItems: ActionItemInputDto[];
}

export interface ProposedActionItemsDto {
  proposed: number;
  approvalRequestIds: string[];
}
