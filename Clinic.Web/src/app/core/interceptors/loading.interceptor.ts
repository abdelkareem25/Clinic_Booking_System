import { HttpContextToken, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { finalize } from 'rxjs';

import { LoadingService } from '../services/loading.service';

export const loadingInterceptor: HttpInterceptorFn = (request, next) => {
  const loading = inject(LoadingService);

  if (request.context.get(SKIP_LOADING)) {
    return next(request);
  }

  loading.show();
  return next(request).pipe(finalize(() => loading.hide()));
};

export const SKIP_LOADING = new HttpContextToken<boolean>(() => false);
