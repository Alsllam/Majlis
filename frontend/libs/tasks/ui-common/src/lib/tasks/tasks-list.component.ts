import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';
import { LocalizationService, PermissionDirective, PermissionService } from '@majlis/core';
import { EmptyStateComponent } from '@majlis/shared-ui-common';
import { TASKS_PERMISSIONS } from '@majlis/tasks-config';
import { TASK_PRIORITIES, TASK_STATUSES, TaskDto, TaskPriority, TaskStatus, TasksService } from '@majlis/tasks-proxy';
import { PageHeaderComponent, ToastService } from '@majlis/theme-shared';
import { WorkspaceListDto, WorkspacesService } from '@majlis/workspaces-proxy';

/** Tasks of one workspace (FR-TSK-001/002): created by people here, or by the agent after approval; status changes inline. */
@Component({
  selector: 'majlis-tasks-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, RouterLink, TranslatePipe, DatePipe, PermissionDirective, PageHeaderComponent, EmptyStateComponent],
  templateUrl: './tasks-list.component.html',
  styleUrl: './tasks-list.component.scss',
})
export class TasksListComponent {
  private readonly tasks = inject(TasksService);
  private readonly workspaces = inject(WorkspacesService);
  private readonly toasts = inject(ToastService);
  private readonly localization = inject(LocalizationService);
  private readonly router = inject(Router);
  private readonly permissionService = inject(PermissionService);
  protected readonly permissions = TASKS_PERMISSIONS;
  protected readonly statuses = TASK_STATUSES;
  protected readonly priorities = TASK_PRIORITIES;
  protected readonly lang = this.localization.lang;

  /** `?workspaceId=`; otherwise the first workspace of the user. */
  readonly workspaceId = input<string | undefined>();
  protected readonly myWorkspaces = signal<WorkspaceListDto[] | null>(null);
  protected readonly selectedWorkspace = signal<string>('');
  protected readonly items = signal<TaskDto[] | null>(null);
  protected readonly statusFilter = signal<TaskStatus | ''>('');
  protected readonly newTitle = signal('');
  protected readonly newPriority = signal<TaskPriority>('Normal');
  protected readonly canContribute = computed(() => {
    const ws = this.myWorkspaces()?.find((w) => w.id === this.selectedWorkspace());
    return !!ws && !ws.isArchived && ws.myRole !== 'Viewer';
  });
  protected readonly canUpdate = computed(() => this.canContribute() && this.permissionService.isGranted(TASKS_PERMISSIONS.updateTask));

  constructor() {
    this.workspaces.getList({ skipCount: 0, maxResultCount: 100 }).subscribe((page) => {
      this.myWorkspaces.set(page.items);
      const wanted = this.workspaceId();
      const first = page.items.find((w) => w.id === wanted) ?? page.items[0];
      if (first) {
        this.selectWorkspace(first.id);
      } else {
        this.items.set([]);
      }
    });
    effect(() => {
      const wanted = this.workspaceId();
      if (wanted && this.myWorkspaces()?.some((w) => w.id === wanted) && wanted !== this.selectedWorkspace()) {
        this.selectWorkspace(wanted);
      }
    });
  }

  protected selectWorkspace(id: string): void {
    this.selectedWorkspace.set(id);
    void this.router.navigate([], { queryParams: { workspaceId: id }, replaceUrl: true });
    this.load();
  }

  protected filterStatus(status: TaskStatus | ''): void {
    this.statusFilter.set(status);
    this.load();
  }

  protected load(): void {
    const workspaceId = this.selectedWorkspace();
    if (!workspaceId) {
      return;
    }
    this.tasks.getList({ workspaceId, status: this.statusFilter() || null, skipCount: 0, maxResultCount: 100 }).subscribe((page) => this.items.set(page.items));
  }

  protected async create(): Promise<void> {
    const title = this.newTitle().trim();
    if (!title) {
      return;
    }
    await firstValueFrom(this.tasks.create({ workspaceId: this.selectedWorkspace(), title, priority: this.newPriority() }));
    this.newTitle.set('');
    this.toasts.success(this.localization.instant('Tasks.Created'));
    this.load();
  }

  protected async setStatus(task: TaskDto, status: TaskStatus): Promise<void> {
    if (status === task.status) {
      return;
    }
    await firstValueFrom(this.tasks.setStatus({ id: task.id, status }));
    this.load();
  }

  protected statusClass(status: TaskStatus): string {
    switch (status) {
      case 'Done':
        return 'status-tag-success';
      case 'InProgress':
        return 'status-tag-info';
      case 'Cancelled':
        return 'status-tag-neutral';
      default:
        return 'status-tag-warning';
    }
  }
}
