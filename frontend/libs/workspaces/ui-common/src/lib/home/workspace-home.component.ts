import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, effect, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';
import { ApprovalRequestDto, ApprovalsService } from '@majlis/approvals-proxy';
import { AuthService, EnArPipe, LocalizationService, PermissionService } from '@majlis/core';
import { DocumentListDto, DocumentsService } from '@majlis/knowledge-proxy';
import { RoomListDto, RoomsService } from '@majlis/rooms-proxy';
import { EmptyStateComponent } from '@majlis/shared-ui-common';
import { TaskDto, TasksService } from '@majlis/tasks-proxy';
import { PageHeaderComponent } from '@majlis/theme-shared';
import { WorkspaceListDto, WorkspacesService } from '@majlis/workspaces-proxy';

const LAST_WORKSPACE_KEY = 'majlis.workspace';
const REFRESH_MS = 30000;

/**
 * The workspace home (SRS §4.2 "workspace home"): what needs the person now, in one place. Live rooms first,
 * approvals waiting for them, their open tasks and the latest documents, for the workspace they last used.
 * Each block reads from its own module's proxy; nothing is aggregated server-side yet.
 */
@Component({
  selector: 'majlis-workspace-home',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, RouterLink, TranslatePipe, DatePipe, EnArPipe, PageHeaderComponent, EmptyStateComponent],
  templateUrl: './workspace-home.component.html',
  styleUrl: './workspace-home.component.scss',
})
export class WorkspaceHomeComponent {
  private readonly workspaces = inject(WorkspacesService);
  private readonly rooms = inject(RoomsService);
  private readonly approvals = inject(ApprovalsService);
  private readonly tasks = inject(TasksService);
  private readonly documents = inject(DocumentsService);
  private readonly permissions = inject(PermissionService);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly lang = inject(LocalizationService).lang;
  private timer: ReturnType<typeof setInterval> | null = null;

  /** `?workspaceId=`; otherwise the last one used, otherwise the first. */
  readonly workspaceId = input<string | undefined>();
  protected readonly myWorkspaces = signal<WorkspaceListDto[] | null>(null);
  protected readonly selected = signal<string>('');
  protected readonly workspace = computed(() => this.myWorkspaces()?.find((w) => w.id === this.selected()) ?? null);
  protected readonly rows = signal<RoomListDto[] | null>(null);
  protected readonly pending = signal<ApprovalRequestDto[] | null>(null);
  protected readonly myTasks = signal<TaskDto[] | null>(null);
  protected readonly latestDocuments = signal<DocumentListDto[] | null>(null);
  protected readonly liveRooms = computed(() => (this.rows() ?? []).filter((r) => r.activeSessionId));
  protected readonly firstName = computed(() => (this.auth.user()?.name ?? '').split(' ')[0]);
  protected readonly canSeeApprovals = computed(() => this.permissions.isGranted('Permissions.Approvals.ViewApproval'));
  protected readonly canSeeTasks = computed(() => this.permissions.isGranted('Permissions.Tasks.ViewTask'));
  protected readonly canSeeDocuments = computed(() => this.permissions.isGranted('Permissions.Knowledge.ViewDocument'));

  constructor() {
    this.workspaces.getList({ skipCount: 0, maxResultCount: 100 }).subscribe((page) => {
      const active = page.items.filter((w) => !w.isArchived);
      this.myWorkspaces.set(active);
      const wanted = this.workspaceId() ?? this.remembered();
      const first = active.find((w) => w.id === wanted) ?? active[0];
      if (first) {
        this.select(first.id);
      } else {
        this.rows.set([]);
      }
    });
    effect(() => {
      const wanted = this.workspaceId();
      if (wanted && this.myWorkspaces()?.some((w) => w.id === wanted) && wanted !== this.selected()) {
        this.select(wanted);
      }
    });
    this.timer = setInterval(() => void this.load(), REFRESH_MS);
    this.destroyRef.onDestroy(() => {
      if (this.timer) {
        clearInterval(this.timer);
      }
    });
  }

  protected select(id: string): void {
    this.selected.set(id);
    try {
      localStorage.setItem(LAST_WORKSPACE_KEY, id);
    } catch {
      // Private mode: nothing remembered.
    }
    void this.router.navigate([], { queryParams: { workspaceId: id }, replaceUrl: true });
    void this.load();
  }

  protected async load(): Promise<void> {
    const workspaceId = this.selected();
    if (!workspaceId) {
      return;
    }
    const safe = async <T>(run: () => Promise<T>, fallback: T): Promise<T> => {
      try {
        return await run();
      } catch {
        return fallback; // reported by the global handler; the other blocks still load
      }
    };
    const [rooms, pending, tasks, documents] = await Promise.all([
      safe(() => firstValueFrom(this.rooms.getList({ workspaceId, skipCount: 0, maxResultCount: 50 })), { items: [], totalCount: 0 }),
      this.canSeeApprovals() ? safe(() => firstValueFrom(this.approvals.getList({ workspaceId, forMe: true, skipCount: 0, maxResultCount: 10 })), { items: [], totalCount: 0 }) : Promise.resolve(null),
      this.canSeeTasks() ? safe(() => firstValueFrom(this.tasks.getList({ workspaceId, assignedToMe: true, skipCount: 0, maxResultCount: 50 })), { items: [], totalCount: 0 }) : Promise.resolve(null),
      this.canSeeDocuments() ? safe(() => firstValueFrom(this.documents.getList({ workspaceId, skipCount: 0, maxResultCount: 6 })), { items: [], totalCount: 0 }) : Promise.resolve(null),
    ]);
    if (workspaceId !== this.selected()) {
      return; // the person switched meanwhile
    }
    this.rows.set([...rooms.items].sort((a, b) => Number(!!b.activeSessionId) - Number(!!a.activeSessionId)).slice(0, 6));
    this.pending.set(pending ? pending.items.slice(0, 6) : null);
    this.myTasks.set(tasks ? tasks.items.filter((t) => t.status === 'ToDo' || t.status === 'InProgress').slice(0, 6) : null);
    this.latestDocuments.set(documents?.items ?? null);
  }

  private remembered(): string | null {
    try {
      return localStorage.getItem(LAST_WORKSPACE_KEY);
    } catch {
      return null;
    }
  }
}
