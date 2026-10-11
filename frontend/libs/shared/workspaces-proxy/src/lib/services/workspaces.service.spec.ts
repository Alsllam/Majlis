import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ConfigService } from '@majlis/core';
import { WorkspacesService } from './workspaces.service';

describe('WorkspacesService', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    jest.spyOn(TestBed.inject(ConfigService), 'apiUrl').mockImplementation((name) => `http://localhost:7000/api/${name}`);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('sets a member with POST /workspaces/members', () => {
    const body = { workspaceId: 'w1', userId: 'u1', displayName: 'خالد', role: 'Contributor' as const };
    TestBed.inject(WorkspacesService).setMember(body).subscribe();
    const req = http.expectOne('http://localhost:7000/api/workspaces/workspaces/members');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(body);
    req.flush(null);
  });

  it('removes a member with DELETE and a body', () => {
    TestBed.inject(WorkspacesService).removeMember({ workspaceId: 'w1', userId: 'u1' }).subscribe();
    const req = http.expectOne('http://localhost:7000/api/workspaces/workspaces/members');
    expect(req.request.method).toBe('DELETE');
    expect(req.request.body).toEqual({ workspaceId: 'w1', userId: 'u1' });
    req.flush(null);
  });
});
