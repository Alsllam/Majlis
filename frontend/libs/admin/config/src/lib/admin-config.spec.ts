import { ApplicationInitStatus } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { AuthService, ConfigService, RoutesService } from '@majlis/core';
import { provideAdminConfig } from './admin-config.providers';

describe('provideAdminConfig', () => {
  it('adds the Groups menu entry for tenant admins', async () => {
    TestBed.configureTestingModule({
      providers: [
        provideAdminConfig(),
        { provide: ConfigService, useValue: { settings: { rolePermissions: { TenantAdmin: ['*'] } } } },
        { provide: AuthService, useValue: { user: () => ({ roles: ['TenantAdmin'] }) } },
      ],
    });
    await TestBed.inject(ApplicationInitStatus).donePromise;
    expect(TestBed.inject(RoutesService).tree().map((n) => n.name)).toEqual(['Menu.Groups']);
  });
});
