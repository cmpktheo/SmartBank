import '@angular/compiler';
import { describe, it, expect, vi } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { HttpErrorResponse, HttpRequest } from '@angular/common/http';
import { firstValueFrom, of, throwError } from 'rxjs';
import { errorInterceptor } from './error.interceptor';
import { ToastService } from '../../shared/ui/toast';
import { AuthStore } from '../../features/auth/auth.store';

function run(
  status: number,
  url: string,
  body?: unknown,
  opts?: { refreshTokenAfter?: string | null; refresh?: () => Promise<void>; retryStatus?: number }
) {
  const toast = { show: vi.fn() };
  const refresh = vi.fn(opts?.refresh ?? (() => Promise.resolve()));
  const store = {
    clearSession: vi.fn(() => Promise.resolve()),
    refresh,
    accessToken: () => opts?.refreshTokenAfter ?? null,
  };
  const injector = Injector.create([
    { provide: ToastService, useValue: toast },
    { provide: AuthStore, useValue: store },
  ]);
  const req = new HttpRequest('GET', url);
  const err = new HttpErrorResponse({ status, error: body ?? {}, url });
  const next = vi.fn((_r: HttpRequest<unknown>) => {
    if (opts?.retryStatus !== undefined && _r.headers.has('Authorization')) {
      if (opts.retryStatus === 200) return of('retried-ok');
      return throwError(
        () => new HttpErrorResponse({ status: opts.retryStatus, error: {}, url })
      );
    }
    return throwError(() => err);
  });
  const result$ = runInInjectionContext(injector, () => errorInterceptor(req, next as never));
  return {
    toast,
    store,
    next,
    promise: firstValueFrom(result$.pipe()).then(
      (v: unknown) => ({ ok: true as const, value: v }),
      (e: unknown) => ({ ok: false as const, error: e })
    ),
  };
}

describe('errorInterceptor', () => {
  it('status 0 shows Service unavailable and rethrows', async () => {
    const { toast, store, promise } = run(0, 'http://x/api/accounts');
    const res = await promise;
    expect(res.ok).toBe(false);
    if (!res.ok) expect(res.error).toMatchObject({ status: 0 });
    expect(toast.show).toHaveBeenCalledWith('Service unavailable. Try again.');
    expect(store.clearSession).not.toHaveBeenCalled();
  });

  it('5xx shows Service unavailable', async () => {
    const { toast, promise } = run(503, 'http://x/api/transfers');
    await promise;
    expect(toast.show).toHaveBeenCalledWith('Service unavailable. Try again.');
  });

  it('400/409 surface backend detail, default when missing', async () => {
    const bad = run(400, 'http://x/api/transfers', { detail: 'Bad amount.' });
    await bad.promise;
    expect(bad.toast.show).toHaveBeenCalledWith('Bad amount.');

    const plain = run(400, 'http://x/api/transfers', {});
    await plain.promise;
    expect(plain.toast.show).toHaveBeenCalledWith('Request failed.');

    const conflict = run(409, 'http://x/api/cards/1/limits', { detail: 'Stale.' });
    await conflict.promise;
    expect(conflict.toast.show).toHaveBeenCalledWith('Stale.');
  });

  it('401 on non-auth tries silent refresh first, clears when refresh yields no token', async () => {
    const { toast, store, promise } = run(401, 'http://x/api/accounts', {}, { refreshTokenAfter: null });
    const res = await promise;
    expect(res.ok).toBe(false);
    expect(store.refresh).toHaveBeenCalledTimes(1);
    expect(store.clearSession).toHaveBeenCalledTimes(1);
    expect(toast.show).not.toHaveBeenCalled();
  });

  it('401 on non-auth retries once with the rotated token on refresh success', async () => {
    const { store, next, promise } = run(
      401,
      'http://x/api/accounts',
      {},
      { refreshTokenAfter: 'new-tok', retryStatus: 200 }
    );
    const res = await promise;
    expect(res).toEqual({ ok: true, value: 'retried-ok' });
    expect(store.refresh).toHaveBeenCalledTimes(1);
    expect(store.clearSession).not.toHaveBeenCalled();
    expect(next).toHaveBeenCalledTimes(2);
    expect(next.mock.calls[1]![0].headers.get('Authorization')).toBe('Bearer new-tok');
  });

  it('401 on non-auth clears the session when the retry also 401s', async () => {
    const { store, promise } = run(
      401,
      'http://x/api/accounts',
      {},
      { refreshTokenAfter: 'new-tok', retryStatus: 401 }
    );
    const res = await promise;
    expect(res.ok).toBe(false);
    expect(store.refresh).toHaveBeenCalledTimes(1);
    expect(store.clearSession).toHaveBeenCalledTimes(1);
  });

  it.each(['/api/auth/login', '/api/auth/mfa/verify', '/api/auth/refresh', '/api/auth/me'])(
    '401 on auth-flow %s never clears the session (AuthStore shows form error)',
    async (path) => {
      const { toast, store, promise } = run(401, `http://x${path}`, { detail: 'Bad.' });
      await promise;
      expect(store.refresh).not.toHaveBeenCalled();
      expect(store.clearSession).not.toHaveBeenCalled();
      expect(toast.show).not.toHaveBeenCalled();
    }
  );

  it('403 shows permission toast', async () => {
    const { toast, store, promise } = run(403, 'http://x/api/cards');
    await promise;
    expect(toast.show).toHaveBeenCalledWith('You do not have permission to do that.');
    expect(store.clearSession).not.toHaveBeenCalled();
  });

  it('unhandled status (404) just rethrows', async () => {
    const { toast, store, promise } = run(404, 'http://x/api/nope');
    const res = await promise;
    expect(res.ok).toBe(false);
    if (!res.ok) expect(res.error).toMatchObject({ status: 404 });
    expect(toast.show).not.toHaveBeenCalled();
    expect(store.clearSession).not.toHaveBeenCalled();
  });

  it('success passes through untouched', async () => {
    const toast = { show: vi.fn() };
    const store = {
      clearSession: vi.fn(() => Promise.resolve()),
      refresh: vi.fn(() => Promise.resolve()),
      accessToken: () => 't',
    };
    const injector = Injector.create([
      { provide: ToastService, useValue: toast },
      { provide: AuthStore, useValue: store },
    ]);
    const req = new HttpRequest('GET', 'http://x/api/accounts');
    const out = await runInInjectionContext(injector, () =>
      firstValueFrom(errorInterceptor(req, () => of('ok') as never))
    );
    expect(out).toBe('ok');
    expect(toast.show).not.toHaveBeenCalled();
    expect(store.clearSession).not.toHaveBeenCalled();
    expect(store.refresh).not.toHaveBeenCalled();
  });
});
