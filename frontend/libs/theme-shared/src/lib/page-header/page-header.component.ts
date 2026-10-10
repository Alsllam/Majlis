import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

/** Title, optional subtitle and a slot for the primary action (skill §0.5). */
@Component({
  selector: 'majlis-page-header',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe],
  template: `
    <header class="page-header">
      <div class="titles">
        <h1>{{ title() | translate }}</h1>
        @if (subtitle()) {
          <p class="subtitle">{{ subtitle() | translate }}</p>
        }
      </div>
      <div class="actions"><ng-content /></div>
    </header>
  `,
  styles: `
    .page-header { display: flex; align-items: flex-start; justify-content: space-between; gap: var(--space-4); margin-block-end: var(--space-5); flex-wrap: wrap; }
    h1 { font-size: var(--fs-2xl); margin: 0; }
    .subtitle { margin: var(--space-1) 0 0; color: var(--text-muted); font-size: var(--fs-sm); }
    .actions { display: flex; gap: var(--space-2); align-items: center; }
  `,
})
export class PageHeaderComponent {
  readonly title = input.required<string>();
  readonly subtitle = input<string | null>(null);
}
