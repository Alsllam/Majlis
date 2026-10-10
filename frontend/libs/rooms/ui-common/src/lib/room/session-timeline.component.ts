import { DatePipe } from '@angular/common';
import { AfterViewChecked, ChangeDetectionStrategy, Component, ElementRef, inject, input, viewChild } from '@angular/core';
import { LocalizationService } from '@majlis/core';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { EnArPipe } from '@majlis/core';
import { AvatarComponent } from '@majlis/shared-ui-common';
import { TimelineItem } from './room-session.facade';

/** Turns and control notices in seq order. Agent text is labelled AI-generated and carries its citations. */
@Component({
  selector: 'majlis-session-timeline',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe, DatePipe, AvatarComponent, RouterLink, EnArPipe],
  templateUrl: './session-timeline.component.html',
  styleUrl: './session-timeline.component.scss',
})
export class SessionTimelineComponent implements AfterViewChecked {
  readonly items = input.required<TimelineItem[]>();
  readonly meId = input.required<string>();
  protected readonly lang = inject(LocalizationService).lang;
  private readonly scroller = viewChild.required<ElementRef<HTMLElement>>('scroller');
  private lastCount = -1;
  private lastLength = -1;

  ngAfterViewChecked(): void {
    const el = this.scroller().nativeElement;
    const items = this.items();
    const last = items[items.length - 1];
    const length = last?.kind === 'turn' ? last.text.length : 0;
    const nearBottom = el.scrollHeight - el.scrollTop - el.clientHeight < 120;
    if ((items.length !== this.lastCount || length !== this.lastLength) && (nearBottom || items.length !== this.lastCount)) {
      el.scrollTop = el.scrollHeight;
    }
    this.lastCount = items.length;
    this.lastLength = length;
  }
}
