import { inject } from '@angular/core';
import { ActivatedRouteSnapshot, CanActivateFn, Router } from '@angular/router';
import { PermissionService } from './permission.service';

/** Reads `data.requiredPolicy`; routes to `/403` when it is not granted. */
export const permissionGuard: CanActivateFn = (route: ActivatedRouteSnapshot) => {
  const policy = route.data['requiredPolicy'] as string | undefined;
  if (inject(PermissionService).isGranted(policy)) {
    return true;
  }
  return inject(Router).createUrlTree(['/403']);
};
