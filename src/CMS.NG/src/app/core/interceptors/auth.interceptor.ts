import { inject } from '@angular/core';
import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { AuthService } from '@core/services/auth.service';

/** Lowercased so it matches the capital-A `/api/Auth/login` route case-insensitively. */
const LOGIN_URL_FRAGMENT = '/auth/login';

/**
 * Attaches the bearer token to every outgoing request and, on a 401, clears the session and returns to
 * the Login page. The login request itself is exempt from that redirect, so its own 401 (bad
 * credentials) surfaces to the Login component instead of bouncing the user mid-attempt.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  const token = auth.token;
  const request = token
    ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
    : req;

  const isLoginRequest = req.url.toLowerCase().includes(LOGIN_URL_FRAGMENT);

  return next(request).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status === 401 && !isLoginRequest) {
        auth.clearSession();
        router.navigateByUrl('/login');
      }
      return throwError(() => error);
    })
  );
};
