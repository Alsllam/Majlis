import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { TranslatePipe } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';
import { AuthService, LocalizationService, PermissionDirective } from '@majlis/core';
import { AvatarComponent, BusyButtonDirective } from '@majlis/shared-ui-common';
import { ToastService } from '@majlis/theme-shared';
import { WORKSPACES_PERMISSIONS } from '@majlis/workspaces-config';
import { WORKSPACE_ROLES, WorkspaceDto, WorkspaceRole, WorkspacesService, canManageWorkspace } from '@majlis/workspaces-proxy';
import { WorkspaceFormModalComponent } from '../workspaces/workspace-form-modal.component';
import { AddMemberModalComponent } from './add-member-modal.component';

/** One workspace: details, members and roles, agent instructions, archive (FR-WSP-001…006). */
@Component({
  selector: 'majlis-workspace',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, FormsModule, TranslatePipe, PermissionDirective, AvatarComponent, BusyButtonDirective],
  templateUrl: './workspace.component.html',
  styleUrl: './workspace.component.scss',
})
export class WorkspaceComponent {
  private readonly workspaces = inject(WorkspacesService);
  private readonly modal = inject(NgbModal);
  private readonly toasts = inject(ToastService);
  private readonly localization = inject(LocalizationService);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  protected readonly permissions = WORKSPACES_PERMISSIONS;
  protected readonly roles = WORKSPACE_ROLES;
  readonly workspaceId = input.required<string>();
  protected readonly workspace = signal<WorkspaceDto | null>(null);
  protected readonly busy = signal(false);
  protected readonly instructions = signal('');
  protected readonly canManage = computed(() => canManageWorkspace(this.workspace()?.myRole));
  protected readonly isOwner = computed(() => this.workspace()?.myRole === 'Owner');
  protected readonly meId = computed(() => this.auth.user()?.id ?? '');
  protected readonly instructionsDirty = computed(() => (this.workspace()?.agentInstructions ?? '') !== this.instructions());

  constructor() {
    effect(() => void this.load(this.workspaceId()));
  }

  private async load(id: string): Promise<void> {
    const ws = await firstValueFrom(this.workspaces.get(id));
    this.workspace.set(ws);
    this.instructions.set(ws.agentInstructions ?? '');
  }

  private async run(action: () => Promise<unknown>, successKey?: string): Promise<void> {
    if (this.busy()) {
      return;
    }
    this.busy.set(true);
    try {
      await action();
      await this.load(this.workspaceId());
      if (successKey) {
        this.toasts.success(this.localization.instant(successKey));
      }
    } catch {
      // Reported by the global handler.
    } finally {
      this.busy.set(false);
    }
  }

  protected edit(): void {
    const ref = this.modal.open(WorkspaceFormModalComponent, { centered: true });
    (ref.componentInstance as WorkspaceFormModalComponent).init(this.workspace());
    ref.closed.subscribe(() => void this.run(() => Promise.resolve(), 'Workspaces.Saved'));
  }

  protected addMember(): void {
    const ws = this.workspace();
    if (!ws) {
      return;
    }
    const ref = this.modal.open(AddMemberModalComponent, { centered: true });
    const instance = ref.componentInstance as AddMemberModalComponent;
    instance.workspaceId = ws.id;
    instance.canAssignOwner = this.isOwner();
    instance.existing = new Set(ws.members.map((m) => m.userId));
    ref.closed.subscribe(() => void this.run(() => Promise.resolve(), 'Workspaces.MemberAdded'));
  }

  protected changeRole(userId: string, displayName: string, role: WorkspaceRole): void {
    const ws = this.workspace();
    if (!ws) {
      return;
    }
    void this.run(() => firstValueFrom(this.workspaces.setMember({ workspaceId: ws.id, userId, displayName, role })), 'Workspaces.RoleChanged');
  }

  protected removeMember(userId: string): void {
    const ws = this.workspace();
    if (!ws) {
      return;
    }
    void this.run(() => firstValueFrom(this.workspaces.removeMember({ workspaceId: ws.id, userId })), 'Workspaces.MemberRemoved');
  }

  protected saveInstructions(): void {
    const ws = this.workspace();
    if (!ws) {
      return;
    }
    void this.run(
      () => firstValueFrom(this.workspaces.updateAgentInstructions({ id: ws.id, agentInstructions: this.instructions().trim() || null })),
      'Workspaces.Saved',
    );
  }

  protected toggleArchive(): void {
    const ws = this.workspace();
    if (!ws) {
      return;
    }
    void this.run(() => firstValueFrom(ws.isArchived ? this.workspaces.restore(ws.id) : this.workspaces.archive(ws.id)), ws.isArchived ? 'Workspaces.Restored' : 'Workspaces.ArchivedDone');
  }

  protected openRooms(): void {
    void this.router.navigate(['/rooms'], { queryParams: { workspaceId: this.workspaceId() } });
  }
}
