import '@angular/compiler';
import { describe, it, expect, vi } from 'vitest';
import { Injector, runInInjectionContext, signal } from '@angular/core';
import { Router } from '@angular/router';
import { authGuard } from './auth.guard';
import { AuthStore } from './auth.store';

function runGuard(authenticated: boolean) {
  const store = { isAuthenticated: signal(authenticated).asReadonly() };
  const router = { navigate: vi.fn(() => Promise.resolve(true)) };
  const injector = Injector.create([
    { provide: AuthStore, useValue: store },
    { provide: Router, useValue: router },
  ]);
  const result = runInInjectionContext(injector, () => authGuard(null as never, null as never));
  return { result, router };
}

describe('authGuard', () => {
  it('allows authenticated users without redirecting', () => {
    const { result, router } = runGuard(true);
    expect(result).toBe(true);
    expect(router.navigate).not.toHaveBeenCalled();
  });

  it('blocks anonymous users and redirects to login with replaceUrl', () => {
    const { result, router } = runGuard(false);
    expect(result).toBe(false);
    expect(router.navigate).toHaveBeenCalledWith(['/auth/login'], { replaceUrl: true });
  });
});
