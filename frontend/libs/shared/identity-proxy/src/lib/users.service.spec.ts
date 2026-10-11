import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ConfigService } from '@majlis/core';
import { UsersService } from './users.service';

describe('UsersService', () => {
  it('looks users up through POST /users/lookup on the identity api', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    jest.spyOn(TestBed.inject(ConfigService), 'apiUrl').mockImplementation((name) => `http://localhost:7000/api/${name}`);
    const http = TestBed.inject(HttpTestingController);
    TestBed.inject(UsersService).lookup({ filterText: 'خا', skipCount: 0, maxResultCount: 10 }).subscribe();
    const req = http.expectOne('http://localhost:7000/api/identity/users/lookup');
    expect(req.request.method).toBe('POST');
    req.flush({ items: [], totalCount: 0 });
    http.verify();
  });
});
