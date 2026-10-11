import { ApplicationInitStatus } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { AuthService, ConfigService, RoutesService } from '@majlis/core';
import { provideAuditConfig } from './audit-config.providers';

describe('provideAuditConfig', () => {
  it('shows the Audit entry to admins only', async () => {
    TestBed.configureTestingModule({
      providers: [
        provideAuditConfig(),
        { provide: ConfigService, useValue: { settings: { rolePermissions: { TenantAdmin: ['*'], Member: [] } } } },
        { provide: AuthService, useValue: { user: () => ({ roles: ['Member'] }) } },
      ],
    });
    await TestBed.inject(ApplicationInitStatus).donePromise;
    expect(TestBed.inject(RoutesService).tree().map((n) => n.name)).toEqual([]);
  });
});
