import { HttpErrorResponse } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable, Subject } from 'rxjs';

/** `RestService` reports every unhandled HTTP failure here; `theme-shared` turns them into toasts and redirects. */
@Injectable({ providedIn: 'root' })
export class HttpErrorReporterService {
  private readonly errors = new Subject<HttpErrorResponse>();

  readonly errors$: Observable<HttpErrorResponse> = this.errors.asObservable();

  reportError(error: HttpErrorResponse): void {
    this.errors.next(error);
  }
}
