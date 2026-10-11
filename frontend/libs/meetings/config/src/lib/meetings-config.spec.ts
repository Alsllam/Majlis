import { ApplicationInitStatus } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { AuthService, ConfigService, RoutesService } from '@majlis/core';
import { provideMeetingsConfig } from './meetings-config.providers';

describe('provideMeetingsConfig', () => {
  it('adds the Meetings menu entry', async () => {
    TestBed.configureTestingModule({
      providers: [
        provideMeetingsConfig(),
        { provide: ConfigService, useValue: { settings: { rolePermissions: { Member: ['Permissions.Meetings.ViewMeeting'] } } } },
        { provide: AuthService, useValue: { user: () => ({ roles: ['Member'] }) } },
      ],
    });
    await TestBed.inject(ApplicationInitStatus).donePromise;
    expect(TestBed.inject(RoutesService).tree().map((n) => n.name)).toEqual(['Menu.Meetings']);
  });
});
