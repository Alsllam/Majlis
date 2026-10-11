import { HttpEventType } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { DocumentType, DocumentsService, contentTypeOf } from '@majlis/knowledge-proxy';

export interface UploadJob {
  id: number;
  name: string;
  progress: number;
  state: 'uploading' | 'confirming' | 'done' | 'error';
  errorKey?: string;
  documentId?: string;
}

/** Two-step upload (begin → PUT to storage with progress → confirm), one job per file; screen-scoped. */
@Injectable()
export class UploadService {
  private readonly documents = inject(DocumentsService);
  private next = 1;
  readonly jobs = signal<UploadJob[]>([]);

  async upload(file: File, workspaceId: string, type: DocumentType, roomId?: string | null): Promise<string | null> {
    const contentType = contentTypeOf(file);
    const job: UploadJob = { id: this.next++, name: file.name, progress: 0, state: 'uploading' };
    this.jobs.update((all) => [job, ...all]);
    if (!contentType) {
      this.patch(job.id, { state: 'error', errorKey: 'Documents.TypeNotSupported' });
      return null;
    }
    try {
      const ticket = await firstValueFrom(
        this.documents.beginUpload({ workspaceId, roomId, fileName: file.name, contentType, sizeBytes: file.size, type }),
      );
      await new Promise<void>((resolve, reject) => {
        this.documents.putToStorage(ticket, file).subscribe({
          next: (event) => {
            if (event.type === HttpEventType.UploadProgress && event.total) {
              this.patch(job.id, { progress: Math.round((event.loaded / event.total) * 100) });
            }
          },
          error: reject,
          complete: resolve,
        });
      });
      this.patch(job.id, { state: 'confirming', progress: 100 });
      await firstValueFrom(this.documents.confirmUpload(ticket.versionId));
      this.patch(job.id, { state: 'done', documentId: ticket.documentId });
      return ticket.documentId;
    } catch {
      this.patch(job.id, { state: 'error', errorKey: 'Documents.UploadFailed' });
      return null;
    }
  }

  dismiss(id: number): void {
    this.jobs.update((all) => all.filter((j) => j.id !== id));
  }

  private patch(id: number, changes: Partial<UploadJob>): void {
    this.jobs.update((all) => all.map((j) => (j.id === id ? { ...j, ...changes } : j)));
  }
}
