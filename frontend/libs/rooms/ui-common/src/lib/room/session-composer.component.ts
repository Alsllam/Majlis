import { ChangeDetectionStrategy, Component, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { BusyButtonDirective } from '@majlis/shared-ui-common';

/** The driver writes here; everyone else sees who is driving. Enter sends, Shift+Enter breaks the line. */
@Component({
  selector: 'majlis-session-composer',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, TranslatePipe, BusyButtonDirective],
  template: `
    <div class="composer">
      @if (isDriver()) {
        <textarea
          class="form-control"
          rows="2"
          [placeholder]="'Session.Composer' | translate"
          [(ngModel)]="text"
          [disabled]="!canInstruct()"
          (keydown.enter)="onEnter($event)"
          [attr.aria-label]="'Session.Composer' | translate"></textarea>
        <div class="row-actions">
          <span class="hint">{{ (running() ? 'Session.TurnRunning' : 'Session.ComposerHint') | translate }}</span>
          @if (running()) {
            <button type="button" class="btn btn-outline-danger btn-sm" (click)="stop.emit()" [majlisBusy]="busy()">■ {{ 'General.Stop' | translate }}</button>
          } @else {
            <button type="button" class="btn btn-primary btn-sm" (click)="submit()" [disabled]="!canInstruct() || !text().trim()" [majlisBusy]="busy()">{{ 'General.Send' | translate }}</button>
          }
        </div>
      } @else {
        <p class="hint viewer">
          @if (driverName(); as name) { {{ 'Session.ViewerHint' | translate: { name } }} } @else { {{ 'Session.FreeHint' | translate }} }
          @if (running()) { · {{ 'Session.TurnRunning' | translate }} }
        </p>
      }
    </div>
  `,
  styles: `
    .composer { border-top: 1px solid var(--border-subtle); padding: var(--space-3) var(--space-4); display: grid; gap: var(--space-2); background: var(--surface); }
    textarea { resize: none; }
    .row-actions { display: flex; justify-content: space-between; align-items: center; gap: var(--space-2); }
    .hint { font-size: var(--fs-xs); color: var(--text-muted); margin: 0; }
    .viewer { font-size: var(--fs-sm); padding: var(--space-2) 0; }
  `,
})
export class SessionComposerComponent {
  readonly canInstruct = input.required<boolean>();
  readonly running = input.required<boolean>();
  readonly isDriver = input.required<boolean>();
  readonly driverName = input<string | null>(null);
  readonly busy = input(false);
  readonly send = output<string>();
  readonly stop = output<void>();
  protected readonly text = signal('');

  protected onEnter(event: Event): void {
    if ((event as KeyboardEvent).shiftKey) {
      return;
    }
    event.preventDefault();
    this.submit();
  }

  protected submit(): void {
    const value = this.text().trim();
    if (!value || !this.canInstruct()) {
      return;
    }
    this.send.emit(value);
    this.text.set('');
  }
}
