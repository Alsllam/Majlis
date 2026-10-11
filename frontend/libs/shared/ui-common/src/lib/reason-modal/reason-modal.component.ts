import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';
import { TranslatePipe } from '@ngx-translate/core';

/**
 * Asks for a reason or a note before a decision (reject with reason, approve with a note). Closes with the trimmed
 * text; `required` makes an empty text invalid. Open it with `NgbModal` and set the inputs on `componentInstance`.
 */
@Component({
  selector: 'majlis-reason-modal',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, TranslatePipe],
  template: `
    <form [formGroup]="form" (ngSubmit)="submit()">
      <div class="modal-header">
        <h2 class="modal-title fs-5">{{ title | translate }}</h2>
        <button type="button" class="btn-close" [attr.aria-label]="'General.Close' | translate" (click)="modal.dismiss()"></button>
      </div>
      <div class="modal-body">
        <label class="form-label" for="reason-text">{{ label | translate }}</label>
        <textarea id="reason-text" class="form-control" rows="3" formControlName="text" [attr.maxlength]="maxLength"></textarea>
      </div>
      <div class="modal-footer">
        <button type="button" class="btn btn-outline-secondary" (click)="modal.dismiss()">{{ 'General.Cancel' | translate }}</button>
        <button type="submit" class="btn" [class]="'btn ' + submitClass" [disabled]="form.invalid">{{ submitLabel | translate }}</button>
      </div>
    </form>
  `,
})
export class ReasonModalComponent {
  protected readonly modal = inject(NgbActiveModal);
  private readonly fb = inject(FormBuilder);
  title = 'General.Decline';
  label = 'General.Reason';
  submitLabel = 'General.Decline';
  submitClass = 'btn-danger';
  maxLength = 1000;
  protected readonly form = this.fb.nonNullable.group({ text: [''] });

  set required(value: boolean) {
    this.form.controls.text.setValidators(value ? [Validators.required, noBlank] : []);
    this.form.controls.text.updateValueAndValidity();
  }

  protected submit(): void {
    if (this.form.invalid) {
      return;
    }
    this.modal.close(this.form.getRawValue().text.trim() || null);
  }
}

function noBlank(control: { value: string }): { blank: true } | null {
  return control.value.trim() ? null : { blank: true };
}
