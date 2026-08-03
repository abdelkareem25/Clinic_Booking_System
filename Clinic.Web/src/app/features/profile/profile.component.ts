import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { TranslatePipe } from '@ngx-translate/core';

import { PermissionService } from '../../core/authz/permission.service';
import { AppLanguage } from '../../core/i18n/locale.model';
import { LocaleService } from '../../core/i18n/locale.service';
import { AuthService } from '../../core/services/auth.service';
import { JwtService } from '../../core/services/jwt.service';
import { ThemeService } from '../../core/services/theme.service';
import { CardComponent } from '../../shared/ui/card/card.component';
import { DetailItem, DetailListComponent } from '../../shared/ui/detail-list/detail-list.component';
import { IconComponent } from '../../shared/ui/icon/icon.component';
import { PageHeaderComponent } from '../../shared/ui/page-header/page-header.component';

/**
 * The signed-in user's own account.
 *
 * Identity is read-only: the API has no "update my profile" or "change
 * password" endpoint, and a form that silently discards what you typed is worse
 * than no form. Preferences (language, theme) *are* editable, because those are
 * genuinely owned by the client.
 */
@Component({
  selector: 'app-profile',
  imports: [
    MatButtonModule,
    MatSlideToggleModule,
    TranslatePipe,
    CardComponent,
    DetailListComponent,
    IconComponent,
    PageHeaderComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './profile.component.html',
  styleUrl: './profile.component.scss',
})
export class ProfileComponent {
  private readonly auth = inject(AuthService);
  private readonly jwt = inject(JwtService);

  protected readonly locale = inject(LocaleService);
  protected readonly permissions = inject(PermissionService);
  protected readonly theme = inject(ThemeService);

  protected readonly user = toSignal(this.auth.currentUser$, {
    initialValue: this.auth.currentUser,
  });

  protected readonly initials = computed(() => {
    const name = this.user()?.displayName?.trim();
    if (!name) {
      return '—';
    }
    const parts = name.split(/\s+/).filter(Boolean);
    return ((parts[0]?.[0] ?? '') + (parts[1]?.[0] ?? '')).toUpperCase();
  });

  protected readonly details = computed<DetailItem[]>(() => {
    const account = this.user();
    if (!account) {
      return [];
    }

    const expiry = this.jwt.getExpiry(account.token);

    return [
      { label: 'auth.displayName', value: account.displayName, icon: 'user', tone: 'strong' },
      { label: 'auth.email', value: account.email, icon: 'mail' },
      { label: 'auth.role', value: this.permissions.roleNames().join(', '), icon: 'shield' },
      {
        label: 'roles.permissions',
        value: `${this.permissions.granted().size}`,
        icon: 'checklist',
      },
      {
        label: 'auth.sessionExpired',
        value: expiry ? expiry.toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' }) : null,
        icon: 'clock',
        wide: true,
      },
    ];
  });

  protected setLanguage(language: AppLanguage): void {
    this.locale.use(language);
  }

  protected logout(): void {
    this.auth.logout();
  }
}
