import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';
import { LocalizationService } from '@majlis/core';
import { NOTIFICATION_TYPES, NotificationSettingsDto, NotificationsService } from '@majlis/notifications-proxy';
import { PageHeaderComponent, ToastService } from '@majlis/theme-shared';

/** Per-user preferences (FR-NTF-002): email on or off, and which kinds to mute entirely. */
@Component({
  selector: 'majlis-notification-settings',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, TranslatePipe, RouterLink, PageHeaderComponent],
  template: `
    <majlis-page-header title="Notifications.Settings" subtitle="Notifications.SettingsSubtitle">
      <a routerLink="/notifications" class="btn btn-outline-secondary btn-sm">{{ 'Notifications.Back' | translate }}</a>
    </majlis-page-header>

    @if (settings(); as s) {
      <form class="card-surface form" (ngSubmit)="save()">
        <div class="form-check form-switch">
          <input class="form-check-input" type="checkbox" id="email-enabled" name="emailEnabled" [ngModel]="s.emailEnabled" (ngModelChange)="set({ emailEnabled: $event })" />
          <label class="form-check-label" for="email-enabled">{{ 'Notifications.EmailEnabled' | translate }}</label>
        </div>
        <p class="hint">{{ 'Notifications.EmailHint' | translate }}</p>
        <fieldset>
          <legend>{{ 'Notifications.Kinds.Title' | translate }}</legend>
          @for (t of types; track t) {
            <div class="form-check">
              <input class="form-check-input" type="checkbox" [id]="'kind-' + t" [name]="t" [checked]="!s.mutedTypes.includes(t)" (change)="toggle(t, $event)" />
              <label class="form-check-label" [for]="'kind-' + t">{{ 'Notifications.Kinds.' + t | translate }}</label>
            </div>
          }
        </fieldset>
        <div>
          <button type="submit" class="btn btn-primary" [disabled]="saving()">{{ 'General.Save' | translate }}</button>
        </div>
      </form>
    } @else {
      <p class="text-secondary">{{ 'General.Loading' | translate }}</p>
    }
  `,
  styles: `
    .form { padding: var(--space-4); display: grid; gap: var(--space-3); max-width: 560px; }
    .hint { margin: 0; font-size: var(--fs-sm); color: var(--text-muted); }
    legend { font-size: var(--fs-md); font-weight: var(--fw-medium); margin-bottom: var(--space-2); }
  `,
})
export class NotificationSettingsComponent {
  private readonly api = inject(NotificationsService);
  private readonly toasts = inject(ToastService);
  private readonly localization = inject(LocalizationService);
  protected readonly types = NOTIFICATION_TYPES;
  protected readonly settings = signal<NotificationSettingsDto | null>(null);
  protected readonly saving = signal(false);

  constructor() {
    this.api.getSettings().subscribe((s) => this.settings.set(s));
  }

  protected set(patch: Partial<NotificationSettingsDto>): void {
    this.settings.update((s) => (s ? { ...s, ...patch } : s));
  }

  protected toggle(type: string, event: Event): void {
    const enabled = (event.target as HTMLInputElement).checked;
    this.settings.update((s) => (s ? { ...s, mutedTypes: enabled ? s.mutedTypes.filter((t) => t !== type) : [...s.mutedTypes, type] } : s));
  }

  protected async save(): Promise<void> {
    const current = this.settings();
    if (!current) {
      return;
    }
    this.saving.set(true);
    try {
      this.settings.set(await firstValueFrom(this.api.updateSettings(current)));
      this.toasts.success(this.localization.instant('Notifications.Saved'));
    } finally {
      this.saving.set(false);
    }
  }
}
