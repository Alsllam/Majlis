import { Pipe, PipeTransform, inject } from '@angular/core';
import { LocalizationService } from './localization.service';

const ARABIC_INDIC = ['٠', '١', '٢', '٣', '٤', '٥', '٦', '٧', '٨', '٩'];

/** Formats a number for the current language: Arabic-Indic digits in `ar`, Latin digits in `en` (skill §0.3). */
@Pipe({ name: 'enar', pure: false })
export class EnArPipe implements PipeTransform {
  private readonly localization = inject(LocalizationService);

  transform(value: number | string | null | undefined): string {
    if (value === null || value === undefined) {
      return '';
    }
    const text = typeof value === 'number' ? new Intl.NumberFormat('en-US').format(value) : value;
    return this.localization.lang() === 'ar' ? text.replace(/\d/g, (d) => ARABIC_INDIC[Number(d)] ?? d) : text;
  }
}
