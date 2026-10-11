import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { EmptyStateComponent } from '@majlis/shared-ui-common';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [EmptyStateComponent, RouterLink, TranslatePipe],
  template: `<majlis-empty-state title="Errors.NotFoundTitle" text="Errors.NotFoundText"><a class="btn btn-primary" routerLink="/">{{ 'General.Home' | translate }}</a></majlis-empty-state>`,
})
export class NotFoundComponent {}
