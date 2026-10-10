import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

/** Illustration (the Majlis room mark) + message + call to action (skill §0.4). */
@Component({
  selector: 'majlis-empty-state',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe],
  template: `
    <div class="empty">
      <svg viewBox="0 0 64 64" width="72" height="72" aria-hidden="true">
        <path d="M14 10h36a4 4 0 0 1 4 4v36a4 4 0 0 1-4 4H40" fill="none" stroke="var(--brand-solid)" stroke-width="5" stroke-linecap="round" />
        <path d="M24 54H14a4 4 0 0 1-4-4V14" fill="none" stroke="var(--brand-solid)" stroke-width="5" stroke-linecap="round" />
        <circle cx="32" cy="32" r="6" fill="var(--agent)" />
      </svg>
      <h2>{{ title() | translate }}</h2>
      @if (text()) {
        <p>{{ text() | translate }}</p>
      }
      <ng-content />
    </div>
  `,
  styles: `
    .empty { display: grid; justify-items: center; text-align: center; gap: var(--space-2); padding: var(--space-7) var(--space-4); color: var(--text-secondary); }
    h2 { font-size: var(--fs-lg); margin: var(--space-2) 0 0; color: var(--text-primary); }
    p { margin: 0; max-width: 46ch; }
  `,
})
export class EmptyStateComponent {
  readonly title = input.required<string>();
  readonly text = input<string | null>(null);
}
