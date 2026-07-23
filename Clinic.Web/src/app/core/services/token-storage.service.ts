import { Injectable } from '@angular/core';

import { AuthUser } from '../models/auth.model';

@Injectable({ providedIn: 'root' })
export class TokenStorageService {
  private readonly sessionKey = 'clinic.auth.session';

  getSession(): AuthUser | null {
    const raw = this.readStorage();
    if (!raw) {
      return null;
    }

    try {
      return JSON.parse(raw) as AuthUser;
    } catch {
      this.clear();
      return null;
    }
  }

  setSession(session: AuthUser): void {
    try {
      localStorage.setItem(this.sessionKey, JSON.stringify(session));
    } catch {
      sessionStorage.setItem(this.sessionKey, JSON.stringify(session));
    }
  }

  getAccessToken(): string | null {
    return this.getSession()?.token ?? null;
  }

  getRefreshToken(): string | null {
    return this.getSession()?.refreshToken ?? null;
  }

  clear(): void {
    localStorage.removeItem(this.sessionKey);
    sessionStorage.removeItem(this.sessionKey);
  }

  private readStorage(): string | null {
    return localStorage.getItem(this.sessionKey) ?? sessionStorage.getItem(this.sessionKey);
  }
}

