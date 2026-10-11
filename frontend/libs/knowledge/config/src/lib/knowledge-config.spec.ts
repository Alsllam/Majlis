import { ApplicationInitStatus } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { AuthService, ConfigService, RoutesService } from '@majlis/core';
import { provideKnowledgeConfig } from './knowledge-config.providers';

describe('provideKnowledgeConfig', () => {
  it('adds the Documents menu entry', async () => {
    TestBed.configureTestingModule({
      providers: [
        provideKnowledgeConfig(),
        { provide: ConfigService, useValue: { settings: { rolePermissions: { Member: ['Permissions.Knowledge.ViewDocument'] } } } },
        { provide: AuthService, useValue: { user: () => ({ roles: ['Member'] }) } },
      ],
    });
    await TestBed.inject(ApplicationInitStatus).donePromise;
    expect(TestBed.inject(RoutesService).tree().map((n) => n.name)).toEqual(['Menu.Documents']);
  });
});
