import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ConfigService } from '@majlis/core';
import { AuditService } from './audit.service';

describe('AuditService', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    jest.spyOn(TestBed.inject(ConfigService), 'apiUrl').mockImplementation((name) => `http://localhost:7000/api/${name}`);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('lists entries with POST /entries/list', () => {
    TestBed.inject(AuditService).getList({ category: 'approval', skipCount: 0, maxResultCount: 50 }).subscribe();
    const req = http.expectOne('http://localhost:7000/api/audit/entries/list');
    expect(req.request.body).toEqual({ category: 'approval', skipCount: 0, maxResultCount: 50 });
    req.flush({ items: [], totalCount: 0 });
  });

  it('exports as a blob with POST /entries/export', () => {
    TestBed.inject(AuditService).export({ skipCount: 0, maxResultCount: 50 }).subscribe();
    const req = http.expectOne('http://localhost:7000/api/audit/entries/export');
    expect(req.request.responseType).toBe('blob');
    req.flush(new Blob(['seq']));
  });
});
