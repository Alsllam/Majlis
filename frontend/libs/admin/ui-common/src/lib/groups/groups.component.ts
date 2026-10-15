import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { Subject, debounceTime, distinctUntilChanged, firstValueFrom, switchMap } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ADMIN_PERMISSIONS } from '@majlis/admin-config';
import { LocalizationService } from '@majlis/core';
import { GroupDto, GroupListDto, GroupsService, UserLookupDto, UsersService } from '@majlis/identity-proxy';
import { AvatarComponent, BusyButtonDirective, EmptyStateComponent } from '@majlis/shared-ui-common';
import { PageHeaderComponent, ToastService } from '@majlis/theme-shared';

/** Tenant groups (FR-IDN-005): list, create, rename, delete, members through the user lookup. Tenant admins only. */
@Component({
  selector: 'majlis-groups',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, TranslatePipe, AvatarComponent, BusyButtonDirective, PageHeaderComponent, EmptyStateComponent],
  templateUrl: './groups.component.html',
  styleUrl: './groups.component.scss',
})
export class GroupsComponent {
  private readonly groups = inject(GroupsService);
  private readonly users = inject(UsersService);
  private readonly toasts = inject(ToastService);
  private readonly localization = inject(LocalizationService);
  private readonly search$ = new Subject<string>();
  protected readonly permissions = ADMIN_PERMISSIONS;
  protected readonly items = signal<GroupListDto[] | null>(null);
  protected readonly selected = signal<GroupDto | null>(null);
  protected readonly busy = signal(false);
  protected readonly newName = signal('');
  protected readonly editName = signal('');
  protected readonly editDescription = signal('');
  protected readonly query = signal('');
  protected readonly results = signal<UserLookupDto[] | null>(null);

  constructor() {
    this.load();
    this.search$
      .pipe(
        debounceTime(300),
        distinctUntilChanged(),
        switchMap((text) => this.users.lookup({ filterText: text, skipCount: 0, maxResultCount: 10 })),
        takeUntilDestroyed(),
      )
      .subscribe((page) => this.results.set(page.items));
  }

  protected load(): void {
    this.groups.getList({ skipCount: 0, maxResultCount: 100 }).subscribe((page) => this.items.set(page.items));
  }

  protected async open(id: string): Promise<void> {
    const group = await firstValueFrom(this.groups.get(id));
    this.selected.set(group);
    this.editName.set(group.name);
    this.editDescription.set(group.description ?? '');
    this.query.set('');
    this.results.set(null);
  }

  protected search(text: string): void {
    this.query.set(text);
    this.search$.next(text.trim());
  }

  private async run(action: () => Promise<unknown>, successKey: string, reopen = true): Promise<void> {
    if (this.busy()) {
      return;
    }
    this.busy.set(true);
    try {
      await action();
      this.toasts.success(this.localization.instant(successKey));
      this.load();
      const current = this.selected();
      if (reopen && current) {
        await this.open(current.id);
      }
    } catch {
      // Reported by the global handler.
    } finally {
      this.busy.set(false);
    }
  }

  protected async create(): Promise<void> {
    const name = this.newName().trim();
    if (!name) {
      return;
    }
    await this.run(async () => {
      const id = await firstValueFrom(this.groups.create({ name }));
      this.newName.set('');
      await this.open(id);
    }, 'Admin.Groups.Created', false);
  }

  protected save(): void {
    const group = this.selected();
    if (!group || !this.editName().trim()) {
      return;
    }
    void this.run(() => firstValueFrom(this.groups.update({ id: group.id, name: this.editName().trim(), description: this.editDescription().trim() || null })), 'Admin.Groups.Saved');
  }

  protected remove(): void {
    const group = this.selected();
    if (!group) {
      return;
    }
    void this.run(async () => {
      await firstValueFrom(this.groups.delete(group.id));
      this.selected.set(null);
    }, 'Admin.Groups.Deleted', false);
  }

  protected addMember(user: UserLookupDto): void {
    const group = this.selected();
    if (!group) {
      return;
    }
    void this.run(() => firstValueFrom(this.groups.setMember({ groupId: group.id, userId: user.id })), 'Admin.Groups.MemberAdded');
  }

  protected removeMember(userId: string): void {
    const group = this.selected();
    if (!group) {
      return;
    }
    void this.run(() => firstValueFrom(this.groups.removeMember({ groupId: group.id, userId })), 'Admin.Groups.MemberRemoved');
  }

  protected isMember(userId: string): boolean {
    return this.selected()?.members.some((m) => m.userId === userId) ?? false;
  }
}
