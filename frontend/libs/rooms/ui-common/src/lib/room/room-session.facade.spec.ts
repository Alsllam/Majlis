import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { ApprovalsService } from '@majlis/approvals-proxy';
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
        { provide: ApprovalsService, useValue: { approve: jest.fn(() => of({})), reject: jest.fn(() => of({})) } },
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

  it('merges approval events into one card by requestId and approves through the Approvals api', async () => {
    const state = { id: 's', roomId: 'r', status: 'Active', driver: { userId: 'u1', displayName: 'سارة' }, controlEpoch: 1, pendingHandOffTo: null, pendingHandOffNote: null, pendingRequests: [], activeTurnId: null, lastSeq: 2 };
    const requested = { requestId: 'a1', tool: 'create_task', summary: 'إنشاء مهمة: مراجعة العقد', reason: null, risk: 'Low', args: { title: 'مراجعة العقد' }, requestedBy: { userId: 'u1', displayName: 'سارة' }, expiresAt: '2026-10-11T12:00:00Z', turnId: 't1' };
    const approvals = { approve: jest.fn(() => of({})), reject: jest.fn(() => of({})) };
    TestBed.configureTestingModule({
      providers: [
        RoomSessionFacade,
        { provide: RoomsService, useValue: { get: () => of({ id: 'r', name: 'غرفة', purpose: null, activeSessionId: 's', participants: [], visibility: 'Private', isArchived: false, workspaceId: 'w' }) } },
        { provide: SessionsService, useValue: { get: () => of({ state, events: [event(1, 'approval.requested', 't1', requested)], hasOlderEvents: false }), getEvents: () => of([]) } },
        { provide: ApprovalsService, useValue: approvals },
        { provide: RealtimeConnectionService, useValue: { state: () => 'offline', connect: () => Promise.reject(new Error('offline')), onReconnected: () => () => undefined } },
        { provide: AuthService, useValue: { user: () => ({ id: 'u2', name: 'خالد', roles: ['Member'], tenantId: 't', locale: 'ar' }) } },
        { provide: PermissionService, useValue: { isGranted: () => true } },
        { provide: ToastService, useValue: { show: jest.fn() } },
        { provide: LocalizationService, useValue: { instant: (k: string) => k } },
      ],
    });
    const facade = TestBed.inject(RoomSessionFacade);
    await facade.open('r');

    expect(facade.pendingApprovals().map((a) => a.requestId)).toEqual(['a1']);
    expect(facade.canApprove()).toBe(true);

    await facade.approve('a1');
    expect(approvals.approve).toHaveBeenCalledWith({ id: 'a1', note: null });
    await facade.reject('a1', 'ليست أولوية');
    expect(approvals.reject).toHaveBeenCalledWith({ id: 'a1', reason: 'ليست أولوية' });

    const apply = (facade as unknown as { apply: (e: SessionEventDto) => void }).apply.bind(facade);
    apply(event(2, 'approval.decided', null, { requestId: 'a1', tool: 'create_task', decision: 'Approved', decidedBy: { userId: 'u2', displayName: 'خالد' }, note: null, edited: false }));
    apply(event(3, 'approval.executed', null, { requestId: 'a1', tool: 'create_task', succeeded: true, resultSummary: 'مراجعة العقد', entityId: 'task-1', reasonKey: null }));
    apply(event(4, 'approval.decided', null, { requestId: 'other', tool: 'create_task', decision: 'Rejected', decidedBy: null, note: null, edited: false }));

    expect(facade.pendingApprovals()).toEqual([]);
    const card = facade.timeline()[0];
    expect(card.kind === 'approval' && [card.state, card.decidedBy?.displayName, card.resultEntityId]).toEqual(['executed', 'خالد', 'task-1']);
    expect(facade.timeline()).toHaveLength(1);
  });
});
