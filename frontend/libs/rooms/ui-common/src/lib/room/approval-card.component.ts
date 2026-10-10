import { DatePipe, KeyValuePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, input, output } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { LocalizationService } from '@majlis/core';
import { BusyButtonDirective } from '@majlis/shared-ui-common';
import { ApprovalItem } from './room-session.facade';

const KNOWN_TOOLS = new Set(['create_task', 'update_task', 'draft_document']);

/** An agent action waiting for a person (FR-APR-004): what it does, the arguments, who asked, and its outcome. */
@Component({
  selector: 'majlis-approval-card',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe, DatePipe, KeyValuePipe, BusyButtonDirective],
  template: `
    @let item = this.item();
    <article class="card-surface approval" [class]="'card-surface approval state-' + item.state" [attr.data-request-id]="item.requestId">
      <header>
        <span class="status-tag status-tag-agent">{{ 'Approvals.Proposed' | translate }}</span>
        <span class="tool">{{ 'Approvals.Tools.' + toolKey(item.tool) | translate }}</span>
        <span class="status-tag" [class]="'status-tag ' + stateClass(item.state)">{{ 'Approvals.States.' + item.state | translate }}</span>
        <time class="num">{{ item.at | date: 'shortTime' : undefined : lang() }}</time>
      </header>
      <p class="summary">{{ item.summary }}</p>
      @if (item.reason) {
        <p class="reason">{{ item.reason }}</p>
      }
      <dl class="args">
        @for (arg of item.args | keyvalue; track arg.key) {
          <div><dt>{{ 'Approvals.Args.' + arg.key | translate }}</dt><dd>{{ arg.value }}</dd></div>
        }
      </dl>
      <footer>
        <span class="who">{{ 'Approvals.RequestedBy' | translate: { name: item.requestedBy.displayName } }}</span>
        @switch (item.state) {
          @case ('pending') {
            @if (canApprove()) {
              <span class="actions">
                <button type="button" class="btn btn-sm btn-primary" (click)="approve.emit(item.requestId)" [majlisBusy]="busy()">{{ 'Approvals.Approve' | translate }}</button>
                <button type="button" class="btn btn-sm btn-outline-danger" (click)="reject.emit(item.requestId)" [disabled]="busy()">{{ 'Approvals.Reject' | translate }}</button>
              </span>
            } @else {
              <span class="muted">{{ 'Approvals.WaitingForApprover' | translate }}</span>
            }
          }
          @case ('approved') { <span class="muted">{{ 'Approvals.ApprovedBy' | translate: { name: item.decidedBy?.displayName ?? '' } }} · {{ 'Approvals.Executing' | translate }}</span> }
          @case ('rejected') { <span class="muted">{{ 'Approvals.RejectedBy' | translate: { name: item.decidedBy?.displayName ?? '' } }}@if (item.note) { : «{{ item.note }}» }</span> }
          @case ('expired') { <span class="muted">{{ 'Approvals.ExpiredText' | translate }}</span> }
          @case ('executed') { <span class="done">{{ 'Approvals.Done' | translate }}@if (item.resultSummary) { : {{ item.resultSummary }} }</span> }
          @case ('failed') { <span class="failed">{{ 'Approvals.FailedText' | translate }}@if (item.reasonKey) { : {{ 'Approvals.Reasons.' + item.reasonKey | translate }} }</span> }
        }
      </footer>
    </article>
  `,
  styles: `
    .approval { padding: var(--space-3); border-inline-start: 3px solid var(--accent-solid, var(--agent)); display: grid; gap: var(--space-2); max-width: 72ch; animation: rise var(--motion-base) var(--ease-out); }
    .state-executed { border-inline-start-color: var(--success-solid); }
    .state-rejected, .state-failed { border-inline-start-color: var(--danger-solid); }
    .state-expired { opacity: .75; }
    header { display: flex; flex-wrap: wrap; align-items: center; gap: var(--space-2); font-size: var(--fs-xs); color: var(--text-muted); }
    .tool { font-weight: var(--fw-medium); color: var(--text-secondary); }
    time { margin-inline-start: auto; }
    p { margin: 0; }
    .summary { font-weight: var(--fw-medium); }
    .reason { font-size: var(--fs-sm); color: var(--text-secondary); }
    .args { display: grid; gap: var(--space-1); margin: 0; font-size: var(--fs-sm);
      div { display: flex; gap: var(--space-2); } dt { color: var(--text-muted); min-width: 7em; font-weight: var(--fw-regular); } dd { margin: 0; overflow-wrap: anywhere; } }
    footer { display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: var(--space-2); font-size: var(--fs-xs); color: var(--text-muted); }
    .actions { display: flex; gap: var(--space-1); }
    .done { color: var(--success-text); }
    .failed { color: var(--danger-text); }
    @keyframes rise { from { opacity: 0; transform: translateY(var(--motion-distance-enter)); } to { opacity: 1; transform: none; } }
  `,
})
export class ApprovalCardComponent {
  readonly item = input.required<ApprovalItem>();
  readonly canApprove = input(false);
  readonly busy = input(false);
  readonly approve = output<string>();
  readonly reject = output<string>();
  protected readonly lang = inject(LocalizationService).lang;

  protected toolKey(tool: string): string {
    return KNOWN_TOOLS.has(tool) ? tool : 'other';
  }

  protected stateClass(state: ApprovalItem['state']): string {
    switch (state) {
      case 'pending':
        return 'status-tag-warning';
      case 'executed':
        return 'status-tag-success';
      case 'rejected':
      case 'failed':
        return 'status-tag-danger';
      case 'approved':
        return 'status-tag-info';
      default:
        return 'status-tag-neutral';
    }
  }
}
