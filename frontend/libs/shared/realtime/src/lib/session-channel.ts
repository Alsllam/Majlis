import { HubConnection } from '@microsoft/signalr';
import { Observable, Subject } from 'rxjs';
import { JoinSessionResult, PresenceSnapshot, PresenceState, SequencedEvent, StreamMessage, StreamSnapshot } from './models';
import { RealtimeConnectionService } from './realtime-connection.service';

/** How long a seq gap may stay open before the missing events are fetched over HTTP (§5.5). */
export const GAP_WAIT_MS = 500;
const HEARTBEAT_MS = 15000;
const HIDDEN_IDLE_MS = 2 * 60 * 1000;

export interface SessionChannelOptions<TEvent extends SequencedEvent> {
  sessionId: string;
  /** The last seq the caller has already applied (from `sessions/getbyid`). */
  lastSeq: number;
  activeTurnId: string | null;
  /** `POST /sessions/events { afterSeq }`: closes the open-vs-join race and fills gaps. */
  fetchAfter: (afterSeq: number) => Promise<TEvent[]>;
  /** Optional: hides the join behind a document visibility check in tests. */
  document?: Document;
}

/**
 * One session subscription: joins the hub group, applies durable events strictly in `seq` order through a reorder
 * buffer (gap → wait 500 ms → fetch), relays stream-lane deltas, sends presence heartbeats and re-joins after a
 * reconnect. Dropped duplicates never reach the consumer.
 */
export class SessionChannel<TEvent extends SequencedEvent> {
  private readonly events = new Subject<TEvent>();
  private readonly stream = new Subject<StreamMessage>();
  private readonly presence = new Subject<PresenceSnapshot>();
  private readonly snapshot = new Subject<StreamSnapshot>();
  private readonly buffer = new Map<number, TEvent>();
  private lastSeq: number;
  private gapTimer: ReturnType<typeof setTimeout> | null = null;
  private heartbeatTimer: ReturnType<typeof setInterval> | null = null;
  private hiddenSince: number | null = null;
  private presenceState: PresenceState = 'active';
  private connection: HubConnection | null = null;
  private unsubscribeReconnect: (() => void) | null = null;
  private closed = false;
  private fetching = false;
  private readonly handlers = {
    sessionEvent: (e: TEvent) => this.accept(e),
    streamEvent: (m: StreamMessage) => this.stream.next(m),
    presence: (p: PresenceSnapshot) => this.presence.next(p),
  };

  readonly events$: Observable<TEvent> = this.events.asObservable();
  readonly stream$: Observable<StreamMessage> = this.stream.asObservable();
  readonly presence$: Observable<PresenceSnapshot> = this.presence.asObservable();
  /** Text so far of a turn that was already streaming when we joined. */
  readonly snapshot$: Observable<StreamSnapshot> = this.snapshot.asObservable();

  constructor(
    private readonly realtime: RealtimeConnectionService,
    private readonly options: SessionChannelOptions<TEvent>,
  ) {
    this.lastSeq = options.lastSeq;
  }

  get appliedSeq(): number {
    return this.lastSeq;
  }

  async open(): Promise<void> {
    this.unsubscribeReconnect = this.realtime.onReconnected(() => void this.join());
    await this.join();
    this.heartbeatTimer = setInterval(() => void this.heartbeat(), HEARTBEAT_MS);
    const doc = this.options.document ?? document;
    doc.addEventListener('visibilitychange', this.onVisibility);
  }

  /** Throttled by the caller (one signal per 3 s while typing). */
  setPresence(state: PresenceState): void {
    this.presenceState = state;
    void this.heartbeat();
  }

  async close(): Promise<void> {
    this.closed = true;
    this.unsubscribeReconnect?.();
    if (this.heartbeatTimer) {
      clearInterval(this.heartbeatTimer);
    }
    if (this.gapTimer) {
      clearTimeout(this.gapTimer);
    }
    (this.options.document ?? document).removeEventListener('visibilitychange', this.onVisibility);
    const connection = this.connection;
    this.connection = null;
    if (connection) {
      this.detach(connection);
      await connection.invoke('LeaveSession', this.options.sessionId).catch(() => undefined);
    }
    this.events.complete();
    this.stream.complete();
    this.presence.complete();
    this.snapshot.complete();
  }

  /** Applies an event received over the hub or fetched over HTTP. Exposed for tests. */
  accept(event: TEvent): void {
    if (this.closed || event.seq <= this.lastSeq) {
      return; // duplicate (hub + gap fetch) or already applied
    }
    this.buffer.set(event.seq, event);
    this.drain();
    if (this.buffer.size > 0 && !this.gapTimer) {
      this.gapTimer = setTimeout(() => {
        this.gapTimer = null;
        void this.fillGap();
      }, GAP_WAIT_MS);
    }
  }

  private drain(): void {
    let next = this.buffer.get(this.lastSeq + 1);
    while (next) {
      this.buffer.delete(next.seq);
      this.lastSeq = next.seq;
      this.events.next(next);
      next = this.buffer.get(this.lastSeq + 1);
    }
    if (this.buffer.size === 0 && this.gapTimer) {
      clearTimeout(this.gapTimer);
      this.gapTimer = null;
    }
  }

  private async fillGap(): Promise<void> {
    if (this.fetching || this.closed) {
      return;
    }
    this.fetching = true;
    try {
      const missing = await this.options.fetchAfter(this.lastSeq);
      for (const event of missing) {
        this.accept(event);
      }
    } catch {
      // Network trouble: the next hub event or reconnect retries the fetch.
    } finally {
      this.fetching = false;
    }
  }

  private async join(): Promise<void> {
    if (this.closed) {
      return;
    }
    const connection = await this.realtime.connect();
    if (this.connection !== connection) {
      if (this.connection) {
        this.detach(this.connection);
      }
      this.connection = connection;
      connection.on('sessionEvent', this.handlers.sessionEvent);
      connection.on('streamEvent', this.handlers.streamEvent);
      connection.on('presence', this.handlers.presence);
    }
    const result = await connection.invoke<JoinSessionResult>('JoinSession', this.options.sessionId, this.options.activeTurnId);
    this.presence.next(result.presence);
    if (result.stream) {
      this.snapshot.next(result.stream);
    }
    await this.fillGap(); // closes the race between getbyid and JoinSession (§5.5 step 3)
  }

  private detach(connection: HubConnection): void {
    connection.off('sessionEvent', this.handlers.sessionEvent);
    connection.off('streamEvent', this.handlers.streamEvent);
    connection.off('presence', this.handlers.presence);
  }

  private async heartbeat(): Promise<void> {
    if (!this.connection || this.closed) {
      return;
    }
    const state: PresenceState =
      this.hiddenSince !== null && Date.now() - this.hiddenSince > HIDDEN_IDLE_MS ? 'idle' : this.presenceState;
    await this.connection.invoke('Heartbeat', this.options.sessionId, state).catch(() => undefined);
  }

  private readonly onVisibility = () => {
    const doc = this.options.document ?? document;
    this.hiddenSince = doc.visibilityState === 'hidden' ? Date.now() : null;
    if (!this.hiddenSince) {
      void this.heartbeat();
    }
  };
}
