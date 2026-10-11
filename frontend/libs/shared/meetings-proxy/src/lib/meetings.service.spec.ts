import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ConfigService } from '@majlis/core';
import { MeetingsService } from './meetings.service';

describe('MeetingsService', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    jest.spyOn(TestBed.inject(ConfigService), 'apiUrl').mockImplementation((name) => `http://localhost:7000/api/${name}`);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('lists meetings with POST /meetings/list', () => {
    TestBed.inject(MeetingsService).getList({ workspaceId: 'w', skipCount: 0, maxResultCount: 100 }).subscribe();
    const req = http.expectOne('http://localhost:7000/api/meetings/meetings/list');
    expect(req.request.method).toBe('POST');
    req.flush({ items: [], totalCount: 0 });
  });

  it('edits the minutes with PUT /meetings/minutes', () => {
    TestBed.inject(MeetingsService).updateMinutes({ meetingId: 'm', summary: 's', decisions: [], openQuestions: [], actionItems: [] }).subscribe();
    const req = http.expectOne('http://localhost:7000/api/meetings/meetings/minutes');
    expect(req.request.method).toBe('PUT');
    req.flush({});
  });

  it('exports the minutes as a blob', () => {
    TestBed.inject(MeetingsService).exportMinutes('m').subscribe();
    const req = http.expectOne('http://localhost:7000/api/meetings/meetings/export-minutes');
    expect(req.request.responseType).toBe('blob');
    req.flush(new Blob(['# x']));
  });
});
