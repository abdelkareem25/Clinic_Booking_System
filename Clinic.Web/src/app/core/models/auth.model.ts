export type Role = 'Admin' | 'Doctor' | 'Receptionist';

export const ROLES: Role[] = ['Admin', 'Doctor', 'Receptionist'];

export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterRequest {
  displayName: string;
  email: string;
  password: string;
  phoneNumber: string;
}

export interface BackendAuthResponse {
  displayName?: string;
  DisplayName?: string;
  email?: string;
  Email?: string;
  token?: string;
  Token?: string;
  refreshToken?: string;
  RefreshToken?: string;
}

export interface AuthUser {
  displayName: string;
  email: string;
  token: string;
  refreshToken?: string;
  roles: Role[];
}

export interface RefreshTokenRequest {
  token: string;
  refreshToken: string;
}

export interface JwtPayload {
  exp?: number;
  email?: string;
  unique_name?: string;
  nameid?: string;
  role?: string | string[];
  roles?: string | string[];
  [claim: string]: unknown;
}

