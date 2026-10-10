import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, throwError } from 'rxjs';
import { ConfigService } from '../config/config.service';
import { HttpErrorReporterService } from './http-error-reporter.service';

export interface RestRequest<TBody> {
  method: 'GET' | 'POST' | 'PUT' | 'DELETE' | 'PATCH';
  url: string;
  body?: TBody;
  params?: Record<string, string | number | boolean | undefined>;
}

export interface RestConfig {
  /** Key in `app-settings.json → apis`. */
  apiName: string;
  /** The caller shows the error itself; it is not reported to the global handler. */
  skipHandleError?: boolean;
}

/** The only way features call the backend. The OAuth interceptor adds the bearer token (skill §7). */
@Injectable({ providedIn: 'root' })
export class RestService {
  private readonly http = inject(HttpClient);
  private readonly config = inject(ConfigService);
  private readonly reporter = inject(HttpErrorReporterService);

  request<TBody, TResult>(request: RestRequest<TBody>, config: RestConfig): Observable<TResult> {
    const url = this.config.apiUrl(config.apiName) + '/' + request.url.replace(/^\/+/, '');
    let params = new HttpParams();
    for (const [key, value] of Object.entries(request.params ?? {})) {
      if (value !== undefined) {
        params = params.set(key, String(value));
      }
    }
    return this.http
      .request<TResult>(request.method, url, { body: request.body, params, responseType: 'json' })
      .pipe(
        catchError((error: HttpErrorResponse) => {
          if (!config.skipHandleError) {
            this.reporter.reportError(error);
          }
          return throwError(() => error);
        }),
      );
  }
}
