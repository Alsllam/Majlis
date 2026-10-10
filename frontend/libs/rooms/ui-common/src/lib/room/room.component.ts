import { ChangeDetectionStrategy, Component, effect, inject, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { TranslatePipe } from '@ngx-translate/core';
import { AvatarComponent, BusyButtonDirective, EmptyStateComponent } from '@majlis/shared-ui-common';
import { HandOffModalComponent } from './hand-off-modal.component';
import { RoomSessionFacade } from './room-session.facade';
import { SessionComposerComponent } from './session-composer.component';
import { SessionTimelineComponent } from './session-timeline.component';

/** The core screen: one shared agent session, watched and driven by everyone in the room. */
@Component({
  selector: 'majlis-room',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, TranslatePipe, AvatarComponent, BusyButtonDirective, EmptyStateComponent, SessionTimelineComponent, SessionComposerComponent],
  providers: [RoomSessionFacade],
  templateUrl: './room.component.html',
  styleUrl: './room.component.scss',
})
export class RoomComponent {
  protected readonly facade = inject(RoomSessionFacade);
  private readonly modal = inject(NgbModal);
  readonly roomId = input.required<string>();

  constructor() {
    effect(() => void this.facade.open(this.roomId()));
  }

  protected handOff(): void {
    const ref = this.modal.open(HandOffModalComponent, { centered: true });
    (ref.componentInstance as HandOffModalComponent).colleagues = this.facade.colleagues();
    ref.closed.subscribe((result: { toUserId: string; note: string | null }) => void this.facade.offerHandOff(result.toUserId, result.note));
  }
}
