import { BaseFilterDto } from '@majlis/core';

/** Mirrors `Majlis.Knowledge.Application.Documents.DTOs` (enums as strings). */
export type DocumentType = 'Other' | 'Regulation' | 'Contract' | 'Policy' | 'Sop' | 'Report';
export const DOCUMENT_TYPES: readonly DocumentType[] = ['Regulation', 'Contract', 'Policy', 'Sop', 'Report', 'Other'];
export type DocumentOrigin = 'Upload' | 'Agent';
export type IngestionStatus = 'Pending' | 'Queued' | 'Processing' | 'Indexed' | 'Failed' | 'Superseded';

/** FR-KNW-001 accepted content types, same list as `KnowledgeFieldDefinitions.ContentTypes`. */
export const ACCEPTED_CONTENT_TYPES: Record<string, string> = {
  'application/pdf': '.pdf',
  'application/vnd.openxmlformats-officedocument.wordprocessingml.document': '.docx',
  'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet': '.xlsx',
  'application/vnd.openxmlformats-officedocument.presentationml.presentation': '.pptx',
  'text/plain': '.txt',
  'text/markdown': '.md',
  'image/png': '.png',
  'image/jpeg': '.jpg',
};

/** Browsers leave `File.type` empty for `.md`; map by extension when needed. */
export function contentTypeOf(file: { name: string; type: string }): string | null {
  if (file.type && ACCEPTED_CONTENT_TYPES[file.type]) {
    return file.type;
  }
  const ext = '.' + (file.name.split('.').pop() ?? '').toLowerCase();
  const match = Object.entries(ACCEPTED_CONTENT_TYPES).find(([, e]) => e === ext || (ext === '.jpeg' && e === '.jpg'));
  return match?.[0] ?? null;
}

export interface DocumentVersionDto {
  id: string;
  number: number;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  status: IngestionStatus;
  failureReasonKey: string | null;
  chunkCount: number;
  pageCount: number;
  creationTime: string;
  indexedAt: string | null;
}

export interface DocumentDto {
  id: string;
  workspaceId: string;
  roomId: string | null;
  title: string;
  type: DocumentType;
  language: string | null;
  effectiveDate: string | null;
  tags: string | null;
  origin: DocumentOrigin;
  currentVersionId: string | null;
  creationTime: string;
  creatorId: string | null;
  versions: DocumentVersionDto[];
}

export interface DocumentListDto {
  id: string;
  workspaceId: string;
  roomId: string | null;
  title: string;
  type: DocumentType;
  language: string | null;
  status: IngestionStatus;
  failureReasonKey: string | null;
  fileName: string;
  sizeBytes: number;
  pageCount: number;
  creationTime: string;
  creatorId: string | null;
}

export interface FilterDocumentDto extends BaseFilterDto {
  workspaceId: string;
  roomId?: string | null;
  status?: IngestionStatus | null;
}

export interface BeginUploadDto {
  workspaceId: string;
  roomId?: string | null;
  documentId?: string | null;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  title?: string | null;
  type: DocumentType;
  language?: 'ar' | 'en' | null;
}

export interface UploadTicketDto {
  documentId: string;
  versionId: string;
  uploadUrl: string;
  expiresAt: string;
  headers: Record<string, string>;
}

export interface DownloadUrlDto {
  url: string;
  expiresAt: string;
  fileName: string;
}

export interface UpdateDocumentDto {
  id: string;
  title: string;
  type: DocumentType;
  effectiveDate?: string | null;
  tags?: string | null;
}
