import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';

import { PermissionService } from '../../../core/authz/permission.service';
import { RolesStore } from '../../../core/authz/roles.store';
import { AuthService } from '../../../core/services/auth.service';
import { JwtService } from '../../../core/services/jwt.service';
import { CardComponent } from '../../../shared/ui/card/card.component';
import { EmptyStateComponent } from '../../../shared/ui/empty-state/empty-state.component';
import { IconComponent } from '../../../shared/ui/icon/icon.component';
import { PageHeaderComponent } from '../../../shared/ui/page-header/page-header.component';

/**
 * Staff accounts.
 *
 * The API exposes registration and login but **no endpoint that lists users**,
 * so this screen shows the signed-in account and its resolved permissions, and
 * says plainly that the roster is unavailable. Inventing a list would be worse
 * than an honest gap — an administrator would act on it.
 */
@Component({
  selector: 'app-user-list',
  imports: [
    DatePipe,
    RouterLink,
    MatButtonModule,
    TranslatePipe,
    CardComponent,
    EmptyStateComponent,
    IconComponent,
    PageHeaderComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './user-list.component.html',
  styleUrl: './user-list.component.scss',
})
export class UserListComponent {
  private readonly auth = inject(AuthService);
  private readonly jwt = inject(JwtService);
  private readonly rolesStore = inject(RolesStore);

  protected readonly permissions = inject(PermissionService);

  protected readonly user = toSignal(this.auth.currentUser$, {
    initialValue: this.auth.currentUser,
  });

  protected readonly roleNames = computed(() => this.permissions.roleNames());

  protected readonly grantedCount = computed(() => this.permissions.granted().size);

  protected readonly sessionExpiry = computed(() =>
    this.jwt.getExpiry(this.user()?.token)
  );

  protected readonly initials = computed(() => {
    const name = this.user()?.displayName?.trim();
    if (!name) {
      return '—';
    }
    const parts = name.split(/\s+/).filter(Boolean);
    return ((parts[0]?.[0] ?? '') + (parts[1]?.[0] ?? '')).toUpperCase();
  });

  protected roleDescription(roleName: string): string {
    return this.rolesStore.getByName(roleName)?.description ?? '';
  }
}
