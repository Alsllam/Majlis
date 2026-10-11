import { TestBed } from '@angular/core/testing';
import { ApplicationInitStatus } from '@angular/core';
import { AuthService, ConfigService, RoutesService } from '@majlis/core';
import { provideRoomsConfig } from './rooms-config.providers';

describe('provideRoomsConfig', () => {
  it('adds the Rooms menu entry for a user who may view rooms', async () => {
    TestBed.configureTestingModule({
      providers: [
        provideRoomsConfig(),
        { provide: ConfigService, useValue: { settings: { rolePermissions: { Member: ['Permissions.Rooms.ViewRoom'] } } } },
        { provide: AuthService, useValue: { user: () => ({ roles: ['Member'] }) } },
      ],
    });
    await TestBed.inject(ApplicationInitStatus).donePromise;
    expect(TestBed.inject(RoutesService).tree().map((n) => n.name)).toEqual(['Menu.Rooms']);
  });
});
