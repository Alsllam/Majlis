import { Injectable, computed, inject, signal } from '@angular/core';
import { PermissionService } from '../permissions/permission.service';

export enum eLayoutType {
  application = 'application',
  empty = 'empty',
}

/** One sidebar entry. `name` is a translation key (`Menu.Rooms`). */
export interface MenuRoute {
  path: string;
  name: string;
  icon?: string;
  order?: number;
  parentName?: string;
  layout?: eLayoutType;
  requiredPolicy?: string;
  expanded?: boolean;
}

export interface MenuNode extends MenuRoute {
  children: MenuNode[];
}

/** Feature `config` libraries add their entries at startup; the sidebar renders the granted ones (skill §5.1). */
@Injectable({ providedIn: 'root' })
export class RoutesService {
  private readonly permissions = inject(PermissionService);
  private readonly routes = signal<MenuRoute[]>([]);

  readonly tree = computed<MenuNode[]>(() => {
    const visible = this.routes().filter((r) => this.permissions.isGranted(r.requiredPolicy));
    const byOrder = (a: MenuRoute, b: MenuRoute) => (a.order ?? 0) - (b.order ?? 0);
    const build = (parent?: string): MenuNode[] =>
      visible
        .filter((r) => r.parentName === parent)
        .sort(byOrder)
        .map((r) => ({ ...r, children: build(r.name) }));
    return build(undefined);
  });

  add(routes: MenuRoute[]): void {
    this.routes.update((current) => [...current, ...routes]);
  }
}
