import { HttpErrorResponse } from '@angular/common/http';
import { DestroyRef, Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { APPROVALS_PERMISSIONS } from '@majlis/approvals-config';
import { ApprovalsService } from '@majlis/approvals-proxy';
import { AuthService, LocalizationService, PermissionService, isApiError } from '@majlis/core';
import { ROOMS_PERMISSIONS } from '@majlis/rooms-config';
import {
  ApprovalDecidedData,
  ApprovalExecutedData,
  ApprovalRequestedData,
  CitationDto,
  ControlChangedData,
  RoomDto,
  RoomsService,
  SessionEventDto,
  SessionStateDto,
  SessionsService,
  TurnCompletedData,
  TurnFailedData,
  TurnStartedData,
  TurnStoppedData,
} from '@majlis/rooms-proxy';
import { ConnectionState, PresenceParticipant, RealtimeConnectionService, SessionChannel, StreamMessage } from '@majlis/shared-realtime';
import { ToastService } from '@majlis/theme-shared';

export type TurnStatus = 'streaming' | 'completed' | 'stopped' | 'failed';

export interface TurnItem {
  kind: 'turn';
  seq: number;
  turnId: string;
  instruction: string;
  instructedBy: { userId: string; displayName: string };
  language: 'ar' | 'en';
  at: string;
  status: TurnStatus;
  text: string;
  chunk: number;
  citations: CitationDto[];
  reasonKey: string | null;
  progressKey: string | null;
}

export interface NoticeItem {
  kind: 'notice';
  seq: number;
  at: string;
  /** Translation key + params; rendered in the current language. */
  key: string;
  params: Record<string, string>;
}

export type ApprovalState = 'pending' | 'approved' | 'rejected' | 'expired' | 'executed' | 'failed';

/** One approval card: the request plus every later outcome, merged by `requestId` (FR-APR-004). */
export interface ApprovalItem {
  kind: 'approval';
  seq: number;
  requestId: string;
  turnId: string | null;
  tool: string;
  summary: string;
  reason: string | null;
  risk: 'Low' | 'Medium' | 'High';
  args: Record<string, unknown>;
  requestedBy: { userId: string; displayName: string };
  at: string;
  expiresAt: string;
  state: ApprovalState;
  decidedBy: { userId: string; displayName: string } | null;
  note: string | null;
  resultSummary: string | null;
  resultEntityId: string | null;
  reasonKey: string | null;
}

export type TimelineItem = TurnItem | NoticeItem | ApprovalItem;

/** Known `turn.failed` reasons (the backend sends localization keys such as `General:Errors:AiBusy`). */
const KNOWN_REASONS = new Set(['AiUnavailable', 'AiBusy', 'ModelError', 'Cancelled']);

export function reasonOf(reasonKey: string | null | undefined): string {
  const last = (reasonKey ?? '').split(':').pop() ?? '';
  return KNOWN_REASONS.has(last) ? last : 'Unknown';
}

const KNOWN_KINDS = new Set(['start', 'claim', 'request-accepted', 'handoff', 'takeover', 'release', 'timeout']);

const CONTROL_EVENTS = new Set([
  'control.changed',
  'control.requested',
  'control.request.resolved',
  'handoff.offered',
  'handoff.resolved',
  'session.ended',
]);

/**
 * Room-screen state on signals (`docs/architecture/realtime-collaboration.md` §10): the timeline built from durable
 * events, the streaming turn text merged from the stream lane, control state with the fencing epoch, and presence.
 * Provided per room component so two open rooms never share state.
 */
@Injectable()
export class RoomSessionFacade {
  private readonly rooms = inject(RoomsService);
  private readonly sessions = inject(SessionsService);
  private readonly approvals = inject(ApprovalsService);
  private readonly realtime = inject(RealtimeConnectionService);
  private readonly auth = inject(AuthService);
  private readonly permissions = inject(PermissionService);
  private readonly toasts = inject(ToastService);
  private readonly localization = inject(LocalizationService);
  private readonly destroyRef = inject(DestroyRef);
  private channel: SessionChannel<SessionEventDto> | null = null;
  private readonly items = signal<Map<number, TimelineItem>>(new Map());

  readonly room = signal<RoomDto | null>(null);
  readonly state = signal<SessionStateDto | null>(null);
  readonly presence = signal<PresenceParticipant[]>([]);
  readonly connection = computed<ConnectionState>(() => this.realtime.state());
  readonly busy = signal(false);
  readonly loading = signal(true);

  readonly timeline = computed<TimelineItem[]>(() => [...this.items().values()].sort((a, b) => a.seq - b.seq));
  readonly me = computed(() => this.auth.user());
  readonly isDriver = computed(() => !!this.state()?.driver && this.state()?.driver?.userId === this.me()?.id);
  readonly controlFree = computed(() => this.state()?.status === 'Active' && !this.state()?.driver);
  readonly activeTurn = computed<TurnItem | null>(() => {
    const id = this.state()?.activeTurnId;
    if (!id) {
      return null;
    }
    const turn = this.timeline().find((t): t is TurnItem => t.kind === 'turn' && t.turnId === id);
    return turn && turn.status === 'streaming' ? turn : null;
  });
  readonly canDrive = computed(() => this.permissions.isGranted(ROOMS_PERMISSIONS.driveSession));
  readonly canTakeOver = computed(() => this.permissions.isGranted(ROOMS_PERMISSIONS.takeOverSession));
  readonly canApprove = computed(() => this.permissions.isGranted(APPROVALS_PERMISSIONS.approveAction));
  /** Cards still waiting for a person; the backend decides who may act (FR-APR-005), the UI shows the buttons to approvers. */
  readonly pendingApprovals = computed(() => this.timeline().filter((i): i is ApprovalItem => i.kind === 'approval' && i.state === 'pending'));
  readonly canInstruct = computed(() => this.isDriver() && this.state()?.status === 'Active' && !this.activeTurn());
  readonly myRequestPending = computed(() => this.state()?.pendingRequests.some((r) => r.userId === this.me()?.id) ?? false);
  readonly handOffToMe = computed(() => this.state()?.pendingHandOffTo?.userId === this.me()?.id);
  readonly colleagues = computed(() => (this.room()?.participants ?? []).filter((p) => p.userId !== this.me()?.id && p.role === 'Contributor'));

  constructor() {
    this.destroyRef.onDestroy(() => void this.leave());
  }

  async open(roomId: string): Promise<void> {
    this.loading.set(true);
    try {
      const room = await firstValueFrom(this.rooms.get(roomId));
      this.room.set(room);
      if (room.activeSessionId) {
        await this.attach(room.activeSessionId);
      }
    } finally {
      this.loading.set(false);
    }
  }

  async startSession(): Promise<void> {
    const room = this.room();
    if (!room) {
      return;
    }
    await this.command(async () => {
      const state = await firstValueFrom(this.sessions.start({ roomId: room.id }));
      this.room.update((r) => (r ? { ...r, activeSessionId: state.id } : r));
      await this.attach(state.id);
    });
  }

  async instruct(text: string): Promise<void> {
    const state = this.state();
    const trimmed = text.trim();
    if (!state || !trimmed) {
      return;
    }
    await this.command(() =>
      firstValueFrom(
        this.sessions.instruct({
          sessionId: state.id,
          text: trimmed,
          epoch: state.controlEpoch,
          clientRequestId: crypto.randomUUID(),
          language: /[؀-ۿ]/.test(trimmed) ? 'ar' : 'en',
        }),
      ),
    );
  }

  async stop(): Promise<void> {
    const state = this.state();
    const turn = this.activeTurn();
    if (!state || !turn) {
      return;
    }
    await this.command(() => firstValueFrom(this.sessions.stop({ sessionId: state.id, turnId: turn.turnId, epoch: state.controlEpoch })));
  }

  claim = () => this.transition((id) => this.sessions.claimControl(id));
  requestControl = () => this.transition((id) => this.sessions.requestControl(id));
  takeOver = () => this.transition((id) => this.sessions.takeOver(id));
  release = () => this.transition((id, epoch) => this.sessions.releaseControl({ sessionId: id, epoch }));
  end = () => this.transition((id, epoch) => this.sessions.end({ sessionId: id, epoch }));
  resolveRequest = (requestId: string, accept: boolean) =>
    this.transition((id, epoch) => this.sessions.resolveControlRequest({ sessionId: id, requestId, epoch, accept }));
  offerHandOff = (toUserId: string, note: string | null) =>
    this.transition((id, epoch) => this.sessions.offerHandOff({ sessionId: id, toUserId, epoch, note }));
  resolveHandOff = (accept: boolean) => this.transition((id) => this.sessions.resolveHandOff({ sessionId: id, accept }));

  /** Decisions are plain HTTP commands; the card changes when `approval.decided` comes back through the timeline. */
  approve = (requestId: string, note: string | null = null) => this.command(() => firstValueFrom(this.approvals.approve({ id: requestId, note })));
  reject = (requestId: string, reason: string) => this.command(() => firstValueFrom(this.approvals.reject({ id: requestId, reason })));

  private async transition(call: (sessionId: string, epoch: number) => import('rxjs').Observable<SessionStateDto>): Promise<void> {
    const state = this.state();
    if (!state) {
      return;
    }
    await this.command(async () => this.state.set(await firstValueFrom(call(state.id, state.controlEpoch))));
  }

  /** Runs a command; a 409 (stale epoch, turn in progress) reloads the state instead of failing silently. */
  private async command(run: () => Promise<unknown>): Promise<void> {
    if (this.busy()) {
      return;
    }
    this.busy.set(true);
    try {
      await run();
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.status === 409) {
        await this.refreshState();
        if (!isApiError(error.error)) {
          this.toasts.show(this.localization.instant('Control.ChangedWarning'), 'warning');
        }
      }
    } finally {
      this.busy.set(false);
    }
  }

  private async attach(sessionId: string): Promise<void> {
    await this.leave();
    const session = await firstValueFrom(this.sessions.get(sessionId));
    this.state.set(session.state);
    this.items.set(new Map());
    for (const event of session.events) {
      this.apply(event);
    }
    const channel = new SessionChannel<SessionEventDto>(this.realtime, {
      sessionId,
      lastSeq: session.state.lastSeq,
      activeTurnId: session.state.activeTurnId,
      fetchAfter: (afterSeq) => firstValueFrom(this.sessions.getEvents({ sessionId, afterSeq, maxResultCount: 200 })),
    });
    this.channel = channel;
    channel.events$.subscribe((event) => {
      this.apply(event);
      if (CONTROL_EVENTS.has(event.type) || event.type.startsWith('turn.')) {
        void this.refreshState();
      }
    });
    channel.stream$.subscribe((message) => this.applyStream(message));
    channel.snapshot$.subscribe((snapshot) =>
      this.patchTurn(snapshot.turnId, (t) => (snapshot.chunk >= t.chunk ? { ...t, text: snapshot.text, chunk: snapshot.chunk } : t)),
    );
    channel.presence$.subscribe((snapshot) => this.presence.set(snapshot.participants));
    try {
      await channel.open();
    } catch {
      // Offline: the connection service keeps retrying; the timeline is already loaded over HTTP.
    }
  }

  private async leave(): Promise<void> {
    const channel = this.channel;
    this.channel = null;
    if (channel) {
      await channel.close();
    }
  }

  private async refreshState(): Promise<void> {
    const state = this.state();
    if (!state) {
      return;
    }
    try {
      const session = await firstValueFrom(this.sessions.get(state.id));
      this.state.set(session.state);
    } catch {
      // Reported by the global handler.
    }
  }

  private apply(event: SessionEventDto): void {
    switch (event.type) {
      case 'turn.started': {
        const data = event.data as unknown as TurnStartedData;
        this.put({
          kind: 'turn',
          seq: event.seq,
          turnId: event.turnId ?? '',
          instruction: data.instruction,
          instructedBy: data.instructedBy,
          language: data.language,
          at: event.at,
          status: 'streaming',
          text: '',
          chunk: 0,
          citations: [],
          reasonKey: null,
          progressKey: null,
        });
        return;
      }
      case 'turn.completed': {
        const data = event.data as unknown as TurnCompletedData;
        this.patchTurn(event.turnId, (t) => ({ ...t, status: 'completed', text: data.text, citations: data.citations ?? [], progressKey: null }));
        return;
      }
      case 'turn.stopped': {
        const data = event.data as unknown as TurnStoppedData;
        this.patchTurn(event.turnId, (t) => ({ ...t, status: 'stopped', text: data.partialText || t.text, progressKey: null }));
        return;
      }
      case 'turn.failed': {
        const data = event.data as unknown as TurnFailedData;
        this.patchTurn(event.turnId, (t) => ({ ...t, status: 'failed', text: data.partialText ?? t.text, reasonKey: reasonOf(data.reasonKey), progressKey: null }));
        return;
      }
      case 'control.changed': {
        const data = event.data as unknown as ControlChangedData;
        this.put({
          kind: 'notice',
          seq: event.seq,
          at: event.at,
          key: `Control.Changed.${KNOWN_KINDS.has(data.kind) ? data.kind : 'other'}`,
          params: { from: data.from?.displayName ?? '', to: data.to?.displayName ?? '' },
        });
        return;
      }
      case 'control.requested':
        this.put({ kind: 'notice', seq: event.seq, at: event.at, key: 'Control.Requested_', params: { name: event.actor.displayName ?? '' } });
        return;
      case 'control.request.resolved':
        this.put({ kind: 'notice', seq: event.seq, at: event.at, key: 'Control.RequestResolved', params: {} });
        return;
      case 'handoff.resolved':
        this.put({ kind: 'notice', seq: event.seq, at: event.at, key: 'Control.HandOffResolved', params: {} });
        return;
      case 'session.ended':
        this.put({ kind: 'notice', seq: event.seq, at: event.at, key: 'Session.Ended', params: {} });
        return;
      case 'approval.requested': {
        const data = event.data as unknown as ApprovalRequestedData;
        this.put({
          kind: 'approval',
          seq: event.seq,
          requestId: data.requestId,
          turnId: data.turnId,
          tool: data.tool,
          summary: data.summary,
          reason: data.reason,
          risk: data.risk,
          args: data.args ?? {},
          requestedBy: data.requestedBy,
          at: event.at,
          expiresAt: data.expiresAt,
          state: 'pending',
          decidedBy: null,
          note: null,
          resultSummary: null,
          resultEntityId: null,
          reasonKey: null,
        });
        return;
      }
      case 'approval.decided': {
        const data = event.data as unknown as ApprovalDecidedData;
        this.patchApproval(data.requestId, (a) => ({ ...a, state: data.decision === 'Approved' ? 'approved' : 'rejected', decidedBy: data.decidedBy, note: data.note }));
        return;
      }
      case 'approval.expired': {
        const data = event.data as unknown as ApprovalDecidedData;
        this.patchApproval(data.requestId, (a) => ({ ...a, state: 'expired' }));
        return;
      }
      case 'approval.executed': {
        const data = event.data as unknown as ApprovalExecutedData;
        this.patchApproval(data.requestId, (a) => ({
          ...a,
          state: data.succeeded ? 'executed' : 'failed',
          resultSummary: data.resultSummary,
          resultEntityId: data.entityId,
          reasonKey: data.reasonKey,
        }));
        return;
      }
      default:
        return;
    }
  }

  private applyStream(message: StreamMessage): void {
    this.patchTurn(message.turnId, (t) => {
      if (message.chunk <= t.chunk || t.status !== 'streaming') {
        return t; // already in the snapshot, or the turn ended
      }
      if (message.type === 'turn.delta') {
        return { ...t, text: t.text + (message.data.text ?? ''), chunk: message.chunk };
      }
      return { ...t, progressKey: message.data.key ?? null, chunk: message.chunk };
    });
  }

  private put(item: TimelineItem): void {
    this.items.update((map) => new Map(map).set(item.seq, item));
  }

  private patchApproval(requestId: string, patch: (item: ApprovalItem) => ApprovalItem): void {
    this.items.update((map) => {
      for (const [seq, item] of map) {
        if (item.kind === 'approval' && item.requestId === requestId) {
          const next = new Map(map);
          next.set(seq, patch(item));
          return next;
        }
      }
      return map;
    });
  }

  private patchTurn(turnId: string | null, patch: (turn: TurnItem) => TurnItem): void {
    if (!turnId) {
      return;
    }
    this.items.update((map) => {
      for (const [seq, item] of map) {
        if (item.kind === 'turn' && item.turnId === turnId) {
          const next = new Map(map);
          next.set(seq, patch(item));
          return next;
        }
      }
      return map;
    });
  }
}
