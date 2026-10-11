import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ConfigService, PagedResultDto, RestConfig, RestService } from '@majlis/core';
import { AuditEntryDto, ChainVerificationDto, FilterAuditDto } from './audit.model';

/** Mirrors `AuditAppService` (`[Route("entries")]`, reached as `/api/audit/entries/...`). Read-only by design. */
@Injectable({ providedIn: 'root' })
export class AuditService {
  private readonly rest = inject(RestService);
  private readonly http = inject(HttpClient);
  private readonly config = inject(ConfigService);
  readonly apiName = 'audit';

  private call<TBody, TResult>(url: string, body: TBody, config?: Partial<RestConfig>) {
    return this.rest.request<TBody, TResult>({ method: 'POST', url: `/entries${url}`, body }, { apiName: this.apiName, ...config });
  }

  getList = (input: FilterAuditDto, config?: Partial<RestConfig>) => this.call<FilterAuditDto, PagedResultDto<AuditEntryDto>>('/list', input, config);
  verify = (config?: Partial<RestConfig>) => this.call<Record<string, never>, ChainVerificationDto>('/verify', {}, config);

  /** The CSV file (UTF-8 with BOM) of the filtered entries; the bearer token is attached as for every BFF call. */
  export(input: FilterAuditDto): Observable<Blob> {
    return this.http.post(`${this.config.apiUrl(this.apiName)}/entries/export`, input, { responseType: 'blob' });
  }
}
