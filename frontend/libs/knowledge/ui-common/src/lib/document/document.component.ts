import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, effect, inject, input, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';
import { EnArPipe, LocalizationService, PermissionDirective } from '@majlis/core';
import { KNOWLEDGE_PERMISSIONS } from '@majlis/knowledge-config';
import { DocumentDto, DocumentsService } from '@majlis/knowledge-proxy';
import { ToastService } from '@majlis/theme-shared';
import { MarkdownBlock, markdownBlocks } from './markdown-blocks';

/**
 * One document: metadata, versions and download. A citation opens it with `?page=` and `?passage=` so the cited
 * passage is shown at the top (the in-page viewer with highlighting comes with the PDF viewer, FR-KNW-006).
 */
@Component({
  selector: 'majlis-document',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, TranslatePipe, DatePipe, EnArPipe, PermissionDirective],
  templateUrl: './document.component.html',
  styleUrl: './document.component.scss',
})
export class DocumentComponent {
  private readonly documents = inject(DocumentsService);
  private readonly route = inject(ActivatedRoute);
  private readonly toasts = inject(ToastService);
  private readonly localization = inject(LocalizationService);
  protected readonly lang = this.localization.lang;
  protected readonly permissions = KNOWLEDGE_PERMISSIONS;
  readonly documentId = input.required<string>();
  protected readonly document = signal<DocumentDto | null>(null);
  /** Text versions (drafts) are shown in the page; other types are downloaded. */
  protected readonly content = signal<MarkdownBlock[] | null>(null);
  protected readonly adding = signal(false);
  protected readonly page = signal<string | null>(null);
  protected readonly passage = signal<string | null>(null);

  constructor() {
    effect(() => void this.load(this.documentId()));
    this.route.queryParamMap.subscribe((params) => {
      this.page.set(params.get('page'));
      this.passage.set(params.get('passage'));
    });
  }

  private async load(id: string): Promise<void> {
    const doc = await firstValueFrom(this.documents.get(id));
    this.document.set(doc);
    const latest = doc.versions[0];
    if (latest && (latest.contentType === 'text/markdown' || latest.contentType === 'text/plain') && latest.sizeBytes <= 512 * 1024) {
      const text = await firstValueFrom(this.documents.getContent(latest.id)).catch(() => null);
      this.content.set(text ? markdownBlocks(text.text) : null);
    } else {
      this.content.set(null);
    }
  }

  /** FR-KNW-009: a person adds the agent draft to the workspace knowledge; indexing starts then. */
  protected async addToKnowledge(): Promise<void> {
    const doc = this.document();
    if (!doc) {
      return;
    }
    this.adding.set(true);
    try {
      this.document.set(await firstValueFrom(this.documents.addToKnowledge(doc.id)));
      this.toasts.success(this.localization.instant('Documents.AddedToKnowledge'));
    } finally {
      this.adding.set(false);
    }
  }

  protected async download(versionId: string): Promise<void> {
    const link = await firstValueFrom(this.documents.getDownloadUrl(versionId));
    window.open(link.url, '_blank', 'noopener');
  }
}
