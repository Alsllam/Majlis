import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, effect, inject, input, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';
import { EnArPipe, LocalizationService } from '@majlis/core';
import { DocumentDto, DocumentsService } from '@majlis/knowledge-proxy';

/**
 * One document: metadata, versions and download. A citation opens it with `?page=` and `?passage=` so the cited
 * passage is shown at the top (the in-page viewer with highlighting comes with the PDF viewer, FR-KNW-006).
 */
@Component({
  selector: 'majlis-document',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, TranslatePipe, DatePipe, EnArPipe],
  templateUrl: './document.component.html',
  styleUrl: './document.component.scss',
})
export class DocumentComponent {
  private readonly documents = inject(DocumentsService);
  private readonly route = inject(ActivatedRoute);
  protected readonly lang = inject(LocalizationService).lang;
  readonly documentId = input.required<string>();
  protected readonly document = signal<DocumentDto | null>(null);
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
    this.document.set(await firstValueFrom(this.documents.get(id)));
  }

  protected async download(versionId: string): Promise<void> {
    const link = await firstValueFrom(this.documents.getDownloadUrl(versionId));
    window.open(link.url, '_blank', 'noopener');
  }
}
