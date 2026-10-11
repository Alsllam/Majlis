import { ApplicationInitStatus } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { AuthService, ConfigService, RoutesService } from '@majlis/core';
import { provideWorkspacesConfig } from './workspaces-config.providers';

describe('provideWorkspacesConfig', () => {
  it('adds the Workspaces menu entry before Rooms', async () => {
    TestBed.configureTestingModule({
      providers: [
        provideWorkspacesConfig(),
        { provide: ConfigService, useValue: { settings: { rolePermissions: { Member: ['Permissions.Workspaces.ViewWorkspace'] } } } },
        { provide: AuthService, useValue: { user: () => ({ roles: ['Member'] }) } },
      ],
    });
    await TestBed.inject(ApplicationInitStatus).donePromise;
    expect(TestBed.inject(RoutesService).tree().map((n) => n.name)).toEqual(['Menu.Workspaces']);
  });
});
