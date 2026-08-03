import { Role } from '../models/auth.model';

/**
 * Permission vocabulary.
 *
 * A permission is always `<module>.<action>`. Keeping it to that one shape is
 * what lets the Roles screen render itself from data (group by module, one
 * checkbox per action) instead of hard-coding a matrix, and lets the UI ask
 * `can('patients.delete')` without a lookup table.
 */
export const PERMISSION_MODULES = [
  'dashboard',
  'patients',
  'doctors',
  'appointments',
  'schedules',
  'records',
  'accounts',
  'reports',
  'users',
  'roles',
  'settings',
] as const;

export type PermissionModule = (typeof PERMISSION_MODULES)[number];

export const PERMISSION_ACTIONS = ['view', 'create', 'edit', 'delete', 'export', 'manage'] as const;

export type PermissionAction = (typeof PERMISSION_ACTIONS)[number];

export type Permission = `${PermissionModule}.${PermissionAction}`;

/** Which actions are meaningful for a module — not every module has all six. */
export const MODULE_ACTIONS: Record<PermissionModule, readonly PermissionAction[]> = {
  dashboard: ['view'],
  patients: ['view', 'create', 'edit', 'delete', 'export'],
  doctors: ['view', 'create', 'edit', 'delete'],
  appointments: ['view', 'create', 'edit', 'delete', 'export'],
  schedules: ['view', 'create', 'edit', 'delete'],
  records: ['view', 'create', 'edit', 'delete', 'export'],
  accounts: ['view', 'create', 'edit', 'delete', 'export'],
  reports: ['view', 'export'],
  users: ['view', 'create', 'edit', 'delete'],
  roles: ['view', 'manage'],
  settings: ['view', 'manage'],
};

/** Every permission this application recognises, in display order. */
export const ALL_PERMISSIONS: Permission[] = PERMISSION_MODULES.flatMap((module) =>
  MODULE_ACTIONS[module].map((action) => `${module}.${action}` as Permission)
);

export function permissionsFor(module: PermissionModule): Permission[] {
  return MODULE_ACTIONS[module].map((action) => `${module}.${action}` as Permission);
}

// -----------------------------------------------------------------------------
// Roles
// -----------------------------------------------------------------------------

export interface ClinicRole {
  id: string;
  /** Matches the JWT role claim for built-in roles. */
  name: string;
  description: string;
  /** System roles map to backend `[Authorize(Roles = …)]` and cannot be deleted. */
  isSystem: boolean;
  permissions: Permission[];
}

/**
 * The built-in roles, seeded on first run and editable afterwards from the
 * Roles screen. They mirror `ClinicRoles` on the API side, so a user whose JWT
 * carries `Admin` resolves to the Admin permission set here.
 */
export const SYSTEM_ROLES: ClinicRole[] = [
  {
    id: 'role-admin',
    name: 'Admin',
    description: 'Full access to every module, including finance and user administration.',
    isSystem: true,
    permissions: [...ALL_PERMISSIONS],
  },
  {
    id: 'role-doctor',
    name: 'Doctor',
    description: 'Clinical access: patients, records, own schedule and appointments.',
    isSystem: true,
    permissions: [
      'dashboard.view',
      'patients.view',
      'patients.edit',
      'doctors.view',
      'appointments.view',
      'appointments.create',
      'appointments.edit',
      'schedules.view',
      'records.view',
      'records.create',
      'records.edit',
      'reports.view',
      'settings.view',
    ],
  },
  {
    id: 'role-receptionist',
    name: 'Receptionist',
    description: 'Front-desk access: registration, booking and payment collection.',
    isSystem: true,
    permissions: [
      'dashboard.view',
      'patients.view',
      'patients.create',
      'patients.edit',
      'doctors.view',
      'appointments.view',
      'appointments.create',
      'appointments.edit',
      'appointments.delete',
      'schedules.view',
      'records.view',
      'settings.view',
    ],
  },
];

/**
 * Fallback used when the JWT carries no role claim at all.
 *
 * The seeded backend account has no role assigned, so without this the app
 * would authenticate successfully and then show an empty shell. Read-only
 * access is the safe middle ground: the user can see the workspace and nothing
 * destructive is exposed.
 */
export const FALLBACK_ROLE_NAME: Role = 'Receptionist';
