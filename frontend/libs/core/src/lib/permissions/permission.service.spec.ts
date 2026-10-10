import { TestBed } from '@angular/core/testing';
import { AuthService } from '../auth/auth.service';
import { ConfigService } from '../config/config.service';
import { PermissionService } from './permission.service';

describe('PermissionService', () => {
  const setup = (roles: string[]) => {
    TestBed.configureTestingModule({
      providers: [
        { provide: AuthService, useValue: { user: () => ({ roles }) } },
        { provide: ConfigService, useValue: { settings: { rolePermissions: { TenantAdmin: ['*'], Member: ['Permissions.Rooms.ViewRoom', 'Permissions.Rooms.DriveSession'] } } } },
      ],
    });
    return TestBed.inject(PermissionService);
  };

  it('grants everything to a wildcard role', () => {
    expect(setup(['TenantAdmin']).isGranted('Permissions.Rooms.TakeOverSession')).toBe(true);
  });

  it('evaluates || and && like the backend policy strings', () => {
    const member = setup(['Member']);
    expect(member.isGranted('Permissions.Rooms.ViewRoom')).toBe(true);
    expect(member.isGranted('Permissions.Rooms.TakeOverSession')).toBe(false);
    expect(member.isGranted('Permissions.Rooms.TakeOverSession || Permissions.Rooms.ViewRoom')).toBe(true);
    expect(member.isGranted('Permissions.Rooms.ViewRoom && Permissions.Rooms.TakeOverSession')).toBe(false);
    expect(member.isGranted('')).toBe(true);
  });
});
