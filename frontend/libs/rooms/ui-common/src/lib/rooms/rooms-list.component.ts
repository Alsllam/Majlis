import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { TranslatePipe } from '@ngx-translate/core';
import { EnArPipe, LocalizationService, PermissionDirective } from '@majlis/core';
import { ROOMS_PERMISSIONS } from '@majlis/rooms-config';
import { RoomListDto, RoomsService } from '@majlis/rooms-proxy';
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
  private readonly modal = inject(NgbModal);
  private readonly toasts = inject(ToastService);
  private readonly localization = inject(LocalizationService);
  protected readonly permissions = ROOMS_PERMISSIONS;
  protected readonly items = signal<RoomListDto[] | null>(null);
  protected readonly skeleton = [1, 2, 3];
  protected readonly lang = this.localization.lang;

  constructor() {
    this.load();
  }

  protected load(): void {
    this.rooms.getList({ skipCount: 0, maxResultCount: 50 }).subscribe((page) => this.items.set(page.items));
  }

  protected create(): void {
    const workspaceId = this.items()?.[0]?.workspaceId;
    if (!workspaceId) {
      return;
    }
    const ref = this.modal.open(RoomCreateModalComponent, { centered: true });
    (ref.componentInstance as RoomCreateModalComponent).workspaceId = workspaceId;
    ref.closed.subscribe(() => {
      this.toasts.success(this.localization.instant('Rooms.Created'));
      this.load();
    });
  }
}
