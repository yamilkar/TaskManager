import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { AuthStore } from '../auth/auth-store';
import { toAppError } from './app-error';

// 401s from these endpoints are credential/session probes, not session expiry.
// Guards and AuthStore.restoreSession handle those cases.
const NO_EXPIRY_REDIRECT_PATHS = ['/api/v1/auth/', '/api/v1/users/me'];

export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const authStore = inject(AuthStore);
  const router = inject(Router);

  return next(req).pipe(
    catchError((err: HttpErrorResponse) => {
      const isExpiry = err.status === 401 && !NO_EXPIRY_REDIRECT_PATHS.some((p) => req.url.includes(p));
      if (isExpiry) {
        authStore.logout();
        void router.navigate(['/login'], { queryParams: { returnUrl: router.url, reason: 'expired' } });
      }
      return throwError(() => toAppError(err));
    }),
  );
};
