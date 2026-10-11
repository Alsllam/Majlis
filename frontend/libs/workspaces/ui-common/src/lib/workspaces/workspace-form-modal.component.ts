import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';
import { TranslatePipe } from '@ngx-translate/core';
import { BusyButtonDirective } from '@majlis/shared-ui-common';
import { WORKSPACE_COLORS, WorkspaceColor, WorkspaceDto, WorkspacesService } from '@majlis/workspaces-proxy';

export const MAX_WORKSPACE_NAME_LENGTH = 120;
export const WORKSPACE_ICONS = ['▦', '⚖️', '📁', '🏗️', '💼', '🧾', '🛠️', '📊', '🏛️', '🤝'];

/** Create (no `workspace`) or edit (with `workspace`) name, description, icon and color. */
@Component({
  selector: 'majlis-workspace-form-modal',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, TranslatePipe, BusyButtonDirective],
  templateUrl: './workspace-form-modal.component.html',
  styles: `
    .icons, .colors { display: flex; flex-wrap: wrap; gap: var(--space-2); }
    .pick { border: 2px solid var(--border-subtle); background: var(--surface); border-radius: var(--radius-md); width: 40px; height: 40px; display: grid; place-items: center; font-size: var(--fs-xl); cursor: pointer;
      &.active { border-color: var(--brand-solid); background: var(--brand-subtle); }
      &:focus-visible { outline: 2px solid var(--focus-ring); outline-offset: 2px; }
    }
    .swatch { width: 22px; height: 22px; border-radius: 50%; }
  `,
})
export class WorkspaceFormModalComponent {
  protected readonly modal = inject(NgbActiveModal);
  private readonly workspaces = inject(WorkspacesService);
  private readonly fb = inject(FormBuilder);
  protected readonly maxName = MAX_WORKSPACE_NAME_LENGTH;
  protected readonly icons = WORKSPACE_ICONS;
  protected readonly colors = WORKSPACE_COLORS;
  protected readonly busy = signal(false);
  workspace: WorkspaceDto | null = null;

  protected readonly form = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(MAX_WORKSPACE_NAME_LENGTH)]],
    description: [''],
    icon: ['▦'],
    color: ['brand' as WorkspaceColor],
  });

  /** Called by the opener after setting `workspace` (edit mode). */
  init(workspace: WorkspaceDto | null): void {
    this.workspace = workspace;
    if (workspace) {
      this.form.patchValue({ name: workspace.name, description: workspace.description ?? '', icon: workspace.icon ?? '▦', color: workspace.color ?? 'brand' });
    }
  }

  protected submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    const body = { name: v.name.trim(), description: v.description.trim() || null, icon: v.icon || null, color: v.color };
    this.busy.set(true);
    if (this.workspace) {
      const id = this.workspace.id;
      this.workspaces.update({ id, ...body }).subscribe({ next: () => this.modal.close(id), error: () => this.busy.set(false) });
    } else {
      this.workspaces.create(body).subscribe({ next: (id) => this.modal.close(id), error: () => this.busy.set(false) });
    }
  }
}
