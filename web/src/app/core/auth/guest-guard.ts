import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthStore } from './auth-store';

export const guestGuard: CanActivateFn = () => {
  if (inject(AuthStore).isAuthenticated()) {
    return inject(Router).createUrlTree(['/tasks']);
  }
  return true;
};
