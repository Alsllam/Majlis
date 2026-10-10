import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

/** Initial-letter avatar. `kind="agent"` uses the brass agent color (brand README: brass = the agent). */
@Component({
  selector: 'majlis-avatar',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<span class="avatar" [class.agent]="kind() === 'agent'" [class.driver]="driver()" [attr.title]="name()">{{ initial() }}</span>`,
  styles: `
    .avatar { width: var(--avatar-size, 32px); height: var(--avatar-size, 32px); border-radius: 50%; background: var(--brand-solid); color: var(--text-on-brand); display: inline-grid; place-items: center; font-weight: var(--fw-semibold); font-size: var(--fs-sm); flex: none; box-sizing: border-box; }
    .agent { background: var(--agent); color: var(--text-on-accent); }
    .driver { outline: 2px solid var(--driver); outline-offset: 2px; }
  `,
})
export class AvatarComponent {
  readonly name = input.required<string>();
  readonly kind = input<'user' | 'agent'>('user');
  readonly driver = input(false);
  protected readonly initial = computed(() => (this.kind() === 'agent' ? '✦' : this.name().trim().slice(0, 1) || '?'));
}
