import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '@core/services/auth.service';

/**
 * Blocks protected routes when there is no valid session, redirecting to the public Login page.
 * Returns a UrlTree (rather than navigating imperatively) so the router cancels the in-flight
 * navigation cleanly.
 */
export const authGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  return auth.isAuthenticated() ? true : router.createUrlTree(['/login']);
};
