import { Injectable, inject } from '@angular/core';
import { Router } from '@angular/router';
import { BehaviorSubject, Observable, map, tap, throwError } from 'rxjs';

import { environment } from '../../../environments/environment';
import {
  AuthUser,
  BackendAuthResponse,
  LoginRequest,
  RefreshTokenRequest,
  RegisterRequest,
  Role
} from '../models/auth.model';
import { ApiService } from './api.service';
import { JwtService } from './jwt.service';
import { TokenStorageService } from './token-storage.service';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly api = inject(ApiService);
  private readonly jwt = inject(JwtService);
  private readonly router = inject(Router);
  private readonly storage = inject(TokenStorageService);
  private readonly userSubject = new BehaviorSubject<AuthUser | null>(this.storage.getSession());

  readonly currentUser$ = this.userSubject.asObservable();
  readonly isAuthenticated$ = this.currentUser$.pipe(
    map((user) => Boolean(user?.token && !this.jwt.isExpired(user.token)))
  );

  get currentUser(): AuthUser | null {
    const user = this.userSubject.value;
    if (!user?.token || this.jwt.isExpired(user.token)) {
      return null;
    }

    return user;
  }

  get accessToken(): string | null {
    return this.currentUser?.token ?? null;
  }

  login(credentials: LoginRequest): Observable<AuthUser> {
    return this.api.post<BackendAuthResponse>('Accounts/Login', credentials).pipe(
      map((response) => this.createSession(response)),
      tap((user) => this.persistSession(user))
    );
  }

  register(payload: RegisterRequest): Observable<AuthUser> {
    return this.api.post<BackendAuthResponse>('Accounts/Register', payload).pipe(
      map((response) => this.createSession(response)),
      tap((user) => this.persistSession(user))
    );
  }

  refreshToken(): Observable<AuthUser> {
    const session = this.storage.getSession();
    if (!session?.refreshToken) {
      return throwError(() => new Error('Refresh token is not available.'));
    }

    const payload: RefreshTokenRequest = {
      token: session.token,
      refreshToken: session.refreshToken
    };

    return this.api.post<BackendAuthResponse>(environment.refreshTokenEndpoint, payload).pipe(
      map((response) => this.createSession(response, session)),
      tap((user) => this.persistSession(user))
    );
  }

  logout(redirect = true): void {
    this.storage.clear();
    this.userSubject.next(null);

    if (redirect) {
      void this.router.navigate(['/auth/login']);
    }
  }

  hasAnyRole(allowedRoles: Role[]): boolean {
    if (allowedRoles.length === 0) {
      return true;
    }

    const roles = this.currentUser?.roles ?? [];
    return allowedRoles.some((role) => roles.includes(role));
  }

  redirectPathFor(_user: AuthUser | null = this.currentUser): string {
    // All authenticated roles (Admin / Doctor / Receptionist) share the same
    // landing dashboard; per-module access is enforced by route guards.
    return '/dashboard';
  }

  private createSession(response: BackendAuthResponse, fallback?: AuthUser): AuthUser {
    const token = response.token ?? response.Token ?? fallback?.token ?? '';
    const refreshToken = response.refreshToken ?? response.RefreshToken ?? fallback?.refreshToken;

    return {
      displayName: response.displayName ?? response.DisplayName ?? fallback?.displayName ?? 'Clinic User',
      email: response.email ?? response.Email ?? fallback?.email ?? '',
      token,
      refreshToken,
      roles: this.jwt.getRoles(token)
    };
  }

  private persistSession(user: AuthUser): void {
    this.storage.setSession(user);
    this.userSubject.next(user);
  }
}

