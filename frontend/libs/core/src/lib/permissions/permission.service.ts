import { Injectable, computed, inject } from '@angular/core';
import { AuthService } from '../auth/auth.service';
import { ConfigService } from '../config/config.service';

/**
 * Policy strings are identical to the backend constants (`Permissions.Rooms.CreateRoom`) and support `||` and `&&`
 * (skill §7). Grants come from the roles in the token and the `rolePermissions` map in app-settings.json; the backend
 * re-checks every call, so this only decides what to show.
 */
@Injectable({ providedIn: 'root' })
export class PermissionService {
  private readonly auth = inject(AuthService);
  private readonly config = inject(ConfigService);

  readonly granted = computed<ReadonlySet<string>>(() => {
    const roles = this.auth.user()?.roles ?? [];
    const map = this.config.settings.rolePermissions;
    const set = new Set<string>();
    for (const role of roles) {
      for (const permission of map[role] ?? []) {
        set.add(permission);
      }
    }
    return set;
  });

  isGranted(policy: string | null | undefined): boolean {
    if (!policy || policy.trim() === '') {
      return true;
    }
    const granted = this.granted();
    const one = (name: string) => granted.has('*') || granted.has(name.trim());
    return policy
      .split('||')
      .map((group) => group.split('&&').every((name) => one(name)))
      .some(Boolean);
  }
}
