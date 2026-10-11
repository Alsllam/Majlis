import { DatePipe } from '@angular/common';
import { HttpEventType } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';
import { LocalizationService, PermissionDirective } from '@majlis/core';
import { MEETINGS_PERMISSIONS } from '@majlis/meetings-config';
import { MEETING_SOURCES, MeetingListDto, MeetingSource, MeetingsService, RECORDING_CONTENT_TYPES } from '@majlis/meetings-proxy';
import { RoomListDto, RoomsService } from '@majlis/rooms-proxy';
import { EmptyStateComponent } from '@majlis/shared-ui-common';
import { PageHeaderComponent, ToastService } from '@majlis/theme-shared';
import { WorkspaceListDto, WorkspacesService } from '@majlis/workspaces-proxy';

const MAX_RECORDING_MB = 500;

/** Meetings of one workspace (FR-MTG-001): list, and a form that creates one from notes, a transcript file or a recording. */
@Component({
  selector: 'majlis-meetings-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, RouterLink, TranslatePipe, DatePipe, PermissionDirective, PageHeaderComponent, EmptyStateComponent],
  templateUrl: './meetings-list.component.html',
  styleUrl: './meetings-list.component.scss',
})
export class MeetingsListComponent {
  private readonly meetings = inject(MeetingsService);
  private readonly rooms = inject(RoomsService);
  private readonly workspaces = inject(WorkspacesService);
  private readonly toasts = inject(ToastService);
  private readonly localization = inject(LocalizationService);
  private readonly router = inject(Router);
  protected readonly permissions = MEETINGS_PERMISSIONS;
  protected readonly sources = MEETING_SOURCES;
  protected readonly lang = this.localization.lang;
  protected readonly recordingAccept = Object.values(RECORDING_CONTENT_TYPES).join(',');

  /** `?workspaceId=`; otherwise the last / first workspace of the user. */
  readonly workspaceId = input<string | undefined>();
  protected readonly myWorkspaces = signal<WorkspaceListDto[] | null>(null);
  protected readonly selectedWorkspace = signal<string>('');
  protected readonly myRooms = signal<RoomListDto[]>([]);
  protected readonly items = signal<MeetingListDto[] | null>(null);
  protected readonly creating = signal(false);
  protected readonly busy = signal(false);
  protected readonly progress = signal<number | null>(null);

  // The create form.
  protected readonly title = signal('');
  protected readonly heldAt = signal(new Date().toISOString().slice(0, 10));
  protected readonly roomId = signal('');
  protected readonly language = signal<'ar' | 'en'>('ar');
  protected readonly attendees = signal('');
  protected readonly source = signal<MeetingSource>('Notes');
  protected readonly text = signal('');
  protected readonly file = signal<File | null>(null);
  protected readonly fileError = signal<string | null>(null);

  protected readonly canContribute = computed(() => {
    const ws = this.myWorkspaces()?.find((w) => w.id === this.selectedWorkspace());
    return !!ws && !ws.isArchived && ws.myRole !== 'Viewer';
  });
  protected readonly canSubmit = computed(() => {
    if (!this.title().trim() || !this.roomId() || !this.heldAt()) {
      return false;
    }
    return this.source() === 'Notes' ? !!this.text().trim() : !!this.file() && !this.fileError();
  });

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
    effect(() => {
      // Switching the source drops a file that no longer fits it.
      this.source();
      this.file.set(null);
      this.fileError.set(null);
    });
  }

  protected selectWorkspace(id: string): void {
    this.selectedWorkspace.set(id);
    void this.router.navigate([], { queryParams: { workspaceId: id }, replaceUrl: true });
    this.rooms.getList({ workspaceId: id, skipCount: 0, maxResultCount: 100 }).subscribe((page) => {
      this.myRooms.set(page.items);
      if (!page.items.some((r) => r.id === this.roomId())) {
        this.roomId.set(page.items[0]?.id ?? '');
      }
    });
    this.load();
  }

  protected load(): void {
    const workspaceId = this.selectedWorkspace();
    if (!workspaceId) {
      return;
    }
    this.meetings.getList({ workspaceId, skipCount: 0, maxResultCount: 100 }).subscribe((page) => this.items.set(page.items));
  }

  protected pickFile(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0] ?? null;
    this.file.set(file);
    this.fileError.set(null);
    if (!file) {
      return;
    }
    if (this.source() === 'Recording') {
      if (!RECORDING_CONTENT_TYPES[file.type]) {
        this.fileError.set('Meetings.Errors.RecordingType');
      } else if (file.size > MAX_RECORDING_MB * 1024 * 1024) {
        this.fileError.set('Meetings.Errors.RecordingTooLarge');
      }
    } else if (file.size > 2 * 1024 * 1024) {
      this.fileError.set('Meetings.Errors.TranscriptTooLarge');
    }
  }

  protected async create(): Promise<void> {
    if (!this.canSubmit() || this.busy()) {
      return;
    }
    this.busy.set(true);
    try {
      const source = this.source();
      const file = this.file();
      const text = source === 'Notes' ? this.text() : source === 'Transcript' && file ? await file.text() : null;
      const id = await firstValueFrom(
        this.meetings.create({
          workspaceId: this.selectedWorkspace(),
          roomId: this.roomId(),
          title: this.title().trim(),
          heldAt: new Date(this.heldAt() + 'T09:00:00Z').toISOString(),
          language: this.language(),
          attendees: this.attendees().split(/[,،\n]/).map((a) => a.trim()).filter(Boolean),
          source,
          text,
        }),
      );
      if (source === 'Recording' && file) {
        await this.uploadRecording(id, file);
      }
      this.toasts.success(this.localization.instant('Meetings.Created'));
      await this.router.navigate(['/meetings', id]);
    } finally {
      this.busy.set(false);
      this.progress.set(null);
    }
  }

  /** Two steps like documents: a pre-signed url, the PUT to storage, then the confirmation that starts transcription. */
  private async uploadRecording(meetingId: string, file: File): Promise<void> {
    const ticket = await firstValueFrom(this.meetings.beginRecordingUpload({ meetingId, fileName: file.name, contentType: file.type, sizeBytes: file.size }));
    this.progress.set(0);
    await new Promise<void>((resolve, reject) => {
      this.meetings.putToStorage(ticket, file).subscribe({
        next: (event) => {
          if (event.type === HttpEventType.UploadProgress && event.total) {
            this.progress.set(Math.round((100 * event.loaded) / event.total));
          }
        },
        error: reject,
        complete: resolve,
      });
    });
    await firstValueFrom(this.meetings.confirmRecording(meetingId));
  }

  protected statusClass(row: MeetingListDto): string {
    if (row.status === 'Failed') {
      return 'status-tag-danger';
    }
    if (row.minutesStatus === 'Approved') {
      return 'status-tag-success';
    }
    if (row.status === 'MinutesReady') {
      return 'status-tag-info';
    }
    return 'status-tag-warning';
  }
}
