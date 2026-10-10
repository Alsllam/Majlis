import { AuthService } from './auth.service';

const encode = (claims: object) => {
  const json = new TextEncoder().encode(JSON.stringify(claims));
  const b64 = btoa(String.fromCharCode(...json)).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
  return `h.${b64}.s`;
};

describe('AuthService.parse', () => {
  it('reads the Majlis claims from the access token, including Arabic names', () => {
    const user = AuthService.parse(encode({ sub: 'u1', name: 'سارة', tenant_id: 't1', role: ['TenantAdmin'], locale: 'ar' }));
    expect(user).toEqual({ id: 'u1', name: 'سارة', tenantId: 't1', roles: ['TenantAdmin'], locale: 'ar' });
  });

  it('accepts a single role claim and defaults the locale to Arabic', () => {
    expect(AuthService.parse(encode({ sub: 'u2', role: 'Member' }))).toMatchObject({ roles: ['Member'], locale: 'ar' });
  });

  it('returns null for a malformed token', () => {
    expect(AuthService.parse('not-a-token')).toBeNull();
  });
});
