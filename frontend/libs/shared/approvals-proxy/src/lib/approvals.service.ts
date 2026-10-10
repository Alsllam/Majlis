import { Injectable, inject } from '@angular/core';
import { PagedResultDto, RestConfig, RestService } from '@majlis/core';
import { ApprovalRequestDto, ApproveRequestDto, FilterApprovalRequestDto, RejectRequestDto } from './approval.model';

/** Mirrors `ApprovalRequestsAppService` (`[Route("requests")]`, reached as `/api/approvals/requests/...`). */
@Injectable({ providedIn: 'root' })
export class ApprovalsService {
  private readonly rest = inject(RestService);
  readonly apiName = 'approvals';

  private call<TBody, TResult>(url: string, body: TBody, config?: Partial<RestConfig>) {
    return this.rest.request<TBody, TResult>({ method: 'POST', url: `/requests${url}`, body }, { apiName: this.apiName, ...config });
  }

  getList = (input: FilterApprovalRequestDto, config?: Partial<RestConfig>) =>
    this.call<FilterApprovalRequestDto, PagedResultDto<ApprovalRequestDto>>('/list', input, config);
  get = (id: string, config?: Partial<RestConfig>) => this.call<{ id: string }, ApprovalRequestDto>('/getbyid', { id }, config);
  approve = (input: ApproveRequestDto, config?: Partial<RestConfig>) => this.call<ApproveRequestDto, ApprovalRequestDto>('/approve', input, config);
  reject = (input: RejectRequestDto, config?: Partial<RestConfig>) => this.call<RejectRequestDto, ApprovalRequestDto>('/reject', input, config);
}
