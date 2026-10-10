import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { AuthService, LocalizationService, PermissionService } from '@majlis/core';
import { RoomsService, SessionEventDto, SessionsService } from '@majlis/rooms-proxy';
import { RealtimeConnectionService } from '@majlis/shared-realtime';
import { ToastService } from '@majlis/theme-shared';
import { RoomSessionFacade } from './room-session.facade';

const scope = { tenantId: 't', workspaceId: 'w', roomId: 'r', sessionId: 's' };
const event = (seq: number, type: SessionEventDto['type'], turnId: string | null, data: Record<string, unknown>): SessionEventDto => ({
  v: 1, type, scope, seq, turnId, at: '2026-10-10T12:00:00Z', actor: { kind: 'user', id: 'u1', displayName: 'سارة' }, data,
});

describe('RoomSessionFacade', () => {
  it('builds the timeline from durable events and merges stream deltas into the running turn', async () => {
    const state = { id: 's', roomId: 'r', status: 'Active', driver: { userId: 'u1', displayName: 'سارة' }, controlEpoch: 1, pendingHandOffTo: null, pendingHandOffNote: null, pendingRequests: [], activeTurnId: 't1', lastSeq: 2 };
    const events = [
      event(1, 'control.changed', null, { from: null, to: { userId: 'u1', displayName: 'سارة' }, kind: 'Start', epoch: 1 }),
      event(2, 'turn.started', 't1', { instruction: 'لخص العقد', instructedBy: { userId: 'u1', displayName: 'سارة' }, language: 'ar' }),
    ];
    TestBed.configureTestingModule({
      providers: [
        RoomSessionFacade,
        { provide: RoomsService, useValue: { get: () => of({ id: 'r', name: 'غرفة', purpose: null, activeSessionId: 's', participants: [], visibility: 'Private', isArchived: false, workspaceId: 'w' }) } },
        { provide: SessionsService, useValue: { get: () => of({ state, events, hasOlderEvents: false }), getEvents: () => of([]) } },
        { provide: RealtimeConnectionService, useValue: { state: () => 'offline', connect: () => Promise.reject(new Error('offline')), onReconnected: () => () => undefined } },
        { provide: AuthService, useValue: { user: () => ({ id: 'u1', name: 'سارة', roles: ['Member'], tenantId: 't', locale: 'ar' }) } },
        { provide: PermissionService, useValue: { isGranted: () => true } },
        { provide: ToastService, useValue: { show: jest.fn() } },
        { provide: LocalizationService, useValue: { instant: (k: string) => k } },
      ],
    });
    const facade = TestBed.inject(RoomSessionFacade);
    await facade.open('r');

    expect(facade.timeline().map((i) => i.kind)).toEqual(['notice', 'turn']);
    expect(facade.isDriver()).toBe(true);
    expect(facade.activeTurn()?.turnId).toBe('t1');
    expect(facade.canInstruct()).toBe(false); // a turn is running

    (facade as unknown as { applyStream: (m: unknown) => void }).applyStream({ type: 'turn.delta', turnId: 't1', chunk: 1, data: { text: 'ملخص' } });
    (facade as unknown as { applyStream: (m: unknown) => void }).applyStream({ type: 'turn.delta', turnId: 't1', chunk: 1, data: { text: 'x' } }); // duplicate chunk
    (facade as unknown as { applyStream: (m: unknown) => void }).applyStream({ type: 'turn.delta', turnId: 't1', chunk: 2, data: { text: ' العقد' } });
    expect(facade.activeTurn()?.text).toBe('ملخص العقد');

    (facade as unknown as { apply: (e: SessionEventDto) => void }).apply(event(3, 'turn.completed', 't1', { text: 'ملخص العقد.', citations: [] }));
    expect(facade.activeTurn()).toBeNull();
    const turn = facade.timeline()[1];
    expect(turn.kind === 'turn' && turn.status).toBe('completed');
  });
});
