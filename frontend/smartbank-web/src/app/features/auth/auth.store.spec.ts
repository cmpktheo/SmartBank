import '@angular/compiler';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { of, throwError, delay } from 'rxjs';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { AuthStore } from './auth.store';

const mem = new Map<string, string>();

function stubSessionStorage() {
  mem.clear();
  vi.stubGlobal('sessionStorage', {
    getItem: (k: string) => mem.get(k) ?? null,
    setItem: (k: string, v: string) => void mem.set(k, v),
    removeItem: (k: string) => void mem.delete(k),
    clear: () => mem.clear(),
  });
}

function makeStore(opts?: { post?: (...args: never[]) => never; get?: (...args: never[]) => never }) {
  const http = {
    post: vi.fn((..._a: unknown[]) => of({}) as never),
    get: vi.fn((..._a: unknown[]) => of({}) as never),
  };
  if (opts?.post) http.post = vi.fn(opts.post as never) as never;
  if (opts?.get) http.get = vi.fn(opts.get as never) as never;
  const router = { navigate: vi.fn(() => Promise.resolve(true)) };
  const injector = Injector.create([
    { provide: HttpClient, useValue: http },
    { provide: Router, useValue: router },
  ]);
  const store = runInInjectionContext(injector, () => new AuthStore());
  return { store, http, router };
}

beforeEach(() => {
  stubSessionStorage();
  vi.useRealTimers();
});

describe('AuthStore.login', () => {
  it('mfaRequired sets challenge and routes to mfa', async () => {
    const { store, router } = makeStore({
      post: () => of({ mfaRequired: true, challengeId: 'ch-1', expiresInSeconds: 120 }) as never,
    });
    await store.login('alex@x.test', 'pw');
    expect(store.mfaChallengeId()).toBe('ch-1');
    expect(store.email()).toBe('alex@x.test');
    expect(store.otpCode()).toBeNull();
    expect(store.mfaExpiresAt()).toBeGreaterThan(Date.now());
    expect(store.isAuthenticated()).toBe(false);
    expect(store.loading()).toBe(false);
    expect(router.navigate).toHaveBeenCalledWith(['/auth/mfa']);
  });

  it('direct tokens authenticate and persist session', async () => {
    const { store, router } = makeStore({
      post: () =>
        of({
          mfaRequired: false,
          accessToken: 'a',
          refreshToken: 'r',
          expiresIn: 3600,
          customerId: 'c-1',
        }) as never,
    });
    store.email.set('alex@x.test');
    await store.login('alex@x.test', 'pw');
    expect(store.accessToken()).toBe('a');
    expect(store.refreshToken()).toBe('r');
    expect(store.customerId()).toBe('c-1');
    expect(store.isAuthenticated()).toBe(true);
    expect(router.navigate).toHaveBeenCalledWith(['/dashboard']);
    expect(mem.get('sb.tokens')).toContain('alex@x.test');
  });

  it('failure surfaces backend detail and clears loading', async () => {
    const { store } = makeStore({
      post: () => throwError(() => ({ error: { detail: 'Bad credentials.' } })) as never,
    });
    await store.login('a@x', 'wrong');
    expect(store.error()).toBe('Bad credentials.');
    expect(store.loading()).toBe(false);
    expect(store.isAuthenticated()).toBe(false);
  });

  it('failure without detail falls back to generic message', async () => {
    const { store } = makeStore({ post: () => throwError(() => ({})) as never });
    await store.login('a@x', 'wrong');
    expect(store.error()).toBe('Login failed.');
  });
});

