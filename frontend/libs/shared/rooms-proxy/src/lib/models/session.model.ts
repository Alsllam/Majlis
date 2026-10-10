/** Mirrors `Majlis.Rooms.Application.Sessions.DTOs` and the envelope in `docs/architecture/events.schema.json`. */
export type SessionStatus = 'Active' | 'Ended';

export interface SessionActorDto {
  userId: string;
  displayName: string;
}

export interface ControlRequestDto {
  id: string;
  userId: string;
  displayName: string;
  requestedAt: string;
}

export interface SessionStateDto {
  id: string;
  roomId: string;
  status: SessionStatus;
  driver: SessionActorDto | null;
  controlEpoch: number;
  pendingHandOffTo: SessionActorDto | null;
  pendingHandOffNote: string | null;
  pendingRequests: ControlRequestDto[];
  activeTurnId: string | null;
  lastSeq: number;
}

export type SessionEventType =
  | 'session.started'
  | 'session.ended'
  | 'control.changed'
  | 'control.requested'
  | 'control.request.resolved'
  | 'handoff.offered'
  | 'handoff.resolved'
  | 'turn.started'
  | 'turn.completed'
  | 'turn.stopped'
  | 'turn.failed'
  | 'approval.requested'
  | 'approval.decided'
  | 'approval.executed'
  | 'approval.expired';

export interface SessionEventScopeDto {
  tenantId: string;
  workspaceId: string;
  roomId: string;
  sessionId: string;
}

export interface SessionEventActorDto {
  kind: 'user' | 'agent' | 'system';
  id: string | null;
  displayName: string | null;
}

export interface SessionEventDto<TData = Record<string, unknown>> {
  v: number;
  type: SessionEventType;
  scope: SessionEventScopeDto;
  seq: number;
  turnId: string | null;
  at: string;
  actor: SessionEventActorDto;
  data: TData;
}

export interface SessionDto {
  state: SessionStateDto;
  events: SessionEventDto[];
  hasOlderEvents: boolean;
}

export interface SessionIdDto {
  sessionId: string;
}

export interface StartSessionDto {
  roomId: string;
}

export interface SessionEventsFilterDto {
  sessionId: string;
  afterSeq?: number;
  beforeSeq?: number;
  maxResultCount?: number;
}

export interface SessionEpochDto {
  sessionId: string;
  epoch: number;
}

export interface InstructSessionDto {
  sessionId: string;
  text: string;
  epoch: number;
  clientRequestId: string;
  language?: 'ar' | 'en';
}

export interface InstructResultDto {
  turnId: string;
  seq: number;
}

export interface StopTurnDto {
  sessionId: string;
  turnId: string;
  epoch: number;
}

export interface ResolveControlRequestDto {
  sessionId: string;
  requestId: string;
  epoch: number;
  accept: boolean;
}

export interface OfferHandOffDto {
  sessionId: string;
  toUserId: string;
  epoch: number;
  note?: string | null;
}

export interface ResolveHandOffDto {
  sessionId: string;
  accept: boolean;
}

/** `data` of the events the room screen renders. */
export interface TurnStartedData {
  instruction: string;
  instructedBy: SessionActorDto;
  language: 'ar' | 'en';
}

/** `CitationContract` in the backend: `[S#]` label → document, version, page and the cited passage. */
export interface CitationDto {
  label: string;
  documentId: string;
  versionId: string;
  title: string;
  page: number | null;
  passage: string;
}

export interface TurnCompletedData {
  text: string;
  citations: CitationDto[];
  inputTokens?: number;
  outputTokens?: number;
}

export interface TurnStoppedData {
  partialText: string;
}

export interface TurnFailedData {
  reasonKey: string;
  partialText: string | null;
}

export interface ControlChangedData {
  from: SessionActorDto | null;
  to: SessionActorDto | null;
  kind: 'start' | 'claim' | 'request-accepted' | 'handoff' | 'takeover' | 'release' | 'timeout';
  epoch: number;
  note?: string | null;
}

/** Approval events written by Rooms from the Approvals module (FR-APR-004/007); `requestId` ties them together. */
export interface ApprovalRequestedData {
  requestId: string;
  tool: string;
  summary: string;
  reason: string | null;
  risk: 'Low' | 'Medium' | 'High';
  args: Record<string, unknown>;
  requestedBy: SessionActorDto;
  expiresAt: string;
  turnId: string | null;
}

export interface ApprovalDecidedData {
  requestId: string;
  tool: string;
  decision: 'Approved' | 'Rejected' | 'Expired';
  decidedBy: SessionActorDto | null;
  note: string | null;
  edited: boolean;
}

export interface ApprovalExecutedData {
  requestId: string;
  tool: string;
  succeeded: boolean;
  resultSummary: string | null;
  entityId: string | null;
  reasonKey: string | null;
}
