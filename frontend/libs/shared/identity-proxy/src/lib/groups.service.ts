import { Injectable, inject } from '@angular/core';
import { BaseFilterDto, PagedResultDto, RestConfig, RestService } from '@majlis/core';

/** Mirrors `GroupsAppService` on the Auth host (`/api/identity/groups/*`). */
export interface GroupMemberDto {
  userId: string;
  displayName: string;
  joinedAt: string;
}

export interface GroupDto {
  id: string;
  name: string;
  description: string | null;
  members: GroupMemberDto[];
  creationTime: string;
}

export interface GroupListDto {
  id: string;
  name: string;
  description: string | null;
  memberCount: number;
  creationTime: string;
}

export type FilterGroupDto = BaseFilterDto;

export interface CreateGroupDto {
  name: string;
  description?: string | null;
}

export interface UpdateGroupDto extends CreateGroupDto {
  id: string;
}

export interface SetGroupMemberDto {
  groupId: string;
  userId: string;
}

/** Tenant groups: anyone may list them for pickers (`ViewUsers`); tenant admins change them (`ManageGroups`). */
@Injectable({ providedIn: 'root' })
export class GroupsService {
  private readonly rest = inject(RestService);
  readonly apiName = 'identity';

  private call<TBody, TResult>(method: 'POST' | 'PUT' | 'DELETE', url: string, body: TBody, config?: Partial<RestConfig>) {
    return this.rest.request<TBody, TResult>({ method, url: `/groups${url}`, body }, { apiName: this.apiName, ...config });
  }

  getList = (input: FilterGroupDto, config?: Partial<RestConfig>) => this.call<FilterGroupDto, PagedResultDto<GroupListDto>>('POST', '/list', input, config);
  get = (id: string, config?: Partial<RestConfig>) => this.call<{ id: string }, GroupDto>('POST', '/getbyid', { id }, config);
  create = (input: CreateGroupDto, config?: Partial<RestConfig>) => this.call<CreateGroupDto, string>('POST', '', input, config);
  update = (input: UpdateGroupDto, config?: Partial<RestConfig>) => this.call<UpdateGroupDto, void>('PUT', '', input, config);
  delete = (id: string, config?: Partial<RestConfig>) => this.call<{ id: string }, void>('DELETE', '', { id }, config);
  setMember = (input: SetGroupMemberDto, config?: Partial<RestConfig>) => this.call<SetGroupMemberDto, GroupDto>('POST', '/members', input, config);
  removeMember = (input: SetGroupMemberDto, config?: Partial<RestConfig>) => this.call<SetGroupMemberDto, GroupDto>('DELETE', '/members', input, config);
}
