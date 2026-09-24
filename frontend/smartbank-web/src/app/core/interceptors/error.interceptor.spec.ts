import '@angular/compiler';
import { describe, it, expect, vi } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { HttpErrorResponse, HttpRequest } from '@angular/common/http';
import { Router } from '@angular/router';
import { firstValueFrom, of, throwError } from 'rxjs';
import { errorInterceptor } from './error.interceptor';
import { ToastService } from '../../shared/ui/toast';

function run(status: number, url: string, body?: unknown) {
  const toast = { show: vi.fn() };
  const router = { navigate: vi.fn() };
  const injector = Injector.create([
    { provide: ToastService, useValue: toast },
    { provide: Router, useValue: router },
  ]);
  const req = new HttpRequest('GET', url);
  const err = new HttpErrorResponse({ status, error: body ?? {}, url });
  const next = () => throwError(() => err);
  const result$ = runInInjectionContext(injector, () => errorInterceptor(req, next as never));
  return { toast, router, promise: firstValueFrom(result$.pipe()) .then(
    () => { throw new Error('should have thrown'); },
    (e: unknown) => e,
  ) };
}

describe('errorInterceptor', () => {
  it('status 0 shows Service unavailable and rethrows', async () => {
    const { toast, router, promise } = run(0, 'http://x/api/accounts');
    const err = await promise;
    expect(err).toMatchObject({ status: 0 });
    expect(toast.show).toHaveBeenCalledWith('Service unavailable. Try again.');
    expect(router.navigate).not.toHaveBeenCalled();
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

  it('401 on non-auth routes back to login', async () => {
    const { toast, router, promise } = run(401, 'http://x/api/accounts');
    await promise;
    expect(router.navigate).toHaveBeenCalledWith(['/auth/login'], { replaceUrl: true });
    expect(toast.show).not.toHaveBeenCalled();
  });

  it.each(['/api/auth/login', '/api/auth/mfa/verify', '/api/auth/refresh'])(
    '401 on auth-flow %s never boots to login (AuthStore shows form error)',
    async (path) => {
      const { toast, router, promise } = run(401, `http://x${path}`, { detail: 'Bad.' });
      await promise;
      expect(router.navigate).not.toHaveBeenCalled();
      expect(toast.show).not.toHaveBeenCalled();
    }
  );

  it('403 shows permission toast', async () => {
    const { toast, router, promise } = run(403, 'http://x/api/cards');
    await promise;
    expect(toast.show).toHaveBeenCalledWith('You do not have permission to do that.');
    expect(router.navigate).not.toHaveBeenCalled();
  });

  it('unhandled status (404) just rethrows', async () => {
    const { toast, router, promise } = run(404, 'http://x/api/nope');
    const err = await promise;
    expect(err).toMatchObject({ status: 404 });
    expect(toast.show).not.toHaveBeenCalled();
    expect(router.navigate).not.toHaveBeenCalled();
  });

  it('success passes through untouched', async () => {
    const toast = { show: vi.fn() };
    const router = { navigate: vi.fn() };
    const injector = Injector.create([
      { provide: ToastService, useValue: toast },
      { provide: Router, useValue: router },
    ]);
    const req = new HttpRequest('GET', 'http://x/api/accounts');
    const out = await runInInjectionContext(injector, () =>
      firstValueFrom(errorInterceptor(req, () => of('ok') as never))
    );
    expect(out).toBe('ok');
    expect(toast.show).not.toHaveBeenCalled();
    expect(router.navigate).not.toHaveBeenCalled();
  });
});
