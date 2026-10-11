import { ApplicationInitStatus } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { AuthService, ConfigService, RoutesService } from '@majlis/core';
import { provideApprovalsConfig } from './approvals-config.providers';

describe('provideApprovalsConfig', () => {
  it('adds the Approvals menu entry', async () => {
    TestBed.configureTestingModule({
      providers: [
        provideApprovalsConfig(),
        { provide: ConfigService, useValue: { settings: { rolePermissions: { Member: ['Permissions.Approvals.ViewApproval'] } } } },
        { provide: AuthService, useValue: { user: () => ({ roles: ['Member'] }) } },
      ],
    });
    await TestBed.inject(ApplicationInitStatus).donePromise;
    expect(TestBed.inject(RoutesService).tree().map((n) => n.name)).toEqual(['Menu.Approvals']);
  });
});
