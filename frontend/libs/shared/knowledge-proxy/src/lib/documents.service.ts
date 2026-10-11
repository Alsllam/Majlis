import { HttpClient, HttpEvent, HttpHeaders, HttpRequest } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { PagedResultDto, RestConfig, RestService } from '@majlis/core';
import {
  BeginUploadDto,
  DocumentDto,
  DocumentListDto,
  DownloadUrlDto,
  FilterDocumentDto,
  UpdateDocumentDto,
  UploadTicketDto,
} from './document.model';

/** Mirrors `DocumentsAppService` (`[Route("documents")]`, reached as `/api/knowledge/documents/...`). */
@Injectable({ providedIn: 'root' })
export class DocumentsService {
  private readonly rest = inject(RestService);
  private readonly http = inject(HttpClient);
  readonly apiName = 'knowledge';

  private call<TBody, TResult>(method: 'POST' | 'PUT' | 'DELETE', url: string, body: TBody, config?: Partial<RestConfig>) {
    return this.rest.request<TBody, TResult>({ method, url: `/documents${url}`, body }, { apiName: this.apiName, ...config });
  }

  getList = (input: FilterDocumentDto, config?: Partial<RestConfig>) =>
    this.call<FilterDocumentDto, PagedResultDto<DocumentListDto>>('POST', '/list', input, config);
  get = (id: string, config?: Partial<RestConfig>) => this.call<{ id: string }, DocumentDto>('POST', '/getbyid', { id }, config);
  beginUpload = (input: BeginUploadDto, config?: Partial<RestConfig>) => this.call<BeginUploadDto, UploadTicketDto>('POST', '/begin-upload', input, config);
  confirmUpload = (versionId: string, config?: Partial<RestConfig>) =>
    this.call<{ versionId: string }, DocumentDto>('POST', '/confirm', { versionId }, config);
  getDownloadUrl = (versionId: string, config?: Partial<RestConfig>) =>
    this.call<{ versionId: string }, DownloadUrlDto>('POST', '/download-url', { versionId }, config);
  update = (input: UpdateDocumentDto, config?: Partial<RestConfig>) => this.call<UpdateDocumentDto, void>('PUT', '', input, config);
  delete = (id: string, config?: Partial<RestConfig>) => this.call<{ id: string }, void>('DELETE', '', { id }, config);

  /**
   * PUTs the file straight to storage through the pre-signed url (never through the BFF). The bearer token must not be
   * sent here, which the OAuth interceptor guarantees because the storage origin is outside `allowedUrls`.
   */
  putToStorage(ticket: UploadTicketDto, file: Blob): Observable<HttpEvent<unknown>> {
    const request = new HttpRequest('PUT', ticket.uploadUrl, file, { headers: new HttpHeaders(ticket.headers), reportProgress: true, responseType: 'text' });
    return this.http.request(request);
  }
}
