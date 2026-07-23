import { Injectable } from '@angular/core';

import { JwtPayload, ROLES, Role } from '../models/auth.model';

const ROLE_CLAIM = 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role';
const NAME_ID_CLAIM = 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier';
const NAME_CLAIM = 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name';

@Injectable({ providedIn: 'root' })
export class JwtService {
  getUserId(token: string | null | undefined): string | null {
    const payload = this.decode(token);
    const value = payload?.nameid ?? payload?.[NAME_ID_CLAIM];
    return value != null ? String(value) : null;
  }

  getUserName(token: string | null | undefined): string | null {
    const payload = this.decode(token);
    const value = payload?.unique_name ?? payload?.[NAME_CLAIM];
    return value != null ? String(value) : null;
  }

  getExpiry(token: string | null | undefined): Date | null {
    const payload = this.decode(token);
    return payload?.exp ? new Date(payload.exp * 1000) : null;
  }

  decode(token: string | null | undefined): JwtPayload | null {
    if (!token) {
      return null;
    }

    const [, payload] = token.split('.');
    if (!payload) {
      return null;
    }

    try {
      return JSON.parse(this.base64UrlDecode(payload)) as JwtPayload;
    } catch {
      return null;
    }
  }

  getRoles(token: string | null | undefined): Role[] {
    const payload = this.decode(token);
    const rawRoles = payload?.role ?? payload?.roles ?? payload?.[ROLE_CLAIM];
    const values = Array.isArray(rawRoles) ? rawRoles : rawRoles ? [rawRoles] : [];

    return values.filter((role): role is Role => (ROLES as string[]).includes(role));
  }

  isExpired(token: string | null | undefined): boolean {
    const payload = this.decode(token);
    if (!payload?.exp) {
      return !token;
    }

    return payload.exp * 1000 <= Date.now();
  }

  private base64UrlDecode(value: string): string {
    const normalized = value.replace(/-/g, '+').replace(/_/g, '/');
    const padded = normalized.padEnd(normalized.length + ((4 - (normalized.length % 4)) % 4), '=');
    const decoded = atob(padded);

    try {
      return decodeURIComponent(
        Array.from(decoded)
          .map((char) => `%${char.charCodeAt(0).toString(16).padStart(2, '0')}`)
          .join('')
      );
    } catch {
      return decoded;
    }
  }
}

