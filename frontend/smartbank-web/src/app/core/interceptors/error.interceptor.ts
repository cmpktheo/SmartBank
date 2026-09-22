import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { ToastService } from '../../shared/ui/toast';

export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const toast = inject(ToastService);
  const router = inject(Router);
  return next(req).pipe(
    catchError((err: HttpErrorResponse) => {
      if (err.status === 0 || err.status >= 500) toast.show('Service unavailable. Try again.');
      else if (err.status === 400 || err.status === 409) toast.show(err.error?.detail ?? 'Request failed.');
      else if (err.status === 401) router.navigate(['/auth/login'], { replaceUrl: true });
      else if (err.status === 403) toast.show('You do not have permission to do that.');
      return throwError(() => err);
    })
  );
};
