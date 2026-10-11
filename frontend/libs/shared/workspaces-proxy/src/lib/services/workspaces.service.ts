import { Injectable, inject } from '@angular/core';
import { PagedResultDto, RestConfig, RestService } from '@majlis/core';
import {
  CreateWorkspaceDto,
  FilterWorkspaceDto,
  RemoveWorkspaceMemberDto,
  SetWorkspaceMemberDto,
  UpdateAgentInstructionsDto,
  UpdateWorkspaceDto,
  WorkspaceDto,
  WorkspaceIdDto,
  WorkspaceListDto,
} from '../models/workspace.model';

/** Mirrors `WorkspacesAppService` (`[Route("workspaces")]`, reached as `/api/workspaces/workspaces/...`). */
@Injectable({ providedIn: 'root' })
export class WorkspacesService {
  private readonly rest = inject(RestService);
  readonly apiName = 'workspaces';

  private call<TBody, TResult>(method: 'POST' | 'PUT' | 'DELETE', url: string, body: TBody, config?: Partial<RestConfig>) {
    return this.rest.request<TBody, TResult>({ method, url: `/workspaces${url}`, body }, { apiName: this.apiName, ...config });
  }

  getList = (input: FilterWorkspaceDto, config?: Partial<RestConfig>) =>
    this.call<FilterWorkspaceDto, PagedResultDto<WorkspaceListDto>>('POST', '/list', input, config);
  get = (id: string, config?: Partial<RestConfig>) => this.call<WorkspaceIdDto, WorkspaceDto>('POST', '/getbyid', { id }, config);
  create = (input: CreateWorkspaceDto, config?: Partial<RestConfig>) => this.call<CreateWorkspaceDto, string>('POST', '', input, config);
  update = (input: UpdateWorkspaceDto, config?: Partial<RestConfig>) => this.call<UpdateWorkspaceDto, void>('PUT', '', input, config);
  updateAgentInstructions = (input: UpdateAgentInstructionsDto, config?: Partial<RestConfig>) =>
    this.call<UpdateAgentInstructionsDto, void>('PUT', '/instructions', input, config);
  archive = (id: string, config?: Partial<RestConfig>) => this.call<WorkspaceIdDto, void>('POST', '/archive', { id }, config);
  restore = (id: string, config?: Partial<RestConfig>) => this.call<WorkspaceIdDto, void>('POST', '/restore', { id }, config);
  setMember = (input: SetWorkspaceMemberDto, config?: Partial<RestConfig>) => this.call<SetWorkspaceMemberDto, void>('POST', '/members', input, config);
  removeMember = (input: RemoveWorkspaceMemberDto, config?: Partial<RestConfig>) =>
    this.call<RemoveWorkspaceMemberDto, void>('DELETE', '/members', input, config);
}
