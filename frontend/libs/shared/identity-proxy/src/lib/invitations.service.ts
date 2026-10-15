import { Injectable, inject } from '@angular/core';
import { BaseFilterDto, PagedResultDto, RestConfig, RestService } from '@majlis/core';

/** Mirrors `InvitationsAppService` on the Auth host (`/api/identity/invitations/*`). */
export type InvitationStatus = 'Pending' | 'Accepted' | 'Revoked' | 'Expired';

export interface InvitationDto {
  id: string;
  email: string;
  workspaceId: string | null;
  workspaceName: string | null;
  workspaceRole: string | null;
  invitedByDisplayName: string;
  status: InvitationStatus;
  expiresAt: string;
  creationTime: string;
}

export interface FilterInvitationDto extends BaseFilterDto {
  workspaceId?: string | null;
}

export interface CreateInvitationDto {
  email: string;
  workspaceId?: string | null;
  workspaceName?: string | null;
  workspaceRole?: string | null;
  language: 'ar' | 'en';
}

/** `acceptedImmediately`: the email already belonged to a tenant user, who was added right away. */
export interface InvitationResultDto {
  id: string;
  acceptedImmediately: boolean;
}

/** Invitations by email (FR-IDN-005). Permission: `Permissions.Identity.InviteUsers` (tenant admins, workspace owners and admins). */
@Injectable({ providedIn: 'root' })
export class InvitationsService {
  private readonly rest = inject(RestService);
  readonly apiName = 'identity';

  private call<TBody, TResult>(url: string, body: TBody, config?: Partial<RestConfig>) {
    return this.rest.request<TBody, TResult>({ method: 'POST', url: `/invitations${url}`, body }, { apiName: this.apiName, ...config });
  }

  getList = (input: FilterInvitationDto, config?: Partial<RestConfig>) => this.call<FilterInvitationDto, PagedResultDto<InvitationDto>>('/list', input, config);
  create = (input: CreateInvitationDto, config?: Partial<RestConfig>) => this.call<CreateInvitationDto, InvitationResultDto>('', input, config);
  revoke = (id: string, config?: Partial<RestConfig>) => this.call<{ id: string }, void>('/revoke', { id }, config);
}
