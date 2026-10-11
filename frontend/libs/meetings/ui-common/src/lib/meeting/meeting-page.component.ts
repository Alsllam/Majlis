import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, effect, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';
import { LocalizationService, PermissionDirective, PermissionService } from '@majlis/core';
import { MEETINGS_PERMISSIONS } from '@majlis/meetings-config';
import { ActionItemInputDto, MeetingDto, MeetingsService } from '@majlis/meetings-proxy';
import { BusyButtonDirective } from '@majlis/shared-ui-common';
import { PageHeaderComponent, ToastService } from '@majlis/theme-shared';
import { WorkspacesService } from '@majlis/workspaces-proxy';

const POLL_MS = 3000;

interface ActionItemRow extends ActionItemInputDto {
  key: string;
  approvalRequestId: string | null;
  taskId: string | null;
}

/**
 * One meeting (FR-MTG-002..005): the transcript, the minutes draft the agent produced, the editor until a person
 * approves, then the action items go to Approvals in bulk, the minutes to the knowledge, and a markdown export.
 */
@Component({
  selector: 'majlis-meeting-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, RouterLink, TranslatePipe, DatePipe, PermissionDirective, BusyButtonDirective, PageHeaderComponent],
  templateUrl: './meeting-page.component.html',
  styleUrl: './meeting-page.component.scss',
})
export class MeetingPageComponent {
  private readonly meetings = inject(MeetingsService);
  private readonly workspaces = inject(WorkspacesService);
  private readonly toasts = inject(ToastService);
  private readonly localization = inject(LocalizationService);
  private readonly permissionService = inject(PermissionService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly permissions = MEETINGS_PERMISSIONS;
  protected readonly lang = this.localization.lang;

  readonly id = input.required<string>();
  protected readonly meeting = signal<MeetingDto | null>(null);
  protected readonly canContribute = signal(false);
  protected readonly busy = signal<string | null>(null);
  protected readonly showTranscript = signal(false);

  // The editor (draft minutes only).
  protected readonly summary = signal('');
  protected readonly decisions = signal('');
  protected readonly openQuestions = signal('');
  protected readonly items = signal<ActionItemRow[]>([]);
  protected readonly dirty = signal(false);

  protected readonly minutes = computed(() => this.meeting()?.minutes ?? null);
  protected readonly isDraft = computed(() => this.minutes()?.status === 'Draft');
  protected readonly isApproved = computed(() => this.minutes()?.status === 'Approved');
  protected readonly working = computed(() => this.meeting()?.status === 'Transcribing' || this.meeting()?.status === 'Summarizing');
  protected readonly canManage = computed(() => this.canContribute() && this.permissionService.isGranted(MEETINGS_PERMISSIONS.manageMeeting));
  protected readonly canGenerate = computed(() => this.canManage() && !!this.meeting()?.transcriptText && !this.working() && !this.isApproved());
  protected readonly pendingItems = computed(() => this.minutes()?.actionItems.filter((i) => !i.approvalRequestId).length ?? 0);
  protected readonly linkedItems = computed(() => this.minutes()?.actionItems.filter((i) => !!i.approvalRequestId).length ?? 0);
  private pollTimer: ReturnType<typeof setTimeout> | null = null;

  constructor() {
    effect(() => {
      const id = this.id();
      void this.load(id);
    });
    this.destroyRef.onDestroy(() => this.stopPolling());
  }

  private async load(id: string): Promise<void> {
    const meeting = await firstValueFrom(this.meetings.get(id));
    this.apply(meeting);
    const page = await firstValueFrom(this.workspaces.getList({ skipCount: 0, maxResultCount: 100 }));
    const ws = page.items.find((w) => w.id === meeting.workspaceId);
    this.canContribute.set(!!ws && !ws.isArchived && ws.myRole !== 'Viewer');
  }

  /** Replaces the meeting and resets the editor unless a person is in the middle of an edit. */
  private apply(meeting: MeetingDto): void {
    const previous = this.meeting();
    this.meeting.set(meeting);
    const minutesChanged = previous?.minutes?.generatedAt !== meeting.minutes?.generatedAt || previous?.minutes?.status !== meeting.minutes?.status;
    if (meeting.minutes && (minutesChanged || !this.dirty())) {
      this.summary.set(meeting.minutes.summary);
      this.decisions.set(meeting.minutes.decisions.join('\n'));
      this.openQuestions.set(meeting.minutes.openQuestions.join('\n'));
      this.items.set(meeting.minutes.actionItems.map((i) => ({ key: i.id, id: i.id, text: i.text, ownerName: i.ownerName, dueDate: i.dueDate, approvalRequestId: i.approvalRequestId, taskId: i.taskId })));
      this.dirty.set(false);
    }
    this.stopPolling();
    if (meeting.status === 'Transcribing' || meeting.status === 'Summarizing') {
      this.pollTimer = setTimeout(() => void this.refresh(), POLL_MS);
    }
  }

  private async refresh(): Promise<void> {
    this.apply(await firstValueFrom(this.meetings.get(this.id())));
  }

  private stopPolling(): void {
    if (this.pollTimer) {
      clearTimeout(this.pollTimer);
      this.pollTimer = null;
    }
  }

  protected touch(): void {
    this.dirty.set(true);
  }

  protected addItem(): void {
    this.items.update((rows) => [...rows, { key: `new-${Date.now()}`, id: null, text: '', ownerName: null, dueDate: null, approvalRequestId: null, taskId: null }]);
    this.touch();
  }

  protected removeItem(row: ActionItemRow): void {
    this.items.update((rows) => rows.filter((r) => r !== row));
    this.touch();
  }

  protected updateItem(row: ActionItemRow, patch: Partial<ActionItemInputDto>): void {
    this.items.update((rows) => rows.map((r) => (r === row ? { ...r, ...patch } : r)));
    this.touch();
  }

  protected async generate(): Promise<void> {
    await this.run('generate', async () => {
      this.apply(await firstValueFrom(this.meetings.generateMinutes(this.id())));
      this.toasts.show(this.localization.instant('Meetings.Generating'), 'info');
    });
  }

  protected async save(): Promise<boolean> {
    return this.run('save', async () => {
      this.apply(
        await firstValueFrom(
          this.meetings.updateMinutes({
            meetingId: this.id(),
            summary: this.summary(),
            decisions: this.lines(this.decisions()),
            openQuestions: this.lines(this.openQuestions()),
            actionItems: this.items().filter((i) => i.text.trim()).map((i) => ({ id: i.id, text: i.text.trim(), ownerName: i.ownerName?.trim() || null, dueDate: i.dueDate || null })),
          }),
        ),
      );
      this.dirty.set(false);
      this.toasts.success(this.localization.instant('Meetings.Saved'));
    });
  }

  protected async approve(): Promise<void> {
    if (this.dirty() && !(await this.save())) {
      return;
    }
    await this.run('approve', async () => {
      this.apply(await firstValueFrom(this.meetings.approveMinutes(this.id())));
      this.toasts.success(this.localization.instant('Meetings.Approved'));
    });
  }

  protected async propose(): Promise<void> {
    await this.run('propose', async () => {
      const result = await firstValueFrom(this.meetings.proposeActionItems(this.id()));
      await this.refresh();
      this.toasts.success(this.localization.instant('Meetings.Proposed', { count: result.proposed }));
    });
  }

  protected async publish(): Promise<void> {
    await this.run('publish', async () => {
      this.apply(await firstValueFrom(this.meetings.publishMinutes(this.id())));
      this.toasts.success(this.localization.instant('Meetings.Published'));
    });
  }

  protected async exportMarkdown(): Promise<void> {
    await this.run('export', async () => {
      const blob = await firstValueFrom(this.meetings.exportMinutes(this.id()));
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `minutes-${(this.meeting()?.heldAt ?? '').slice(0, 10).replace(/-/g, '')}.md`;
      link.click();
      URL.revokeObjectURL(url);
    });
  }

  protected async remove(): Promise<void> {
    await this.run('delete', async () => {
      await firstValueFrom(this.meetings.delete(this.id()));
      this.toasts.success(this.localization.instant('Meetings.Deleted'));
      await this.router.navigate(['/meetings'], { queryParams: { workspaceId: this.meeting()?.workspaceId } });
    });
  }

  private async run(name: string, action: () => Promise<void>): Promise<boolean> {
    if (this.busy()) {
      return false;
    }
    this.busy.set(name);
    try {
      await action();
      return true;
    } catch {
      return false; // reported by the global handler
    } finally {
      this.busy.set(null);
    }
  }

  private lines(text: string): string[] {
    return text.split('\n').map((l) => l.replace(/^[-•]\s*/, '').trim()).filter(Boolean);
  }
}
