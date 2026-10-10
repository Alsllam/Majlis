import { Injectable, inject } from '@angular/core';
import { BaseFilterDto, PagedResultDto, RestConfig, RestService } from '@majlis/core';

/** Mirrors `UsersAppService` on the Auth host (`/api/identity/users/*`). */
export interface UserLookupDto {
  id: string;
  displayName: string;
  email: string;
}

export type FilterUserDto = BaseFilterDto;

/** Users of the caller's tenant, for member pickers. Permission: `Permissions.Identity.ViewUsers`. */
@Injectable({ providedIn: 'root' })
export class UsersService {
  private readonly rest = inject(RestService);
  readonly apiName = 'identity';

  lookup = (input: FilterUserDto, config?: Partial<RestConfig>) =>
    this.rest.request<FilterUserDto, PagedResultDto<UserLookupDto>>({ method: 'POST', url: '/users/lookup', body: input }, { apiName: this.apiName, ...config });
}
