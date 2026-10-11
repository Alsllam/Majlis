import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { TranslatePipe } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';
import { APPROVALS_PERMISSIONS } from '@majlis/approvals-config';
import { ApprovalRequestDto, ApprovalStatus, ApprovalsService } from '@majlis/approvals-proxy';
import { LocalizationService, PermissionDirective } from '@majlis/core';
import { EmptyStateComponent, ReasonModalComponent } from '@majlis/shared-ui-common';
import { PageHeaderComponent, ToastService } from '@majlis/theme-shared';

type Tab = 'forMe' | 'pending' | 'all';
const KNOWN_TOOLS = new Set(['create_task', 'update_task', 'draft_document']);

/** The approver inbox (FR-APR-009): requests the caller may decide, every pending request, or the full history. */
@Component({
  selector: 'majlis-approvals-inbox',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe, DatePipe, RouterLink, PermissionDirective, PageHeaderComponent, EmptyStateComponent],
  templateUrl: './approvals-inbox.component.html',
  styleUrl: './approvals-inbox.component.scss',
})
export class ApprovalsInboxComponent {
  private readonly approvals = inject(ApprovalsService);
  private readonly modal = inject(NgbModal);
  private readonly toasts = inject(ToastService);
  private readonly localization = inject(LocalizationService);
  protected readonly permissions = APPROVALS_PERMISSIONS;
  protected readonly lang = this.localization.lang;
  protected readonly tab = signal<Tab>('forMe');
  protected readonly items = signal<ApprovalRequestDto[] | null>(null);
  protected readonly busyId = signal<string | null>(null);
  protected readonly tabs: readonly Tab[] = ['forMe', 'pending', 'all'];
  protected readonly pendingCount = computed(() => this.items()?.filter((r) => r.status === 'Pending').length ?? 0);

  constructor() {
    this.load();
  }

  protected select(tab: Tab): void {
    this.tab.set(tab);
    this.load();
  }

  protected load(): void {
    const tab = this.tab();
    const status: ApprovalStatus | null = tab === 'pending' ? 'Pending' : null;
    this.approvals.getList({ forMe: tab === 'forMe', status, skipCount: 0, maxResultCount: 100 }).subscribe((page) => this.items.set(page.items));
  }

  protected async approve(request: ApprovalRequestDto): Promise<void> {
    await this.decide(request.id, () => firstValueFrom(this.approvals.approve({ id: request.id })), 'Approvals.ApprovedToast');
  }

  protected reject(request: ApprovalRequestDto): void {
    const ref = this.modal.open(ReasonModalComponent, { centered: true });
    const modal = ref.componentInstance as ReasonModalComponent;
    modal.title = 'Approvals.Reject';
    modal.label = 'Approvals.RejectReason';
    modal.submitLabel = 'Approvals.Reject';
    modal.required = true;
    ref.closed.subscribe((reason: string | null) => {
      if (reason) {
        void this.decide(request.id, () => firstValueFrom(this.approvals.reject({ id: request.id, reason })), 'Approvals.RejectedToast');
      }
    });
  }

  protected toolKey(tool: string): string {
    return KNOWN_TOOLS.has(tool) ? tool : 'other';
  }

  protected stateClass(status: ApprovalStatus): string {
    switch (status) {
      case 'Pending':
        return 'status-tag-warning';
      case 'Executed':
        return 'status-tag-success';
      case 'Rejected':
      case 'Failed':
        return 'status-tag-danger';
      case 'Approved':
        return 'status-tag-info';
      default:
        return 'status-tag-neutral';
    }
  }

  private async decide(id: string, call: () => Promise<unknown>, toastKey: string): Promise<void> {
    this.busyId.set(id);
    try {
      await call();
      this.toasts.success(this.localization.instant(toastKey));
    } catch {
      // Reported by the global handler (403 when the policy forbids it, 409 when already decided).
    } finally {
      this.busyId.set(null);
      this.load();
    }
  }
}
