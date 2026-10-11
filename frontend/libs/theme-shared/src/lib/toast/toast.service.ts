import { Injectable, signal } from '@angular/core';

export type ToastSeverity = 'success' | 'info' | 'warning' | 'danger';

export interface Toast {
  id: number;
  severity: ToastSeverity;
  /** Already translated text. */
  text: string;
  durationMs: number;
}

@Injectable({ providedIn: 'root' })
export class ToastService {
  private next = 1;
  private readonly list = signal<Toast[]>([]);

  readonly toasts = this.list.asReadonly();

  show(text: string, severity: ToastSeverity = 'info', durationMs = 5000): void {
    const toast: Toast = { id: this.next++, severity, text, durationMs };
    this.list.update((all) => [...all, toast]);
    setTimeout(() => this.dismiss(toast.id), durationMs);
  }

  success(text: string): void {
    this.show(text, 'success');
  }

  error(text: string): void {
    this.show(text, 'danger', 8000);
  }

  dismiss(id: number): void {
    this.list.update((all) => all.filter((t) => t.id !== id));
  }
}
