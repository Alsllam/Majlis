import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, ElementRef, computed, effect, inject, input, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';
import { EnArPipe, LocalizationService, PermissionDirective } from '@majlis/core';
import { KNOWLEDGE_PERMISSIONS } from '@majlis/knowledge-config';
import { DOCUMENT_TYPES, DocumentListDto, DocumentType, DocumentsService } from '@majlis/knowledge-proxy';
import { EmptyStateComponent } from '@majlis/shared-ui-common';
import { PageHeaderComponent, ToastService } from '@majlis/theme-shared';
import { WorkspaceListDto, WorkspacesService } from '@majlis/workspaces-proxy';
import { UploadService } from './upload.service';

const LIVE_STATUSES = new Set(['Pending', 'Queued', 'Processing']);

/** Documents of one workspace with upload, live ingestion status (polled while something is in flight), download, delete. */
@Component({
  selector: 'majlis-documents-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, RouterLink, TranslatePipe, DatePipe, EnArPipe, PermissionDirective, PageHeaderComponent, EmptyStateComponent],
  providers: [UploadService],
  templateUrl: './documents-list.component.html',
  styleUrl: './documents-list.component.scss',
})
export class DocumentsListComponent {
  private readonly documents = inject(DocumentsService);
  private readonly workspaces = inject(WorkspacesService);
  private readonly toasts = inject(ToastService);
  private readonly localization = inject(LocalizationService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly uploads = inject(UploadService);
  protected readonly permissions = KNOWLEDGE_PERMISSIONS;
  protected readonly types = DOCUMENT_TYPES;
  protected readonly lang = this.localization.lang;
  private readonly fileInput = viewChild<ElementRef<HTMLInputElement>>('fileInput');
  private pollTimer: ReturnType<typeof setTimeout> | null = null;

  /** `?workspaceId=`; otherwise the first workspace of the user. */
  readonly workspaceId = input<string | undefined>();
  protected readonly myWorkspaces = signal<WorkspaceListDto[] | null>(null);
  protected readonly selectedWorkspace = signal<string>('');
  protected readonly items = signal<DocumentListDto[] | null>(null);
  protected readonly uploadType = signal<DocumentType>('Other');
  protected readonly canUpload = computed(() => {
    const ws = this.myWorkspaces()?.find((w) => w.id === this.selectedWorkspace());
    return !!ws && !ws.isArchived && ws.myRole !== 'Viewer';
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
    this.destroyRef.onDestroy(() => this.stopPolling());
  }

  protected selectWorkspace(id: string): void {
    this.selectedWorkspace.set(id);
    void this.router.navigate([], { queryParams: { workspaceId: id }, replaceUrl: true });
    this.load();
  }

  protected load(): void {
    const workspaceId = this.selectedWorkspace();
    if (!workspaceId) {
      return;
    }
    this.documents.getList({ workspaceId, skipCount: 0, maxResultCount: 100 }).subscribe((page) => {
      this.items.set(page.items);
      this.schedulePolling(page.items.some((d) => LIVE_STATUSES.has(d.status)));
    });
  }

  protected pickFiles(): void {
    this.fileInput()?.nativeElement.click();
  }

  protected async onFiles(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const files = Array.from(input.files ?? []);
    input.value = '';
    const workspaceId = this.selectedWorkspace();
    for (const file of files) {
      const id = await this.uploads.upload(file, workspaceId, this.uploadType());
      if (id) {
        this.toasts.success(this.localization.instant('Documents.Uploaded', { name: file.name }));
      }
    }
    this.load();
  }

  protected async download(doc: DocumentListDto): Promise<void> {
    const detail = await firstValueFrom(this.documents.get(doc.id));
    const version = detail.versions[0];
    if (!version) {
      return;
    }
    const link = await firstValueFrom(this.documents.getDownloadUrl(version.id));
    window.open(link.url, '_blank', 'noopener');
  }

  protected async remove(doc: DocumentListDto): Promise<void> {
    await firstValueFrom(this.documents.delete(doc.id));
    this.toasts.success(this.localization.instant('Documents.Deleted'));
    this.load();
  }

  private schedulePolling(active: boolean): void {
    this.stopPolling();
    if (active) {
      this.pollTimer = setTimeout(() => this.load(), 3000);
    }
  }

  private stopPolling(): void {
    if (this.pollTimer) {
      clearTimeout(this.pollTimer);
      this.pollTimer = null;
    }
  }
}
