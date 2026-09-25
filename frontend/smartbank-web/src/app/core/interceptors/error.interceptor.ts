import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, from, switchMap, throwError } from 'rxjs';
import { ToastService } from '../../shared/ui/toast';
import { AuthStore } from '../../features/auth/auth.store';

export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const toast = inject(ToastService);
  const store = inject(AuthStore);
  return next(req).pipe(
    catchError((err: HttpErrorResponse) => {
      // Auth-flow requests (login / MFA / refresh / me) have no trusted session
      // yet: a 401 means bad credentials, a wrong code, or an expired challenge.
      // The AuthStore surfaces that as a form error — never boot back to login.
      const isAuthFlow = req.url.includes('/api/auth/');
      if (err.status === 0 || err.status >= 500) toast.show('Service unavailable. Try again.');
      else if (err.status === 400 || err.status === 409) toast.show(err.error?.detail ?? 'Request failed.');
      // A 401 outside the auth flow means the access token died (expired, revoked,
      // or issued before the backend was wiped). Try one silent refresh first so
      // an action taken near expiry extends the session instead of booting to
      // login; only drop the session when the refresh itself fails. The retry
      // goes downstream via next(), so a second 401 cannot loop back here.
      else if (err.status === 401 && !isAuthFlow) {
        return from(store.refresh()).pipe(
          switchMap(() => {
            const token = store.accessToken();
            if (!token) {
              // Refresh failed (store.refresh already cleared via logout, but be
              // explicit so stubs/mocks and future changes can't leave tokens).
              void store.clearSession();
              return throwError(() => err);
            }
            const retry = req.clone({
              setHeaders: {
                Authorization: `Bearer ${token}`,
                'X-Correlation-Id': crypto.randomUUID(),
              },
            });
            return next(retry).pipe(
              catchError((retryErr: unknown) => {
                if ((retryErr as HttpErrorResponse)?.status === 401) void store.clearSession();
                return throwError(() => retryErr);
              })
            );
          }),
          catchError(() => throwError(() => err))
        );
      }
      else if (err.status === 403) toast.show('You do not have permission to do that.');
      return throwError(() => err);
    })
  );
};
