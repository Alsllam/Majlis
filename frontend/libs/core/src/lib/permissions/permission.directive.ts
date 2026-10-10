import { Directive, TemplateRef, ViewContainerRef, effect, inject, input } from '@angular/core';
import { PermissionService } from './permission.service';

/** `*majlisPermission="'Permissions.Rooms.CreateRoom'"` renders the element only when the policy is granted. */
@Directive({ selector: '[majlisPermission]' })
export class PermissionDirective {
  private readonly permissions = inject(PermissionService);
  private readonly template = inject(TemplateRef<unknown>);
  private readonly container = inject(ViewContainerRef);
  private shown = false;

  readonly majlisPermission = input.required<string>();

  constructor() {
    effect(() => {
      const granted = this.permissions.isGranted(this.majlisPermission());
      if (granted && !this.shown) {
        this.container.createEmbeddedView(this.template);
        this.shown = true;
      } else if (!granted && this.shown) {
        this.container.clear();
        this.shown = false;
      }
    });
  }
}
