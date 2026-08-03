import {
  Directive,
  TemplateRef,
  ViewContainerRef,
  effect,
  inject,
  input,
} from '@angular/core';

import { Permission } from '../../core/authz/permission.model';
import { PermissionService } from '../../core/authz/permission.service';

/**
 * Structural directive that renders its content only when the user holds the
 * permission(s).
 *
 *   <button *appHasPermission="'patients.create'">New patient</button>
 *   <div    *appHasPermission="['accounts.view', 'reports.view']">…</div>
 *
 * A list is satisfied by ANY of its entries; pass `mode: 'all'` to require all.
 * Because `PermissionService.granted` is a signal, editing a role on the Roles
 * screen updates every guarded control in the app immediately.
 */
@Directive({
  selector: '[appHasPermission]',
})
export class HasPermissionDirective {
  private readonly permissions = inject(PermissionService);
  private readonly template = inject(TemplateRef<unknown>);
  private readonly viewContainer = inject(ViewContainerRef);

  readonly appHasPermission = input.required<Permission | Permission[]>();
  readonly appHasPermissionMode = input<'any' | 'all'>('any');

  private rendered = false;

  constructor() {
    effect(() => {
      const required = this.appHasPermission();
      const list = Array.isArray(required) ? required : [required];
      const allowed =
        this.appHasPermissionMode() === 'all'
          ? this.permissions.canAll(list)
          : this.permissions.canAny(list);

      if (allowed && !this.rendered) {
        this.viewContainer.createEmbeddedView(this.template);
        this.rendered = true;
      } else if (!allowed && this.rendered) {
        this.viewContainer.clear();
        this.rendered = false;
      }
    });
  }
}
