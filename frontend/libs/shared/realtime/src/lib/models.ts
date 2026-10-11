/** Hub contract (`SessionHub` + `docs/architecture/events.schema.json`). The durable envelope type lives in the proxy. */
export type ConnectionState = 'offline' | 'connecting' | 'connected' | 'reconnecting';

export interface StreamMessage {
  type: 'turn.delta' | 'turn.progress';
  turnId: string;
  chunk: number;
  data: { text?: string; key?: string; params?: Record<string, unknown> };
}

export type PresenceState = 'active' | 'idle' | 'typing:comment' | 'typing:suggestion';

export interface PresenceParticipant {
  userId: string;
  displayName: string;
  state: PresenceState;
  connections: number;
}

export interface PresenceSnapshot {
  sessionId: string;
  participants: PresenceParticipant[];
}

export interface StreamSnapshot {
  turnId: string;
  text: string;
  chunk: number;
}

export interface JoinSessionResult {
  presence: PresenceSnapshot;
  stream: StreamSnapshot | null;
}

/** The minimum the channel needs from a durable event to order it. */
export interface SequencedEvent {
  seq: number;
  type: string;
  turnId: string | null;
}
