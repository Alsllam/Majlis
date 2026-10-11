import { DestroyRef, Injectable, inject, signal } from '@angular/core';
import { HubConnection, HubConnectionBuilder, HubConnectionState, IHttpConnectionOptions, LogLevel } from '@microsoft/signalr';
import { AuthService, ConfigService } from '@majlis/core';
import { ConnectionState } from './models';

/** Milliseconds to wait before each reconnect attempt (§5.5), then every 30 s. */
const RECONNECT_DELAYS = [0, 2000, 5000, 10000, 30000];
/** Tickets expire after 30 s and hub connections after 15 min; re-authenticate every 10 min (§7). */
const REAUTH_INTERVAL = 10 * 60 * 1000;

/**
 * One `HubConnection` per tab. Each (re)connect fetches a fresh ticket from the Auth host through the BFF, so no
 * bearer token ever goes in the hub url. State is a signal; `rejoined` callbacks let channels re-join after a reconnect.
 */
@Injectable({ providedIn: 'root' })
export class RealtimeConnectionService {
  private readonly config = inject(ConfigService);
  private readonly auth = inject(AuthService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly stateSignal = signal<ConnectionState>('offline');
  private connection: HubConnection | null = null;
  private starting: Promise<HubConnection> | null = null;
  private reauthTimer: ReturnType<typeof setInterval> | null = null;
  private reconnectTimer: ReturnType<typeof setTimeout> | null = null;
  private attempt = 0;
  private stopped = true;
  private readonly reconnectHandlers = new Set<() => void>();

  readonly state = this.stateSignal.asReadonly();

  constructor() {
    this.destroyRef.onDestroy(() => void this.stop());
  }

  /** Returns the live connection, starting it when needed. Concurrent callers share one start. */
  async connect(): Promise<HubConnection> {
    if (this.connection?.state === HubConnectionState.Connected) {
      return this.connection;
    }
    if (!this.starting) {
      this.stopped = false;
      this.starting = this.start().finally(() => (this.starting = null));
    }
    return this.starting;
  }

  /** Called after every automatic reconnect, so channels can `JoinSession` again and fetch the gap. */
  onReconnected(handler: () => void): () => void {
    this.reconnectHandlers.add(handler);
    return () => this.reconnectHandlers.delete(handler);
  }

  async stop(): Promise<void> {
    this.stopped = true;
    this.clearTimers();
    const connection = this.connection;
    this.connection = null;
    this.stateSignal.set('offline');
    if (connection) {
      await connection.stop().catch(() => undefined);
    }
  }

  private async start(): Promise<HubConnection> {
    this.stateSignal.set(this.attempt === 0 ? 'connecting' : 'reconnecting');
    const ticket = await this.fetchTicket();
    const options: IHttpConnectionOptions = { withCredentials: true };
    const connection = new HubConnectionBuilder()
      .withUrl(`${this.config.settings.realtime.hubUrl}?ticket=${encodeURIComponent(ticket)}`, options)
      .configureLogging(LogLevel.Warning)
      .build();
    connection.onclose(() => this.handleClosed(connection));
    try {
      await connection.start();
    } catch (error) {
      this.scheduleReconnect();
      throw error;
    }
    const reconnected = this.attempt > 0;
    this.attempt = 0;
    this.connection = connection;
    this.stateSignal.set('connected');
    this.startReauth(connection);
    if (reconnected) {
      for (const handler of this.reconnectHandlers) {
        handler();
      }
    }
    return connection;
  }

  private handleClosed(connection: HubConnection): void {
    if (this.connection !== connection) {
      return;
    }
    this.connection = null;
    this.clearTimers();
    if (!this.stopped) {
      this.stateSignal.set('reconnecting');
      this.scheduleReconnect();
    }
  }

  private scheduleReconnect(): void {
    if (this.stopped || this.reconnectTimer) {
      return;
    }
    const delay = RECONNECT_DELAYS[Math.min(this.attempt, RECONNECT_DELAYS.length - 1)] ?? 30000;
    this.attempt++;
    this.reconnectTimer = setTimeout(() => {
      this.reconnectTimer = null;
      void this.connect().catch(() => undefined);
    }, delay);
  }

  private startReauth(connection: HubConnection): void {
    this.reauthTimer = setInterval(async () => {
      try {
        await connection.invoke('Reauthenticate', await this.fetchTicket());
      } catch {
        // The hub aborts the connection on a bad ticket; onclose then reconnects.
      }
    }, REAUTH_INTERVAL);
  }

  private clearTimers(): void {
    if (this.reauthTimer) {
      clearInterval(this.reauthTimer);
      this.reauthTimer = null;
    }
    if (this.reconnectTimer) {
      clearTimeout(this.reconnectTimer);
      this.reconnectTimer = null;
    }
  }

  private async fetchTicket(): Promise<string> {
    const token = this.auth.accessToken;
    if (!token) {
      throw new Error('not_authenticated');
    }
    const response = await fetch(this.config.settings.realtime.ticketUrl, {
      method: 'POST',
      headers: { Authorization: `Bearer ${token}` },
    });
    if (!response.ok) {
      throw new Error(`ticket_failed_${response.status}`);
    }
    const body = (await response.json()) as { ticket: string };
    return body.ticket;
  }
}
