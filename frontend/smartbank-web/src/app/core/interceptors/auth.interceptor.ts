import { HttpInterceptorFn, HttpRequest, HttpHandlerFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { from, switchMap } from 'rxjs';
import { AuthStore } from '../../features/auth/auth.store';

function attach(req: HttpRequest<unknown>, next: HttpHandlerFn, store: AuthStore) {
  const token = store.accessToken();
  const headers: Record<string, string> = {
    // Correlate SPA -> gateway -> services (see CorrelationIdMiddleware + Tempo/Loki).
    'X-Correlation-Id': crypto.randomUUID(),
  };
  if (token) headers['Authorization'] = `Bearer ${token}`;
  return next(req.clone({ setHeaders: headers }));
}

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const store = inject(AuthStore);
  // Never try to refresh the session for the auth flow itself (login/MFA/refresh/me)
  // — that would recurse and would mask credential errors as session expiry.
  if (req.url.includes('/api/auth/') || !store.needsRefresh()) {
    return attach(req, next, store);
  }
  // Token expires soon and the user is doing an action: rotate it first so the
  // session timer (store.expiresAt) moves. Concurrent actions share one refresh.
  return from(store.refreshIfNeeded()).pipe(switchMap(() => attach(req, next, store)));
};
