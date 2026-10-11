import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { TranslatePipe } from '@ngx-translate/core';
import { EnArPipe, LocalizationService, PermissionDirective } from '@majlis/core';
import { EmptyStateComponent } from '@majlis/shared-ui-common';
import { PageHeaderComponent, ToastService } from '@majlis/theme-shared';
import { WORKSPACES_PERMISSIONS } from '@majlis/workspaces-config';
import { WorkspaceListDto, WorkspacesService } from '@majlis/workspaces-proxy';
import { WorkspaceFormModalComponent } from './workspace-form-modal.component';

/** The workspaces the user belongs to, as cards with icon, color and role. */
@Component({
  selector: 'majlis-workspaces-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, TranslatePipe, EnArPipe, PermissionDirective, PageHeaderComponent, EmptyStateComponent],
  templateUrl: './workspaces-list.component.html',
  styleUrl: './workspaces-list.component.scss',
})
export class WorkspacesListComponent {
  private readonly workspaces = inject(WorkspacesService);
  private readonly modal = inject(NgbModal);
  private readonly toasts = inject(ToastService);
  private readonly localization = inject(LocalizationService);
  protected readonly permissions = WORKSPACES_PERMISSIONS;
  protected readonly items = signal<WorkspaceListDto[] | null>(null);
  protected readonly includeArchived = signal(false);
  protected readonly skeleton = [1, 2, 3];

  constructor() {
    this.load();
  }

  protected load(): void {
    this.items.set(null);
    this.workspaces.getList({ skipCount: 0, maxResultCount: 50, includeArchived: this.includeArchived() }).subscribe((page) => this.items.set(page.items));
  }

  protected toggleArchived(): void {
    this.includeArchived.update((v) => !v);
    this.load();
  }

  protected create(): void {
    const ref = this.modal.open(WorkspaceFormModalComponent, { centered: true });
    ref.closed.subscribe(() => {
      this.toasts.success(this.localization.instant('Workspaces.Created'));
      this.load();
    });
  }
}
