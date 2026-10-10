import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { ToastService } from './toast.service';

/** Slides in from the top end and auto-dismisses with a progress bar (skill §0.4). */
@Component({
  selector: 'majlis-toasts',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe],
  template: `
    <div class="toasts" aria-live="polite">
      @for (toast of toasts.toasts(); track toast.id) {
        <div class="toast-item" [class]="'toast-item toast-' + toast.severity" role="status">
          <span class="text">{{ toast.text }}</span>
          <button type="button" class="close" [attr.aria-label]="'General.Close' | translate" (click)="toasts.dismiss(toast.id)">×</button>
          <span class="bar" [style.animation-duration.ms]="toast.durationMs"></span>
        </div>
      }
    </div>
  `,
  styles: `
    .toasts { position: fixed; inset-block-start: var(--space-4); inset-inline-end: var(--space-4); z-index: 1080; display: grid; gap: var(--space-2); width: min(360px, calc(100vw - 32px)); }
    .toast-item { position: relative; overflow: hidden; display: flex; align-items: flex-start; gap: var(--space-2); padding: var(--space-3) var(--space-4); border-radius: var(--radius-md); box-shadow: var(--shadow-md); background: var(--surface-overlay); color: var(--text-primary); border-inline-start: 4px solid var(--info-solid); animation: toast-in var(--motion-base) var(--ease-out); }
    .toast-success { border-color: var(--success-solid); } .toast-warning { border-color: var(--warning-solid); } .toast-danger { border-color: var(--danger-solid); }
    .text { flex: 1; font-size: var(--fs-sm); }
    .close { border: 0; background: none; color: var(--text-muted); font-size: var(--fs-lg); line-height: 1; cursor: pointer; }
    .bar { position: absolute; inset-block-end: 0; inset-inline-start: 0; height: 2px; width: 100%; background: var(--brand-solid); transform-origin: var(--bar-origin, left); animation: toast-bar linear forwards; }
    :host-context([dir='rtl']) .bar { --bar-origin: right; }
    @keyframes toast-in { from { opacity: 0; transform: translateY(calc(-1 * var(--motion-distance-enter))); } to { opacity: 1; transform: none; } }
    @keyframes toast-bar { from { transform: scaleX(1); } to { transform: scaleX(0); } }
  `,
})
export class ToastsComponent {
  protected readonly toasts = inject(ToastService);
}
