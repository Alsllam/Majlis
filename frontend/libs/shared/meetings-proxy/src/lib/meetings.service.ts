import { HttpClient, HttpEvent, HttpHeaders, HttpRequest } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ConfigService, PagedResultDto, RestConfig, RestService } from '@majlis/core';
import {
  BeginRecordingUploadDto,
  CreateMeetingDto,
  FilterMeetingDto,
  MeetingDto,
  MeetingListDto,
  ProposedActionItemsDto,
  RecordingUploadTicketDto,
  UpdateMinutesDto,
} from './meeting.model';

/** Mirrors `MeetingsAppService` (`[Route("meetings")]`, reached as `/api/meetings/meetings/...`). */
@Injectable({ providedIn: 'root' })
export class MeetingsService {
  private readonly rest = inject(RestService);
  private readonly http = inject(HttpClient);
  private readonly config = inject(ConfigService);
  readonly apiName = 'meetings';

  private call<TBody, TResult>(method: 'POST' | 'PUT' | 'DELETE', url: string, body: TBody, config?: Partial<RestConfig>) {
    return this.rest.request<TBody, TResult>({ method, url: `/meetings${url}`, body }, { apiName: this.apiName, ...config });
  }

  getList = (input: FilterMeetingDto, config?: Partial<RestConfig>) => this.call<FilterMeetingDto, PagedResultDto<MeetingListDto>>('POST', '/list', input, config);
  get = (id: string, config?: Partial<RestConfig>) => this.call<{ id: string }, MeetingDto>('POST', '/getbyid', { id }, config);
  create = (input: CreateMeetingDto, config?: Partial<RestConfig>) => this.call<CreateMeetingDto, string>('POST', '', input, config);
  beginRecordingUpload = (input: BeginRecordingUploadDto, config?: Partial<RestConfig>) =>
    this.call<BeginRecordingUploadDto, RecordingUploadTicketDto>('POST', '/begin-recording-upload', input, config);
  confirmRecording = (id: string, config?: Partial<RestConfig>) => this.call<{ id: string }, MeetingDto>('POST', '/confirm-recording', { id }, config);
  generateMinutes = (id: string, config?: Partial<RestConfig>) => this.call<{ id: string }, MeetingDto>('POST', '/generate-minutes', { id }, config);
  updateMinutes = (input: UpdateMinutesDto, config?: Partial<RestConfig>) => this.call<UpdateMinutesDto, MeetingDto>('PUT', '/minutes', input, config);
  approveMinutes = (id: string, config?: Partial<RestConfig>) => this.call<{ id: string }, MeetingDto>('POST', '/approve-minutes', { id }, config);
  proposeActionItems = (id: string, config?: Partial<RestConfig>) =>
    this.call<{ id: string }, ProposedActionItemsDto>('POST', '/propose-action-items', { id }, config);
  publishMinutes = (id: string, config?: Partial<RestConfig>) => this.call<{ id: string }, MeetingDto>('POST', '/publish-minutes', { id }, config);
  delete = (id: string, config?: Partial<RestConfig>) => this.call<{ id: string }, void>('DELETE', '', { id }, config);

  /** The minutes as a markdown file (the bearer token is attached as for every BFF call). */
  exportMinutes(id: string): Observable<Blob> {
    return this.http.post(`${this.config.apiUrl(this.apiName)}/meetings/export-minutes`, { id }, { responseType: 'blob' });
  }

  /** PUTs the recording straight to storage through the pre-signed url (never through the BFF). */
  putToStorage(ticket: RecordingUploadTicketDto, file: Blob): Observable<HttpEvent<unknown>> {
    const request = new HttpRequest('PUT', ticket.uploadUrl, file, { headers: new HttpHeaders(ticket.headers), reportProgress: true, responseType: 'text' });
    return this.http.request(request);
  }
}
