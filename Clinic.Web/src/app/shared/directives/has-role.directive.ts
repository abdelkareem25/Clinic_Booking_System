import { Directive, Input, TemplateRef, ViewContainerRef, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import { Role } from '../../core/models/auth.model';
import { AuthService } from '../../core/services/auth.service';

@Directive({
  selector: '[appHasRole]',
  standalone: true
})
export class HasRoleDirective {
  private readonly auth = inject(AuthService);
  private readonly template = inject(TemplateRef<unknown>);
  private readonly viewContainer = inject(ViewContainerRef);
  private roles: Role[] = [];
  private rendered = false;

  @Input() set appHasRole(value: Role | Role[]) {
    this.roles = Array.isArray(value) ? value : [value];
    this.updateView();
  }

  constructor() {
    this.auth.currentUser$.pipe(takeUntilDestroyed()).subscribe(() => this.updateView());
  }

  private updateView(): void {
    const allowed = this.auth.hasAnyRole(this.roles);

    if (allowed && !this.rendered) {
      this.viewContainer.createEmbeddedView(this.template);
      this.rendered = true;
    }

    if (!allowed && this.rendered) {
      this.viewContainer.clear();
      this.rendered = false;
    }
  }
}

