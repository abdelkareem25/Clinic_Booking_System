import { Role } from '../core/models/auth.model';

export interface NavItem {
  label: string;
  icon: string;
  route: string;
  /** Roles allowed to see this item. Empty array = any authenticated user. */
  roles: Role[];
}

export const NAV_ITEMS: NavItem[] = [
  { label: 'Dashboard', icon: 'dashboard', route: '/dashboard', roles: [] },
  { label: 'Doctors', icon: 'medical_services', route: '/doctors', roles: [] },
  { label: 'Patients', icon: 'groups', route: '/patients', roles: [] },
  { label: 'Appointments', icon: 'event_available', route: '/appointments', roles: [] },
  { label: 'Doctor Schedules', icon: 'calendar_month', route: '/doctor-schedules', roles: [] },
  { label: 'Users', icon: 'manage_accounts', route: '/users', roles: ['Admin'] }
];
