import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { Permission } from '../authz/permission.model';
import { PermissionService } from '../authz/permission.service';

/**
 * Blocks a route unless the user holds at least one of the given permissions.
 *
 * Routes state their requirement rather than a role list, so changing what a
 * Receptionist may do is a change in the Roles screen, not in the router.
 */
export const permissionGuard = (...permissions: Permission[]): CanActivateFn => {
  return () => {
    const permissionService = inject(PermissionService);
    const router = inject(Router);

    return permissionService.canAny(permissions)
      ? true
      : router.createUrlTree(['/unauthorized']);
  };
};
