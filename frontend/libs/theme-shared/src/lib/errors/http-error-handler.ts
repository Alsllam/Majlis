import { HttpErrorResponse } from '@angular/common/http';
import { DestroyRef, Injectable, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router } from '@angular/router';
import { AuthService, HttpErrorReporterService, LocalizationService, isApiError } from '@majlis/core';
import { ToastService } from '../toast/toast.service';

/** Shows backend `error.messages` as a toast; 401 restarts login, 403 routes to `/403` (skill §7). */
@Injectable({ providedIn: 'root' })
export class HttpErrorHandler {
  private readonly reporter = inject(HttpErrorReporterService);
  private readonly toasts = inject(ToastService);
  private readonly router = inject(Router);
  private readonly auth = inject(AuthService);
  private readonly localization = inject(LocalizationService);
  private readonly destroyRef = inject(DestroyRef);

  start(): void {
    this.reporter.errors$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((error) => this.handle(error));
  }

  handle(error: HttpErrorResponse): void {
    switch (error.status) {
      case 401:
        this.auth.login();
        return;
      case 403:
        void this.router.navigateByUrl('/403');
        return;
      case 0:
        this.toasts.error(this.localization.instant('Errors.Network'));
        return;
    }
    if (isApiError(error.error) && error.error.error.messages.length > 0) {
      this.toasts.error(error.error.error.messages.join('\n'));
      return;
    }
    this.toasts.error(this.localization.instant('Errors.Unexpected'));
  }
}
