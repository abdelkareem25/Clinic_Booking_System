import { Permission } from '../core/authz/permission.model';
import { IconName } from '../shared/ui/icon/icon.registry';

export interface NavItem {
  /** Translation key. */
  label: string;
  icon: IconName;
  route: string;
  /** Shown only if the user holds at least one of these. Empty = always. */
  permissions: Permission[];
}

export interface NavGroup {
  /** Translation key for the group heading; `null` renders the items ungrouped. */
  label: string | null;
  items: NavItem[];
}

/**
 * The sidebar.
 *
 * Fourteen flat entries — the reference app's approach — is a wall a new
 * receptionist has to read top to bottom every time. Grouping by what someone
 * is trying to *do* (see a patient, run the day, handle money, administer the
 * system) turns it into four short scans, and matches how clinic roles divide.
 *
 * Groups whose items are all permission-filtered away disappear entirely, so a
 * Receptionist never sees an empty "Finance" heading.
 */
export const NAV_GROUPS: NavGroup[] = [
  {
    label: null,
    items: [
      {
        label: 'nav.dashboard',
        icon: 'dashboard',
        route: '/dashboard',
        permissions: ['dashboard.view'],
      },
    ],
  },
  {
    label: 'nav.groupClinical',
    items: [
      { label: 'nav.patients', icon: 'patients', route: '/patients', permissions: ['patients.view'] },
      { label: 'nav.doctors', icon: 'doctors', route: '/doctors', permissions: ['doctors.view'] },
      { label: 'nav.records', icon: 'records', route: '/records', permissions: ['records.view'] },
    ],
  },
  {
    label: 'nav.groupOperations',
    items: [
      {
        label: 'nav.appointments',
        icon: 'appointments',
        route: '/appointments',
        permissions: ['appointments.view'],
      },
      {
        label: 'nav.schedules',
        icon: 'schedules',
        route: '/schedules',
        permissions: ['schedules.view'],
      },
    ],
  },
  {
    label: 'nav.groupFinance',
    items: [
      { label: 'nav.accounts', icon: 'accounts', route: '/accounts', permissions: ['accounts.view'] },
      { label: 'nav.reports', icon: 'reports', route: '/reports', permissions: ['reports.view'] },
    ],
  },
  {
    label: 'nav.groupAdmin',
    items: [
      { label: 'nav.users', icon: 'users', route: '/users', permissions: ['users.view'] },
      { label: 'nav.roles', icon: 'roles', route: '/roles', permissions: ['roles.view'] },
      { label: 'nav.settings', icon: 'settings', route: '/settings', permissions: ['settings.view'] },
    ],
  },
];
