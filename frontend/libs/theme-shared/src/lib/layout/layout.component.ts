import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { NgbDropdownModule } from '@ng-bootstrap/ng-bootstrap';
import { TranslatePipe } from '@ngx-translate/core';
import { AuthService, LANGUAGES, LocalizationService, RoutesService, THEMES, ThemeService } from '@majlis/core';
import { ToastsComponent } from '../toast/toasts.component';

/** Sidebar + top bar. Content sits on `--surface-sunken`; cards on `--surface` (skill §0.5). */
@Component({
  selector: 'majlis-layout',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterOutlet, RouterLink, RouterLinkActive, NgbDropdownModule, TranslatePipe, ToastsComponent],
  templateUrl: './layout.component.html',
  styleUrl: './layout.component.scss',
})
export class LayoutComponent {
  protected readonly auth = inject(AuthService);
  protected readonly localization = inject(LocalizationService);
  protected readonly theme = inject(ThemeService);
  protected readonly routes = inject(RoutesService);
  protected readonly languages = LANGUAGES;
  protected readonly themes = THEMES;
  protected readonly collapsed = signal(false);
  protected readonly logo = computed(() => {
    const dark = this.theme.theme() !== 'light' ? '-dark' : '';
    return `/brand/logo-${this.localization.lang()}${dark}.svg`;
  });
  protected readonly initials = computed(() => (this.auth.user()?.name ?? '?').slice(0, 1));

  toggle(): void {
    this.collapsed.update((v) => !v);
  }
}
