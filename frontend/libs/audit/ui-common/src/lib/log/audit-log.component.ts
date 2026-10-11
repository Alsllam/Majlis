import { DatePipe, KeyValuePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';
import { AUDIT_CATEGORIES, AuditEntryDto, AuditService, ChainVerificationDto, FilterAuditDto } from '@majlis/audit-proxy';
import { AUDIT_PERMISSIONS } from '@majlis/audit-config';
import { EnArPipe, LocalizationService, PermissionDirective } from '@majlis/core';
import { EmptyStateComponent } from '@majlis/shared-ui-common';
import { PageHeaderComponent, ToastService } from '@majlis/theme-shared';
import { WorkspaceListDto, WorkspacesService } from '@majlis/workspaces-proxy';

const PAGE = 50;

/** The tenant's audit log (invariant 7): filter, page, verify the hash chain, export CSV. Admins only. */
@Component({
  selector: 'majlis-audit-log',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, RouterLink, TranslatePipe, DatePipe, KeyValuePipe, EnArPipe, PermissionDirective, PageHeaderComponent, EmptyStateComponent],
  templateUrl: './audit-log.component.html',
  styleUrl: './audit-log.component.scss',
})
export class AuditLogComponent {
  private readonly audit = inject(AuditService);
  private readonly workspaces = inject(WorkspacesService);
  private readonly toasts = inject(ToastService);
  private readonly localization = inject(LocalizationService);
  protected readonly lang = this.localization.lang;
  protected readonly permissions = AUDIT_PERMISSIONS;
  protected readonly categories = AUDIT_CATEGORIES;
  protected readonly myWorkspaces = signal<WorkspaceListDto[]>([]);
  protected readonly items = signal<AuditEntryDto[] | null>(null);
  protected readonly total = signal(0);
  protected readonly page = signal(0);
  protected readonly category = signal('');
  protected readonly workspaceId = signal('');
  protected readonly text = signal('');
  protected readonly from = signal('');
  protected readonly to = signal('');
  protected readonly verification = signal<ChainVerificationDto | null>(null);
  protected readonly busy = signal(false);
  protected readonly expanded = signal<string | null>(null);
  protected readonly pages = computed(() => Math.max(1, Math.ceil(this.total() / PAGE)));

  constructor() {
    this.workspaces.getList({ skipCount: 0, maxResultCount: 100 }).subscribe((p) => this.myWorkspaces.set(p.items));
    this.load();
  }

  protected filter(): FilterAuditDto {
    return {
      skipCount: this.page() * PAGE,
      maxResultCount: PAGE,
      category: this.category() || null,
      workspaceId: this.workspaceId() || null,
      filterText: this.text().trim() || undefined,
      from: this.from() ? new Date(this.from()).toISOString() : null,
      to: this.to() ? new Date(this.to() + 'T23:59:59').toISOString() : null,
    };
  }

  protected apply(): void {
    this.page.set(0);
    this.load();
  }

  protected goTo(page: number): void {
    this.page.set(Math.min(Math.max(0, page), this.pages() - 1));
    this.load();
  }

  protected load(): void {
    this.audit.getList(this.filter()).subscribe((result) => {
      this.items.set(result.items);
      this.total.set(result.totalCount);
    });
  }

  protected toggle(id: string): void {
    this.expanded.update((current) => (current === id ? null : id));
  }

  protected async verify(): Promise<void> {
    this.busy.set(true);
    try {
      this.verification.set(await firstValueFrom(this.audit.verify()));
    } finally {
      this.busy.set(false);
    }
  }

  protected async exportCsv(): Promise<void> {
    this.busy.set(true);
    try {
      const blob = await firstValueFrom(this.audit.export({ ...this.filter(), skipCount: 0, maxResultCount: 10000 }));
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `majlis-audit-${new Date().toISOString().slice(0, 10)}.csv`;
      link.click();
      URL.revokeObjectURL(url);
      this.toasts.success(this.localization.instant('Audit.Exported'));
    } finally {
      this.busy.set(false);
    }
  }

  protected workspaceName(id: string | null): string {
    return this.myWorkspaces().find((w) => w.id === id)?.name ?? '';
  }

  protected actionKey(entry: AuditEntryDto): string {
    return `Audit.Actions.${entry.action}`;
  }
}
