import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';
import { TranslatePipe } from '@ngx-translate/core';
import { Subject, debounceTime, distinctUntilChanged, switchMap } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { UserLookupDto, UsersService } from '@majlis/identity-proxy';
import { AvatarComponent, BusyButtonDirective } from '@majlis/shared-ui-common';
import { WORKSPACE_ROLES, WorkspaceRole, WorkspacesService } from '@majlis/workspaces-proxy';

/** Picks a tenant user (Identity lookup) and a workspace role. */
@Component({
  selector: 'majlis-add-member-modal',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, TranslatePipe, AvatarComponent, BusyButtonDirective],
  templateUrl: './add-member-modal.component.html',
  styles: `
    .results { list-style: none; margin: var(--space-2) 0 0; padding: 0; display: grid; gap: var(--space-1); max-height: 240px; overflow: auto; }
    .user { display: flex; align-items: center; gap: var(--space-2); width: 100%; text-align: start; border: 1px solid var(--border-subtle); background: var(--surface); border-radius: var(--radius-md); padding: var(--space-2) var(--space-3); cursor: pointer;
      &:hover { background: var(--surface-hover); }
      &.active { border-color: var(--brand-solid); background: var(--brand-subtle); }
      &:disabled { opacity: .5; cursor: default; }
    }
    .email { color: var(--text-muted); font-size: var(--fs-xs); }
    .empty { color: var(--text-muted); font-size: var(--fs-sm); padding: var(--space-2) 0; }
  `,
})
export class AddMemberModalComponent {
  protected readonly modal = inject(NgbActiveModal);
  private readonly users = inject(UsersService);
  private readonly workspaces = inject(WorkspacesService);
  private readonly search$ = new Subject<string>();
  protected readonly roles = WORKSPACE_ROLES;
  protected readonly results = signal<UserLookupDto[] | null>(null);
  protected readonly selected = signal<UserLookupDto | null>(null);
  protected readonly role = signal<WorkspaceRole>('Contributor');
  protected readonly busy = signal(false);
  protected query = '';
  workspaceId = '';
  canAssignOwner = false;
  existing = new Set<string>();

  constructor() {
    this.search$
      .pipe(
        debounceTime(300),
        distinctUntilChanged(),
        switchMap((text) => this.users.lookup({ filterText: text, skipCount: 0, maxResultCount: 20 })),
        takeUntilDestroyed(),
      )
      .subscribe((page) => this.results.set(page.items));
    this.search$.next('');
  }

  protected search(text: string): void {
    this.query = text;
    this.search$.next(text.trim());
  }

  protected submit(): void {
    const user = this.selected();
    if (!user) {
      return;
    }
    this.busy.set(true);
    this.workspaces
      .setMember({ workspaceId: this.workspaceId, userId: user.id, displayName: user.displayName, role: this.role() })
      .subscribe({ next: () => this.modal.close(user.id), error: () => this.busy.set(false) });
  }
}
