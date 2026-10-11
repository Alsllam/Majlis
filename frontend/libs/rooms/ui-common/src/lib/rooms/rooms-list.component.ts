import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { TranslatePipe } from '@ngx-translate/core';
import { EnArPipe, LocalizationService, PermissionDirective } from '@majlis/core';
import { ROOMS_PERMISSIONS } from '@majlis/rooms-config';
import { RoomListDto, RoomsService } from '@majlis/rooms-proxy';
import { WorkspaceListDto, WorkspacesService } from '@majlis/workspaces-proxy';
import { EmptyStateComponent } from '@majlis/shared-ui-common';
import { PageHeaderComponent, ToastService } from '@majlis/theme-shared';
import { RoomCreateModalComponent } from './room-create-modal.component';

/** Rooms the user takes part in, as cards (the list engine comes with the first table screen). */
@Component({
  selector: 'majlis-rooms-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, TranslatePipe, DatePipe, EnArPipe, PermissionDirective, PageHeaderComponent, EmptyStateComponent],
  templateUrl: './rooms-list.component.html',
  styleUrl: './rooms-list.component.scss',
})
export class RoomsListComponent {
  private readonly rooms = inject(RoomsService);
  private readonly workspaces = inject(WorkspacesService);
  private readonly modal = inject(NgbModal);
  /** Optional `?workspaceId=` filter (from the workspace page). */
  readonly workspaceId = input<string | undefined>();
  protected readonly myWorkspaces = signal<WorkspaceListDto[] | null>(null);
  protected readonly canCreate = computed(() => (this.myWorkspaces()?.some((w) => !w.isArchived && w.myRole !== 'Viewer') ?? false));
  private readonly toasts = inject(ToastService);
  private readonly localization = inject(LocalizationService);
  protected readonly permissions = ROOMS_PERMISSIONS;
  protected readonly items = signal<RoomListDto[] | null>(null);
  protected readonly skeleton = [1, 2, 3];
  protected readonly lang = this.localization.lang;

  constructor() {
    this.workspaces.getList({ skipCount: 0, maxResultCount: 100 }).subscribe((page) => this.myWorkspaces.set(page.items));
    effect(() => this.load(this.workspaceId()));
  }

  protected load(workspaceId = this.workspaceId()): void {
    this.items.set(null);
    this.rooms.getList({ skipCount: 0, maxResultCount: 50, workspaceId }).subscribe((page) => this.items.set(page.items));
  }

  protected workspaceName(id: string): string {
    return this.myWorkspaces()?.find((w) => w.id === id)?.name ?? '';
  }

  protected create(): void {
    const options = (this.myWorkspaces() ?? []).filter((w) => !w.isArchived && w.myRole !== 'Viewer');
    if (options.length === 0) {
      return;
    }
    const ref = this.modal.open(RoomCreateModalComponent, { centered: true });
    const instance = ref.componentInstance as RoomCreateModalComponent;
    instance.workspaces = options;
    instance.preselect(this.workspaceId() ?? options[0]?.id ?? '');
    ref.closed.subscribe(() => {
      this.toasts.success(this.localization.instant('Rooms.Created'));
      this.load();
    });
  }
}
