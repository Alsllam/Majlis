import { BaseFilterDto } from '@majlis/core';

/** Mirrors `Majlis.Audit.Application.Entries.DTOs`. */
export type AuditCategory = 'session' | 'approval' | 'tool' | 'document' | 'membership' | 'task';
export const AUDIT_CATEGORIES: readonly AuditCategory[] = ['session', 'approval', 'tool', 'document', 'membership', 'task'];

export interface AuditEntryDto {
  id: string;
  seq: number;
  occurredAt: string;
  category: AuditCategory | string;
  action: string;
  actorKind: 'user' | 'agent' | 'system' | string;
  actorUserId: string | null;
  actorDisplayName: string | null;
  workspaceId: string | null;
  roomId: string | null;
  sessionId: string | null;
  entityType: string | null;
  entityId: string | null;
  details: Record<string, unknown>;
  hash: string;
}

export interface FilterAuditDto extends BaseFilterDto {
  workspaceId?: string | null;
  category?: string | null;
  actorUserId?: string | null;
  from?: string | null;
  to?: string | null;
}

export interface ChainVerificationDto {
  entries: number;
  valid: boolean;
  brokenAtSeq: number | null;
  headHash: string;
  verifiedAt: string;
}
