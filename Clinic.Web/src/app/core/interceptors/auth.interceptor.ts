import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';

import { environment } from '../../../environments/environment';
import { AuthService } from '../services/auth.service';
import { TokenStorageService } from '../services/token-storage.service';

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(AuthService);
  const storage = inject(TokenStorageService);
  const token = storage.getAccessToken();
  const authenticatedRequest = token && !isAuthEndpoint(request)
    ? addBearerToken(request, token)
    : request;

  return next(authenticatedRequest).pipe(
    catchError((error: unknown) => {
      if (
        error instanceof HttpErrorResponse &&
        error.status === 401 &&
        token &&
        !isAuthEndpoint(request) &&
        storage.getRefreshToken()
      ) {
        return auth.refreshToken().pipe(
          switchMap((user) => next(addBearerToken(request, user.token))),
          catchError((refreshError: unknown) => {
            auth.logout();
            return throwError(() => refreshError);
          })
        );
      }

      return throwError(() => error);
    })
  );
};

function addBearerToken(request: HttpRequest<unknown>, token: string): HttpRequest<unknown> {
  return request.clone({
    setHeaders: {
      Authorization: `Bearer ${token}`
    }
  });
}

function isAuthEndpoint(request: HttpRequest<unknown>): boolean {
  const url = request.url.toLowerCase();
  return (
    url.includes('/accounts/login') ||
    url.includes('/accounts/register') ||
    url.includes(environment.refreshTokenEndpoint.toLowerCase())
  );
}

