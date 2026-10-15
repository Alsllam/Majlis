import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { TranslatePipe } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';
import { AuthService, LocalizationService, PermissionDirective } from '@majlis/core';
import { GroupListDto, GroupsService, InvitationDto, InvitationsService } from '@majlis/identity-proxy';
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
  private readonly groups = inject(GroupsService);
  private readonly invitations = inject(InvitationsService);
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
  protected readonly tenantGroups = signal<GroupListDto[]>([]);
  protected readonly groupToAdd = signal('');
  protected readonly groupRole = signal<WorkspaceRole>('Contributor');
  protected readonly pendingInvitations = signal<InvitationDto[]>([]);
  protected readonly inviteEmail = signal('');
  protected readonly inviteRole = signal<WorkspaceRole>('Contributor');
  protected readonly grantableRoles = WORKSPACE_ROLES.filter((r) => r !== 'Owner');
  protected readonly availableGroups = computed(() => {
    const granted = new Set(this.workspace()?.groups.map((g) => g.groupId));
    return this.tenantGroups().filter((g) => !granted.has(g.id));
  });

  constructor() {
    effect(() => void this.load(this.workspaceId()));
  }

  private async load(id: string): Promise<void> {
    const ws = await firstValueFrom(this.workspaces.get(id));
    this.workspace.set(ws);
    this.instructions.set(ws.agentInstructions ?? '');
    if (canManageWorkspace(ws.myRole) && !ws.isArchived) {
      const [groups, invitations] = await Promise.all([
        firstValueFrom(this.groups.getList({ skipCount: 0, maxResultCount: 100 })).catch(() => ({ items: [] as GroupListDto[] })),
        firstValueFrom(this.invitations.getList({ workspaceId: id, skipCount: 0, maxResultCount: 50 })).catch(() => ({ items: [] as InvitationDto[] })),
      ]);
      this.tenantGroups.set(groups.items);
      this.pendingInvitations.set(invitations.items);
    }
  }

  protected addGroup(): void {
    const ws = this.workspace();
    const group = this.tenantGroups().find((g) => g.id === this.groupToAdd());
    if (!ws || !group) {
      return;
    }
    void this.run(async () => {
      await firstValueFrom(this.workspaces.setGroup({ workspaceId: ws.id, groupId: group.id, groupName: group.name, role: this.groupRole() }));
      this.groupToAdd.set('');
    }, 'Workspaces.GroupAdded');
  }

  protected changeGroupRole(groupId: string, groupName: string, role: WorkspaceRole): void {
    const ws = this.workspace();
    if (!ws) {
      return;
    }
    void this.run(() => firstValueFrom(this.workspaces.setGroup({ workspaceId: ws.id, groupId, groupName, role })), 'Workspaces.RoleChanged');
  }

  protected removeGroup(groupId: string): void {
    const ws = this.workspace();
    if (!ws) {
      return;
    }
    void this.run(() => firstValueFrom(this.workspaces.removeGroup({ workspaceId: ws.id, groupId })), 'Workspaces.GroupRemoved');
  }

  protected invite(): void {
    const ws = this.workspace();
    const email = this.inviteEmail().trim();
    if (!ws || !email) {
      return;
    }
    void this.run(async () => {
      const result = await firstValueFrom(this.invitations.create({ email, workspaceId: ws.id, workspaceName: ws.name, workspaceRole: this.inviteRole(), language: this.localization.lang() }));
      this.inviteEmail.set('');
      if (result.acceptedImmediately) {
        this.toasts.success(this.localization.instant('Workspaces.MemberAdded'));
      }
    }, 'Workspaces.Invited');
  }

  protected revokeInvitation(id: string): void {
    void this.run(() => firstValueFrom(this.invitations.revoke(id)), 'Workspaces.InvitationRevoked');
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
