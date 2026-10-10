import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ConfigService } from '@majlis/core';
import { TasksService } from './tasks.service';

describe('TasksService', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    jest.spyOn(TestBed.inject(ConfigService), 'apiUrl').mockImplementation((name) => `http://localhost:7000/api/${name}`);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('lists tasks with POST /tasks/list', () => {
    TestBed.inject(TasksService).getList({ workspaceId: 'w', skipCount: 0, maxResultCount: 100 }).subscribe();
    const req = http.expectOne('http://localhost:7000/api/tasks/tasks/list');
    expect(req.request.method).toBe('POST');
    req.flush({ items: [], totalCount: 0 });
  });

  it('changes the status with POST /tasks/status', () => {
    TestBed.inject(TasksService).setStatus({ id: 't', status: 'Done' }).subscribe();
    const req = http.expectOne('http://localhost:7000/api/tasks/tasks/status');
    expect(req.request.body).toEqual({ id: 't', status: 'Done' });
    req.flush(null);
  });
});
