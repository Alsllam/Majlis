import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ConfigService } from '@majlis/core';
import { ApprovalsService } from './approvals.service';

describe('ApprovalsService', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    jest.spyOn(TestBed.inject(ConfigService), 'apiUrl').mockImplementation((name) => `http://localhost:7000/api/${name}`);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('lists the inbox with POST /requests/list', () => {
    TestBed.inject(ApprovalsService).getList({ forMe: true, skipCount: 0, maxResultCount: 50 }).subscribe();
    const req = http.expectOne('http://localhost:7000/api/approvals/requests/list');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ forMe: true, skipCount: 0, maxResultCount: 50 });
    req.flush({ items: [], totalCount: 0 });
  });

  it('rejects with a reason through POST /requests/reject', () => {
    TestBed.inject(ApprovalsService).reject({ id: 'a', reason: 'ليست أولوية' }).subscribe();
    const req = http.expectOne('http://localhost:7000/api/approvals/requests/reject');
    expect(req.request.body).toEqual({ id: 'a', reason: 'ليست أولوية' });
    req.flush({});
  });
});
