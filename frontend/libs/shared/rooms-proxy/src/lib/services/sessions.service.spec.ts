import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { ConfigService } from '@majlis/core';
import { SessionsService } from './sessions.service';
import { RoomsService } from './rooms.service';

describe('rooms proxy', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    jest
      .spyOn(TestBed.inject(ConfigService), 'apiUrl')
      .mockImplementation((name) => `http://localhost:7000/api/${name}`);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('instructs the agent with the epoch and client request id', () => {
    const body = { sessionId: 's1', text: 'لخص', epoch: 3, clientRequestId: 'c1' };
    TestBed.inject(SessionsService).instruct(body).subscribe();
    const req = http.expectOne('http://localhost:7000/api/rooms/sessions/instruct');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(body);
    req.flush({ turnId: 't1', seq: 7 });
  });

  it('lists rooms through POST /rooms/list', () => {
    TestBed.inject(RoomsService).getList({ skipCount: 0, maxResultCount: 20 }).subscribe();
    const req = http.expectOne('http://localhost:7000/api/rooms/rooms/list');
    expect(req.request.method).toBe('POST');
    req.flush({ items: [], totalCount: 0 });
  });
});
