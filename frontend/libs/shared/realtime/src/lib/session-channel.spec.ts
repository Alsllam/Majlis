import { SessionChannel, GAP_WAIT_MS } from './session-channel';
import { RealtimeConnectionService } from './realtime-connection.service';
import { SequencedEvent } from './models';

const ev = (seq: number): SequencedEvent => ({ seq, type: 'turn.started', turnId: null });

describe('SessionChannel reorder buffer', () => {
  const realtime = {} as RealtimeConnectionService;

  it('applies events strictly in seq order and drops duplicates', () => {
    const applied: number[] = [];
    const channel = new SessionChannel(realtime, { sessionId: 's', lastSeq: 2, activeTurnId: null, fetchAfter: async () => [] });
    channel.events$.subscribe((e) => applied.push(e.seq));
    channel.accept(ev(4));
    channel.accept(ev(3));
    channel.accept(ev(3));
    channel.accept(ev(2));
    expect(applied).toEqual([3, 4]);
    expect(channel.appliedSeq).toBe(4);
  });

  it('fetches the gap after 500 ms when a seq is missing', async () => {
    jest.useFakeTimers();
    const fetchAfter = jest.fn(async (after: number) => [ev(after + 1)]);
    const applied: number[] = [];
    const channel = new SessionChannel(realtime, { sessionId: 's', lastSeq: 0, activeTurnId: null, fetchAfter });
    channel.events$.subscribe((e) => applied.push(e.seq));
    channel.accept(ev(2));
    expect(applied).toEqual([]);
    await jest.advanceTimersByTimeAsync(GAP_WAIT_MS);
    expect(fetchAfter).toHaveBeenCalledWith(0);
    expect(applied).toEqual([1, 2]);
    jest.useRealTimers();
  });
});
