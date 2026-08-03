import { Injectable, computed } from '@angular/core';

import { LocalCollection, newId } from '../data/local-collection';
import { ClinicRole, Permission, SYSTEM_ROLES } from './permission.model';

/**
 * The role → permission mapping.
 *
 * ASP.NET Identity owns *which role a user holds* (it is a JWT claim); this
 * store owns *what a role may do*. Splitting it that way is what makes the
 * Roles screen editable without a backend change — and if a permissions
 * endpoint is added later, only `load`/`persist` here have to move.
 */
@Injectable({ providedIn: 'root' })
export class RolesStore {
  private readonly collection = new LocalCollection<ClinicRole>({
    key: 'roles',
    version: 1,
    seed: () => SYSTEM_ROLES.map((role) => ({ ...role, permissions: [...role.permissions] })),
    searchFields: ['name', 'description'],
  });

  readonly roles = this.collection.all;
  readonly count = this.collection.count;

  /** Role name → permission set, for the O(1) lookup PermissionService needs. */
  readonly permissionsByRole = computed(() => {
    const map = new Map<string, ReadonlySet<Permission>>();
    for (const role of this.roles()) {
      map.set(role.name.toLowerCase(), new Set(role.permissions));
    }
    return map;
  });

  getById(id: string): ClinicRole | undefined {
    return this.collection.getById(id);
  }

  getByName(name: string): ClinicRole | undefined {
    const needle = name.toLowerCase();
    return this.roles().find((role) => role.name.toLowerCase() === needle);
  }

  create(input: Pick<ClinicRole, 'name' | 'description' | 'permissions'>): ClinicRole {
    return this.collection.insert({
      id: newId('role'),
      name: input.name.trim(),
      description: input.description.trim(),
      isSystem: false,
      permissions: [...input.permissions],
    });
  }

  update(id: string, patch: Partial<Pick<ClinicRole, 'name' | 'description' | 'permissions'>>): void {
    const existing = this.collection.getById(id);
    if (!existing) {
      return;
    }

    // A system role's name is the JWT claim the backend issues — renaming it
    // would silently disconnect every user holding it.
    const next: Partial<ClinicRole> = { ...patch };
    if (existing.isSystem) {
      delete next.name;
    }

    this.collection.update(id, next);
  }

  remove(id: string): boolean {
    const role = this.collection.getById(id);
    if (!role || role.isSystem) {
      return false;
    }
    return this.collection.remove(id);
  }

  /** Restore the shipped permission matrix. */
  resetToDefaults(): void {
    this.collection.reset();
  }
}
