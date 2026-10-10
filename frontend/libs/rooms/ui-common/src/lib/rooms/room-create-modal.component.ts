import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';
import { TranslatePipe } from '@ngx-translate/core';
import { RoomVisibility, RoomsService } from '@majlis/rooms-proxy';
import { BusyButtonDirective } from '@majlis/shared-ui-common';

export const MAX_ROOM_NAME_LENGTH = 120;

@Component({
  selector: 'majlis-room-create-modal',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, TranslatePipe, BusyButtonDirective],
  template: `
    <form [formGroup]="form" (ngSubmit)="submit()">
      <div class="modal-header">
        <h2 class="modal-title fs-5">{{ 'Rooms.Create' | translate }}</h2>
        <button type="button" class="btn-close" [attr.aria-label]="'General.Close' | translate" (click)="modal.dismiss()"></button>
      </div>
      <div class="modal-body d-grid gap-3">
        <div>
          <label class="form-label" for="room-name">{{ 'Rooms.Name' | translate }} <span class="text-danger">*</span></label>
          <input id="room-name" class="form-control" formControlName="name" [maxlength]="maxName" autocomplete="off" />
          @if (form.controls.name.touched && form.controls.name.invalid) {
            <div class="form-text text-danger">{{ 'Rooms.NameRequired' | translate }}</div>
          }
        </div>
        <div>
          <label class="form-label" for="room-purpose">{{ 'Rooms.Purpose' | translate }}</label>
          <textarea id="room-purpose" class="form-control" rows="2" formControlName="purpose"></textarea>
        </div>
        <div>
          <label class="form-label" for="room-visibility">{{ 'Rooms.Visibility' | translate }}</label>
          <select id="room-visibility" class="form-select" formControlName="visibility">
            <option value="Private">{{ 'Rooms.VisibilityPrivate' | translate }}</option>
            <option value="Open">{{ 'Rooms.VisibilityOpen' | translate }}</option>
          </select>
        </div>
      </div>
      <div class="modal-footer">
        <button type="button" class="btn btn-outline-secondary" (click)="modal.dismiss()">{{ 'General.Cancel' | translate }}</button>
        <button type="submit" class="btn btn-primary" [majlisBusy]="busy()">{{ 'General.Create' | translate }}</button>
      </div>
    </form>
  `,
})
export class RoomCreateModalComponent {
  protected readonly modal = inject(NgbActiveModal);
  private readonly rooms = inject(RoomsService);
  private readonly fb = inject(FormBuilder);
  protected readonly maxName = MAX_ROOM_NAME_LENGTH;
  protected readonly busy = signal(false);
  workspaceId = '';

  protected readonly form = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(MAX_ROOM_NAME_LENGTH)]],
    purpose: [''],
    visibility: ['Private' as RoomVisibility],
  });

  protected submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const value = this.form.getRawValue();
    this.busy.set(true);
    this.rooms
      .create({ workspaceId: this.workspaceId, name: value.name.trim(), purpose: value.purpose.trim() || null, visibility: value.visibility })
      .subscribe({ next: (id) => this.modal.close(id), error: () => this.busy.set(false) });
  }
}
