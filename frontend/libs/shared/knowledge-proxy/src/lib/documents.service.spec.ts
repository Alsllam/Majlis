import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ConfigService } from '@majlis/core';
import { contentTypeOf } from './document.model';
import { DocumentsService } from './documents.service';

describe('DocumentsService', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    jest.spyOn(TestBed.inject(ConfigService), 'apiUrl').mockImplementation((name) => `http://localhost:7000/api/${name}`);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('begins an upload with POST /documents/begin-upload', () => {
    const body = { workspaceId: 'w', fileName: 'a.pdf', contentType: 'application/pdf', sizeBytes: 10, type: 'Contract' as const };
    TestBed.inject(DocumentsService).beginUpload(body).subscribe();
    const req = http.expectOne('http://localhost:7000/api/knowledge/documents/begin-upload');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(body);
    req.flush({ documentId: 'd', versionId: 'v', uploadUrl: 'http://blob/x?sig', expiresAt: '', headers: {} });
  });

  it('PUTs the file to the pre-signed url with the ticket headers', () => {
    const ticket = { documentId: 'd', versionId: 'v', uploadUrl: 'http://127.0.0.1:10000/documents/x?sig=1', expiresAt: '', headers: { 'x-ms-blob-type': 'BlockBlob' } };
    TestBed.inject(DocumentsService).putToStorage(ticket, new Blob(['hi'])).subscribe();
    const req = http.expectOne(ticket.uploadUrl);
    expect(req.request.method).toBe('PUT');
    expect(req.request.headers.get('x-ms-blob-type')).toBe('BlockBlob');
    req.flush('');
  });

  it('maps content types, including markdown by extension', () => {
    expect(contentTypeOf({ name: 'notes.md', type: '' })).toBe('text/markdown');
    expect(contentTypeOf({ name: 'a.pdf', type: 'application/pdf' })).toBe('application/pdf');
    expect(contentTypeOf({ name: 'a.zip', type: 'application/zip' })).toBeNull();
  });
});
