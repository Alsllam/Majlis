import { ApplicationInitStatus } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { AuthService, ConfigService, RoutesService } from '@majlis/core';
import { provideTasksConfig } from './tasks-config.providers';

describe('provideTasksConfig', () => {
  it('adds the Tasks menu entry', async () => {
    TestBed.configureTestingModule({
      providers: [
        provideTasksConfig(),
        { provide: ConfigService, useValue: { settings: { rolePermissions: { Member: ['Permissions.Tasks.ViewTask'] } } } },
        { provide: AuthService, useValue: { user: () => ({ roles: ['Member'] }) } },
      ],
    });
    await TestBed.inject(ApplicationInitStatus).donePromise;
    expect(TestBed.inject(RoutesService).tree().map((n) => n.name)).toEqual(['Menu.Tasks']);
  });
});
