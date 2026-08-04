import { Role } from './auth.model';
import { PageQuery } from './pagination.model';

/**
 * A staff account, as served by `GET /api/Accounts`.
 *
 * Named `account` rather than `user` to match the API surface it comes from.
 * The finance module's `accounts` namespace is a different thing entirely —
 * that is money, this is logins.
 */
export interface Account {
  id: string;
  displayName: string;
  userName: string;
  email: string;
  phoneNumber?: string | null;
  role: Role | string;
  isActive: boolean;
  /** Identity's automatic lockout after failed sign-ins — not an admin decision. */
  isLockedOut: boolean;
  createdAtUtc: string;
}

export interface CreateAccountRequest {
  displayName: string;
  userName?: string | null;
  email: string;
  phoneNumber?: string | null;
  password: string;
  confirmPassword: string;
  role: string;
  isActive: boolean;
}

export interface UpdateAccountRequest {
  displayName: string;
  email: string;
  phoneNumber?: string | null;
  role: string;
  isActive: boolean;
  /** Empty leaves the password untouched — the reset is opt-in. */
  newPassword?: string | null;
  confirmNewPassword?: string | null;
}

/**
 * Roles an account may be given.
 *
 * Wider than `ROLES` in `auth.model`, which lists only the *staff* roles the
 * SPA resolves permissions for. `Patient` is a real account role on the API
 * (`ClinicRoles.All`), and the create form has to be able to offer it or an
 * administrator cannot provision a patient login at all.
 */
export const ACCOUNT_ROLES = ['Admin', 'Doctor', 'Receptionist', 'Patient'] as const;

export type AccountRole = (typeof ACCOUNT_ROLES)[number];

export type AccountStatusFilter = 'all' | 'active' | 'inactive';

export interface AccountQuery extends PageQuery {
  role?: string;
  status?: Exclude<AccountStatusFilter, 'all'>;
}
