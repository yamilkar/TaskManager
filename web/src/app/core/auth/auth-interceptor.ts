import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { APP_CONFIG } from '../config/app-config';
import { AuthStore } from './auth-store';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const config = inject(APP_CONFIG);
  const token = inject(AuthStore).token();

  const isApiRequest = [config.usersApiUrl, config.tasksApiUrl].some((base) => req.url.startsWith(base));
  if (!token || !isApiRequest) {
    return next(req);
  }
  return next(req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }));
};
