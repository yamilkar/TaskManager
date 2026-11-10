import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { APP_CONFIG } from '../config/app-config';
import { AuthStore } from './auth-store';

function originOf(url: string): string | null {
  try {
    return new URL(url, document.baseURI).origin;
  } catch {
    return null;
  }
}

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const config = inject(APP_CONFIG);
  const token = inject(AuthStore).token();

  const allowedOrigins = [config.usersApiUrl, config.tasksApiUrl].map(originOf);
  const origin = originOf(req.url);
  if (!token || origin === null || !allowedOrigins.includes(origin)) {
    return next(req);
  }
  return next(req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }));
};
