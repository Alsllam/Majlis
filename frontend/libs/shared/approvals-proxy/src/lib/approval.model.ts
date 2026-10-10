import { BaseFilterDto } from '@majlis/core';

/** Mirrors `Majlis.Approvals.Application.Requests.DTOs` (enums as strings). */
export type RiskLevel = 'Low' | 'Medium' | 'High';
export type ApprovalStatus = 'Pending' | 'Approved' | 'Rejected' | 'Expired' | 'Executed' | 'Failed';

export interface ApprovalActorDto {
  userId: string;
  displayName: string;
}

export interface ApprovalRequestDto {
  id: string;
  workspaceId: string;
  roomId: string;
  sessionId: string;
  turnId: string | null;
  tool: string;
  args: Record<string, unknown>;
  editedArgs: Record<string, unknown> | null;
  summary: string;
  reason: string | null;
  risk: RiskLevel;
  status: ApprovalStatus;
  requestedBy: ApprovalActorDto;
  expiresAt: string;
  decidedBy: ApprovalActorDto | null;
  decidedAt: string | null;
  decisionNote: string | null;
  resultSummary: string | null;
  resultEntityId: string | null;
  failureReasonKey: string | null;
  creationTime: string;
  /** Whether the caller may approve or reject it now (default policy, FR-APR-005). */
  canDecide: boolean;
}

export interface FilterApprovalRequestDto extends BaseFilterDto {
  workspaceId?: string | null;
  sessionId?: string | null;
  status?: ApprovalStatus | null;
  /** Only requests the caller may decide (the approver inbox). */
  forMe?: boolean;
}

export interface ApproveRequestDto {
  id: string;
  /** Edit then approve (FR-APR-003). */
  editedArgs?: Record<string, unknown> | null;
  note?: string | null;
}

export interface RejectRequestDto {
  id: string;
  reason: string;
}
