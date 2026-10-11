import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';
import { TranslatePipe } from '@ngx-translate/core';
import { RoomParticipantDto } from '@majlis/rooms-proxy';

@Component({
  selector: 'majlis-hand-off-modal',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, TranslatePipe],
  template: `
    <form [formGroup]="form" (ngSubmit)="submit()">
      <div class="modal-header">
        <h2 class="modal-title fs-5">{{ 'Control.HandOff' | translate }}</h2>
        <button type="button" class="btn-close" [attr.aria-label]="'General.Close' | translate" (click)="modal.dismiss()"></button>
      </div>
      <div class="modal-body d-grid gap-3">
        <div>
          <label class="form-label" for="handoff-to">{{ 'Control.HandOffTo' | translate }}</label>
          <select id="handoff-to" class="form-select" formControlName="toUserId">
            @for (c of colleagues; track c.userId) {
              <option [value]="c.userId">{{ c.displayName }}</option>
            }
          </select>
        </div>
        <div>
          <label class="form-label" for="handoff-note">{{ 'Control.HandOffNote' | translate }}</label>
          <textarea id="handoff-note" class="form-control" rows="2" formControlName="note"></textarea>
        </div>
      </div>
      <div class="modal-footer">
        <button type="button" class="btn btn-outline-secondary" (click)="modal.dismiss()">{{ 'General.Cancel' | translate }}</button>
        <button type="submit" class="btn btn-primary" [disabled]="form.invalid">{{ 'Control.HandOff' | translate }}</button>
      </div>
    </form>
  `,
})
export class HandOffModalComponent {
  protected readonly modal = inject(NgbActiveModal);
  private readonly fb = inject(FormBuilder);
  colleagues: RoomParticipantDto[] = [];
  protected readonly form = this.fb.nonNullable.group({ toUserId: ['', Validators.required], note: [''] });

  protected submit(): void {
    if (this.form.invalid) {
      return;
    }
    const value = this.form.getRawValue();
    this.modal.close({ toUserId: value.toUserId, note: value.note.trim() || null });
  }
}
