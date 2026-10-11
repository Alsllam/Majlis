import { Directive, ElementRef, effect, inject, input } from '@angular/core';

/** Disables the button and shows an inline spinner while keeping its width (skill §0.4). */
@Directive({ selector: 'button[majlisBusy]' })
export class BusyButtonDirective {
  private readonly el = inject<ElementRef<HTMLButtonElement>>(ElementRef);
  readonly majlisBusy = input.required<boolean>();

  constructor() {
    effect(() => {
      const button = this.el.nativeElement;
      const busy = this.majlisBusy();
      if (busy && !button.dataset['busy']) {
        button.dataset['busy'] = '1';
        button.style.minWidth = `${button.offsetWidth}px`;
        button.setAttribute('aria-busy', 'true');
        button.disabled = true;
        const spinner = document.createElement('span');
        spinner.className = 'spinner-border spinner-border-sm me-2';
        spinner.setAttribute('role', 'status');
        spinner.dataset['busySpinner'] = '1';
        button.prepend(spinner);
      } else if (!busy && button.dataset['busy']) {
        delete button.dataset['busy'];
        button.removeAttribute('aria-busy');
        button.disabled = false;
        button.querySelector('[data-busy-spinner]')?.remove();
      }
    });
  }
}
