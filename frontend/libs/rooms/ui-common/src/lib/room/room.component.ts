import { ChangeDetectionStrategy, Component, effect, inject, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { TranslatePipe } from '@ngx-translate/core';
import { AvatarComponent, BusyButtonDirective, EmptyStateComponent, ReasonModalComponent } from '@majlis/shared-ui-common';
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

  /** FR-APR-002: a rejection always carries a reason, so the agent and the room know why. */
  protected rejectApproval(requestId: string): void {
    const ref = this.modal.open(ReasonModalComponent, { centered: true });
    const modal = ref.componentInstance as ReasonModalComponent;
    modal.title = 'Approvals.Reject';
    modal.label = 'Approvals.RejectReason';
    modal.submitLabel = 'Approvals.Reject';
    modal.required = true;
    ref.closed.subscribe((reason: string | null) => {
      if (reason) {
        void this.facade.reject(requestId, reason);
      }
    });
  }

  protected handOff(): void {
    const ref = this.modal.open(HandOffModalComponent, { centered: true });
    (ref.componentInstance as HandOffModalComponent).colleagues = this.facade.colleagues();
    ref.closed.subscribe((result: { toUserId: string; note: string | null }) => void this.facade.offerHandOff(result.toUserId, result.note));
  }
}
