import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ConfigService } from '@majlis/core';
import { NotificationsService } from './notifications.service';

describe('NotificationsService', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    jest.spyOn(TestBed.inject(ConfigService), 'apiUrl').mockImplementation((name) => `http://localhost:7000/api/${name}`);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('reads the unread count with POST /notifications/unread-count', () => {
    TestBed.inject(NotificationsService).getUnreadCount().subscribe();
    const req = http.expectOne('http://localhost:7000/api/notifications/notifications/unread-count');
    expect(req.request.method).toBe('POST');
    req.flush({ count: 2 });
  });

  it('marks rows read with POST /notifications/mark-read', () => {
    TestBed.inject(NotificationsService).markRead(['a', 'b']).subscribe();
    const req = http.expectOne('http://localhost:7000/api/notifications/notifications/mark-read');
    expect(req.request.body).toEqual({ ids: ['a', 'b'] });
    req.flush(null);
  });
});
