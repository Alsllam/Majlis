import { Injectable, inject } from '@angular/core';
import { RestConfig, RestService } from '@majlis/core';
import {
  InstructResultDto,
  InstructSessionDto,
  OfferHandOffDto,
  ResolveControlRequestDto,
  ResolveHandOffDto,
  SessionDto,
  SessionEpochDto,
  SessionEventDto,
  SessionEventsFilterDto,
  SessionIdDto,
  SessionStateDto,
  StartSessionDto,
  StopTurnDto,
} from '../models/session.model';

/** Mirrors `SessionsAppService` (`[Route("sessions")]`). Commands are HTTP (ADR-0002); driver commands carry `epoch`. */
@Injectable({ providedIn: 'root' })
export class SessionsService {
  private readonly rest = inject(RestService);
  readonly apiName = 'rooms';

  private post<TBody, TResult>(url: string, body: TBody, config?: Partial<RestConfig>) {
    return this.rest.request<TBody, TResult>({ method: 'POST', url: `/sessions/${url}`, body }, { apiName: this.apiName, ...config });
  }

  start = (input: StartSessionDto, config?: Partial<RestConfig>) => this.post<StartSessionDto, SessionStateDto>('start', input, config);
  get = (sessionId: string, config?: Partial<RestConfig>) => this.post<SessionIdDto, SessionDto>('getbyid', { sessionId }, config);
  getEvents = (input: SessionEventsFilterDto, config?: Partial<RestConfig>) =>
    this.post<SessionEventsFilterDto, SessionEventDto[]>('events', input, config);
  instruct = (input: InstructSessionDto, config?: Partial<RestConfig>) => this.post<InstructSessionDto, InstructResultDto>('instruct', input, config);
  stop = (input: StopTurnDto, config?: Partial<RestConfig>) => this.post<StopTurnDto, void>('stop', input, config);
  claimControl = (sessionId: string, config?: Partial<RestConfig>) => this.post<SessionIdDto, SessionStateDto>('claim', { sessionId }, config);
  requestControl = (sessionId: string, config?: Partial<RestConfig>) =>
    this.post<SessionIdDto, SessionStateDto>('request-control', { sessionId }, config);
  resolveControlRequest = (input: ResolveControlRequestDto, config?: Partial<RestConfig>) =>
    this.post<ResolveControlRequestDto, SessionStateDto>('resolve-request', input, config);
  offerHandOff = (input: OfferHandOffDto, config?: Partial<RestConfig>) => this.post<OfferHandOffDto, SessionStateDto>('offer-handoff', input, config);
  resolveHandOff = (input: ResolveHandOffDto, config?: Partial<RestConfig>) =>
    this.post<ResolveHandOffDto, SessionStateDto>('resolve-handoff', input, config);
  takeOver = (sessionId: string, config?: Partial<RestConfig>) => this.post<SessionIdDto, SessionStateDto>('takeover', { sessionId }, config);
  releaseControl = (input: SessionEpochDto, config?: Partial<RestConfig>) => this.post<SessionEpochDto, SessionStateDto>('release', input, config);
  end = (input: SessionEpochDto, config?: Partial<RestConfig>) => this.post<SessionEpochDto, SessionStateDto>('end', input, config);
}
