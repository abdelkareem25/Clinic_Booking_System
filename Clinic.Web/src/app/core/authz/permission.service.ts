import { Injectable, Signal, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';

import { AuthService } from '../services/auth.service';
import { FALLBACK_ROLE_NAME, Permission } from './permission.model';
import { RolesStore } from './roles.store';

/**
 * Resolves the signed-in user's effective permissions.
 *
 * Everything the UI hides — a nav item, a route, a "Delete" button — asks this
 * service, so authorisation is expressed once and consistently. Note this is a
 * *usability* layer, not a security boundary: the API still enforces its own
 * `[Authorize]` rules, and it has to, because anything the browser decides can
 * be bypassed.
 */
@Injectable({ providedIn: 'root' })
export class PermissionService {
  private readonly auth = inject(AuthService);
  private readonly rolesStore = inject(RolesStore);

  private readonly user = toSignal(this.auth.currentUser$, {
    initialValue: this.auth.currentUser,
  });

  /** Role names carried by the JWT, falling back when the claim is absent. */
  readonly roleNames = computed<string[]>(() => {
    const roles = this.user()?.roles ?? [];
    return roles.length ? roles : [FALLBACK_ROLE_NAME];
  });

  /** The union of every permission granted by the user's roles. */
  readonly granted = computed<ReadonlySet<Permission>>(() => {
    if (!this.user()) {
      return new Set<Permission>();
    }

    const byRole = this.rolesStore.permissionsByRole();
    const effective = new Set<Permission>();

    for (const roleName of this.roleNames()) {
      const permissions = byRole.get(roleName.toLowerCase());
      permissions?.forEach((permission) => effective.add(permission));
    }

    return effective;
  });

  readonly isAdmin = computed(() =>
    this.roleNames().some((role) => role.toLowerCase() === 'admin')
  );

  can(permission: Permission): boolean {
    return this.granted().has(permission);
  }

  canAny(permissions: readonly Permission[]): boolean {
    if (!permissions.length) {
      return true;
    }
    const granted = this.granted();
    return permissions.some((permission) => granted.has(permission));
  }

  canAll(permissions: readonly Permission[]): boolean {
    const granted = this.granted();
    return permissions.every((permission) => granted.has(permission));
  }

  /** Reactive form of `can`, for templates that must re-render on a role change. */
  can$(permission: Permission): Signal<boolean> {
    return computed(() => this.granted().has(permission));
  }
}
