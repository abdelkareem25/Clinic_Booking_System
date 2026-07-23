import { HttpContextToken, HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';

import { NotificationService } from '../services/notification.service';

/** Set on a request's HttpContext to opt out of the automatic error snackbar. */
export const SKIP_ERROR_NOTIFY = new HttpContextToken<boolean>(() => false);

export const errorInterceptor: HttpInterceptorFn = (request, next) => {
  const notifications = inject(NotificationService);

  return next(request).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse && shouldNotify(request.url, error, request.context.get(SKIP_ERROR_NOTIFY))) {
        notifications.error(toUserMessage(error));
      }

      return throwError(() => error);
    })
  );
};

function shouldNotify(url: string, error: HttpErrorResponse, skip: boolean): boolean {
  if (skip || url.includes('/Accounts/RefreshToken')) {
    return false;
  }

  // The backend returns 404 for empty collections; components render their own
  // empty state, so a global error toast would just be noise.
  return error.status !== 404;
}

function toUserMessage(error: HttpErrorResponse): string {
  if (typeof error.error === 'string' && error.error.trim().length > 0) {
    return error.error;
  }

  if (error.error?.message) {
    return String(error.error.message);
  }

  if (Array.isArray(error.error)) {
    return error.error.map((item) => item.description ?? item.message ?? item).join(', ');
  }

  switch (error.status) {
    case 0:
      return 'The API is not reachable. Check that the ASP.NET Core backend is running.';
    case 400:
      return 'The request could not be processed. Please review the form values.';
    case 401:
      return 'Your session is not authorized for this action.';
    case 403:
      return 'You do not have permission to perform this action.';
    case 404:
      return 'The requested clinic record was not found.';
    case 500:
      return 'The server hit an unexpected error.';
    default:
      return error.message || 'Something went wrong.';
  }
}

