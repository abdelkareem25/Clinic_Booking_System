import { Role } from '../../core/models/auth.model';

export interface SessionUser {
  id: string;
  displayName: string;
  username: string | null;
  email: string;
  roles: Role[];
  expiresAt: Date | null;
}
