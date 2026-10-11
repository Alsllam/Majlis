/** Shapes shared with the .NET backend (`Majlis.Framework.Application.Dtos`). */
export interface PagedResultDto<T> {
  items: T[];
  totalCount: number;
}

export type ActiveFilter = 'All' | 'Active' | 'InActive';

export interface BaseFilterDto {
  filterText?: string;
  skipCount: number;
  maxResultCount: number;
  sorting?: string;
  activeFilter?: ActiveFilter;
}

export interface EntityIdDto {
  id: string;
}

export interface LookupDto {
  id: string;
  displayName: string;
}

/** The backend error envelope written by `ExceptionHandlingMiddleware`. */
export interface ApiError {
  error: {
    code: string;
    date: string;
    messages: string[];
    source: 'Validation' | 'Application' | 'Unexpected' | string;
  };
}

export function isApiError(value: unknown): value is ApiError {
  return (
    typeof value === 'object' &&
    value !== null &&
    'error' in value &&
    typeof (value as ApiError).error === 'object' &&
    Array.isArray((value as ApiError).error?.messages)
  );
}