describe('AuthStore.verifyMfa / resendMfa / fetchOtp', () => {
  it('verifyMfa success sets tokens and routes to dashboard', async () => {
    const { store, router } = makeStore({
      post: () =>
        of({ accessToken: 'a', refreshToken: 'r', expiresIn: 60, customerId: 'c' }) as never,
    });
    store.mfaChallengeId.set('ch-1');
    await store.verifyMfa('123456');
    expect(store.accessToken()).toBe('a');
    expect(store.mfaChallengeId()).toBeNull();
    expect(router.navigate).toHaveBeenCalledWith(['/dashboard']);
  });

  it('verifyMfa failure keeps challenge and shows Invalid code.', async () => {
    const { store } = makeStore({ post: () => throwError(() => ({ error: {} })) as never });
    store.mfaChallengeId.set('ch-1');
    await store.verifyMfa('000000');
    expect(store.error()).toBe('Invalid code.');
    expect(store.mfaChallengeId()).toBe('ch-1');
  });

  it('resendMfa refreshes expiry and clears stale otp', async () => {
    const { store } = makeStore({ post: () => of({ expiresInSeconds: 90 }) as never });
    store.otpCode.set('stale');
    await store.resendMfa();
    expect(store.otpCode()).toBeNull();
    expect(store.mfaExpiresAt()).toBeGreaterThan(Date.now());
    expect(store.error()).toBeNull();
  });

  it('resendMfa failure sets error and rethrows', async () => {
    const boom = { error: { detail: 'Too many.' } };
    const { store } = makeStore({ post: () => throwError(() => boom) as never });
    await expect(store.resendMfa()).rejects.toBe(boom);
    expect(store.error()).toBe('Too many.');
  });

  it('fetchOtp returns null without email', async () => {
    const { store, http } = makeStore();
    expect(await store.fetchOtp()).toBeNull();
    expect(http.get).not.toHaveBeenCalled();
  });

  it('fetchOtp stores code on success, clears on error', async () => {
    const ok = makeStore({ get: () => of({ code: '999999' }) as never });
    ok.store.email.set('a@x');
    expect(await ok.store.fetchOtp()).toBe('999999');
    expect(ok.store.otpCode()).toBe('999999');

    const bad = makeStore({ get: () => throwError(() => new Error('down')) as never });
    bad.store.email.set('a@x');
    expect(await bad.store.fetchOtp()).toBeNull();
    expect(bad.store.otpCode()).toBeNull();
  });
});

describe('AuthStore.refresh / logout / hydrate', () => {
  it('refresh success rotates tokens', async () => {
    const { store } = makeStore({
      post: () => of({ accessToken: 'n', refreshToken: 'nr', expiresIn: 60 }) as never,
    });
    store.refreshToken.set('old');
    await store.refresh();
    expect(store.accessToken()).toBe('n');
    expect(store.refreshToken()).toBe('nr');
  });

  it('concurrent refresh shares one backend call', async () => {
    const post = vi.fn(() => of({ accessToken: 'n', refreshToken: 'nr', expiresIn: 60 }).pipe(delay(5)) as never);
    const { store } = makeStore({ post: post as never });
    store.refreshToken.set('old');
    await Promise.all([store.refresh(), store.refresh()]);
    expect(post).toHaveBeenCalledTimes(1);
  });

  it('refresh failure logs out', async () => {
    const { store, router } = makeStore({
      post: (url: string) =>
        String(url).includes('/refresh')
          ? (throwError(() => ({ status: 401 })) as never)
          : (of({}) as never),
    });
    store.accessToken.set('stale');
    await store.refresh();
    expect(store.accessToken()).toBeNull();
    expect(router.navigate).toHaveBeenCalledWith(['/auth/login'], { replaceUrl: true });
  });

  it('logout clears state + storage even when backend call fails', async () => {
    const { store, router } = makeStore({ post: () => throwError(() => new Error('down')) as never });
    store.accessToken.set('a');
    store.refreshToken.set('r');
    store.customerId.set('c');
    mem.set('sb.tokens', '{}');
    mem.set('sb.selectedAccountId', 'acc-1');
    await store.logout();
    expect(store.accessToken()).toBeNull();
    expect(store.refreshToken()).toBeNull();
    expect(store.customerId()).toBeNull();
    expect(mem.has('sb.tokens')).toBe(false);
    expect(mem.has('sb.selectedAccountId')).toBe(false);
    expect(router.navigate).toHaveBeenCalledWith(['/auth/login'], { replaceUrl: true });
  });

  it('hydrateFromSession restores tokens, ignores corrupt JSON', () => {
    const { store } = makeStore();
    expect(store.isAuthenticated()).toBe(false);
    store.hydrateFromSession();
    expect(store.isAuthenticated()).toBe(false);

    mem.set(
      'sb.tokens',
      JSON.stringify({ accessToken: 'a', refreshToken: 'r', expiresAt: 1, customerId: 'c', email: 'e@x' })
    );
    store.hydrateFromSession();
    expect(store.accessToken()).toBe('a');
    expect(store.email()).toBe('e@x');

    mem.set('sb.tokens', '{broken');
    expect(() => store.hydrateFromSession()).not.toThrow();
  });
});
