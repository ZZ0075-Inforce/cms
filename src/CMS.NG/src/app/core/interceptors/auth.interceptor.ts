import { inject } from '@angular/core';
import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { Router } from '@angular/router';
import { MessageService } from 'primeng/api';
import { catchError, throwError } from 'rxjs';
import { AuthService } from '@core/services/auth.service';

/** Lowercased so it matches the capital-A `/api/Auth/login` route case-insensitively. */
const LOGIN_URL_FRAGMENT = '/auth/login';

/** Fallback when a 500 body carries no safe message of its own. */
const GENERIC_SERVER_ERROR = '發生未預期的錯誤，請稍後再試。';

/**
 * Attaches the bearer token to every outgoing request and centralises HTTP error handling:
 *
 * - 401: clears the session and returns to the Login page. The login request itself is exempt, so its
 *   own 401 (bad credentials) surfaces to the Login component instead of bouncing the user mid-attempt.
 * - 500-class: shows a friendly error toast using the safe `message` from the response body (the API's
 *   global handler returns `{ message }` only — never a stack trace or SQL).
 * - Everything else (400 validation, 403, 404, …) passes through unchanged for the caller to handle.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const messageService = inject(MessageService);

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
      } else if (error.status >= 500) {
        messageService.add({
          severity: 'error',
          summary: '系統錯誤',
          detail: error.error?.message ?? GENERIC_SERVER_ERROR
        });
      }
      return throwError(() => error);
    })
  );
};
