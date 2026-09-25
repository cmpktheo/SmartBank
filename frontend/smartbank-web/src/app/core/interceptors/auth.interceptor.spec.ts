import '@angular/compiler';
import { describe, it, expect, vi } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { HttpRequest } from '@angular/common/http';
import { firstValueFrom, of } from 'rxjs';
import { authInterceptor } from './auth.interceptor';
import { AuthStore } from '../../features/auth/auth.store';

const UUID_RE = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

async function run(token: string | null, needsRefresh = false) {
  const store = {
    accessToken: () => token,
    needsRefresh: () => needsRefresh,
    refreshIfNeeded: async () => !!token,
  };
  const injector = Injector.create([{ provide: AuthStore, useValue: store }]);
  const original = new HttpRequest('POST', 'http://x/api/transfers', { amount: '10' });
  let forwarded!: HttpRequest<unknown>;
  const out = await runInInjectionContext(injector, () =>
    firstValueFrom(
      authInterceptor(original, ((req: HttpRequest<unknown>) => {
        forwarded = req;
        return of('ok');
      }) as never)
    )
  );
  return { out, original, forwarded: forwarded! };
}

describe('authInterceptor', () => {
  it('attaches Bearer token + correlation id, preserving method/url/body', async () => {
    const { out, original, forwarded } = await run('tok-123');
    expect(out).toBe('ok');
    expect(forwarded.headers.get('Authorization')).toBe('Bearer tok-123');
    expect(forwarded.headers.get('X-Correlation-Id')).toMatch(UUID_RE);
    expect(forwarded.method).toBe(original.method);
    expect(forwarded.url).toBe(original.url);
    expect(forwarded.body).toEqual(original.body);
    // original request is immutable — headers live only on the clone
    expect(original.headers.has('Authorization')).toBe(false);
    expect(original.headers.has('X-Correlation-Id')).toBe(false);
  });

  it('omits Authorization when anonymous but still correlates', async () => {
    const { forwarded } = await run(null);
    expect(forwarded.headers.has('Authorization')).toBe(false);
    expect(forwarded.headers.get('X-Correlation-Id')).toMatch(UUID_RE);
  });

  it('issues a fresh correlation id per request', async () => {
    const first = await run('t');
    const second = await run('t');
    expect(first.forwarded.headers.get('X-Correlation-Id')).not.toBe(
      second.forwarded.headers.get('X-Correlation-Id')
    );
  });

  it('refreshes an expiring session before attaching the (rotated) token', async () => {
    let current = 'old';
    const accessToken = vi.fn(() => current);
    const refreshIfNeeded = vi.fn(async () => {
      current = 'new';
      return true;
    });
    const store = { accessToken, needsRefresh: () => true, refreshIfNeeded };
    const injector = Injector.create([{ provide: AuthStore, useValue: store }]);
    const original = new HttpRequest('POST', 'http://x/api/transfers', { amount: '10' });
    let forwarded!: HttpRequest<unknown>;
    const out = await runInInjectionContext(injector, () =>
      firstValueFrom(
        authInterceptor(original, ((req: HttpRequest<unknown>) => {
          forwarded = req;
          return of('ok');
        }) as never)
      )
    );
    expect(out).toBe('ok');
    expect(refreshIfNeeded).toHaveBeenCalledTimes(1);
    expect(forwarded.headers.get('Authorization')).toBe('Bearer new');
  });

  it('never refreshes for the auth flow itself', async () => {
    const refreshIfNeeded = vi.fn(async () => true);
    const store = { accessToken: () => 't', needsRefresh: () => true, refreshIfNeeded };
    const injector = Injector.create([{ provide: AuthStore, useValue: store }]);
    const original = new HttpRequest('POST', 'http://x/api/auth/refresh', {});
    await runInInjectionContext(injector, () =>
      firstValueFrom(authInterceptor(original, (() => of('ok')) as never))
    );
    expect(refreshIfNeeded).not.toHaveBeenCalled();
  });
});
