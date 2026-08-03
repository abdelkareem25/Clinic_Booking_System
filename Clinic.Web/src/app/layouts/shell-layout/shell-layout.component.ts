import { BreakpointObserver, Breakpoints } from '@angular/cdk/layout';
import { ChangeDetectionStrategy, Component, computed, effect, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatDividerModule } from '@angular/material/divider';
import { MatMenuModule } from '@angular/material/menu';
import { MatTooltipModule } from '@angular/material/tooltip';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { filter, map } from 'rxjs';

import { PermissionService } from '../../core/authz/permission.service';
import { NotificationsStore } from '../../core/data/notifications.store';
import { AppLanguage } from '../../core/i18n/locale.model';
import { LocaleService } from '../../core/i18n/locale.service';
import { AuthService } from '../../core/services/auth.service';
import { ThemeService } from '../../core/services/theme.service';
import { NAV_GROUPS } from '../../routes/navigation.config';
import { IconComponent } from '../../shared/ui/icon/icon.component';

const SIDEBAR_STORAGE_KEY = 'clinic.sidebar.collapsed';

/**
 * The application shell: sidebar, top bar and the routed content region.
 *
 * Two behaviours are worth calling out.
 *
 * The sidebar collapses to an icon rail rather than disappearing, because
 * clinic staff navigate constantly and losing the rail costs a click every
 * time. The choice is persisted per device.
 *
 * Navigation is rendered from `NAV_GROUPS` filtered by permission, and a group
 * whose items are all filtered out is dropped along with its heading — so the
 * sidebar always describes what this user can actually do.
 */
@Component({
  selector: 'app-shell-layout',
  imports: [
    RouterLink,
    RouterLinkActive,
    RouterOutlet,
    MatButtonModule,
    MatDividerModule,
    MatMenuModule,
    MatTooltipModule,
    TranslatePipe,
    IconComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './shell-layout.component.html',
  styleUrl: './shell-layout.component.scss',
})
export class ShellLayoutComponent {
  private readonly auth = inject(AuthService);
  private readonly breakpoints = inject(BreakpointObserver);
  private readonly permissions = inject(PermissionService);
  private readonly router = inject(Router);

  protected readonly locale = inject(LocaleService);
  protected readonly notifications = inject(NotificationsStore);
  protected readonly theme = inject(ThemeService);

  protected readonly user = toSignal(this.auth.currentUser$, {
    initialValue: this.auth.currentUser,
  });

  protected readonly isHandset = toSignal(
    this.breakpoints
      .observe([Breakpoints.Handset, Breakpoints.TabletPortrait])
      .pipe(map((state) => state.matches)),
    { initialValue: false }
  );

  protected readonly collapsed = signal(this.readCollapsed());
  protected readonly drawerOpen = signal(false);

  /** Permission-filtered navigation; empty groups are dropped with their heading. */
  protected readonly navGroups = computed(() => {
    // Touch the permission set so the sidebar re-renders when a role changes.
    this.permissions.granted();

    return NAV_GROUPS.map((group) => ({
      ...group,
      items: group.items.filter((item) => this.permissions.canAny(item.permissions)),
    })).filter((group) => group.items.length > 0);
  });

  protected readonly initials = computed(() => {
    const name = this.user()?.displayName?.trim();
    if (!name) {
      return '—';
    }
    const parts = name.split(/\s+/).filter(Boolean);
    return ((parts[0]?.[0] ?? '') + (parts[1]?.[0] ?? '')).toUpperCase();
  });

  protected readonly roleLabel = computed(() => this.permissions.roleNames().join(' · '));

  constructor() {
    // Closing the drawer on navigation is what makes the mobile sidebar feel
    // like a menu rather than a page that has to be dismissed manually.
    this.router.events
      .pipe(filter((event): event is NavigationEnd => event instanceof NavigationEnd))
      .subscribe(() => this.drawerOpen.set(false));

    effect(() => {
      const collapsed = this.collapsed();
      try {
        localStorage.setItem(SIDEBAR_STORAGE_KEY, String(collapsed));
      } catch {
        /* storage blocked — the rail simply resets on reload */
      }
    });
  }

  protected toggleSidebar(): void {
    if (this.isHandset()) {
      this.drawerOpen.update((open) => !open);
      return;
    }
    this.collapsed.update((value) => !value);
  }

  protected closeDrawer(): void {
    this.drawerOpen.set(false);
  }

  protected setLanguage(language: AppLanguage): void {
    this.locale.use(language);
  }

  protected logout(): void {
    this.auth.logout();
  }

  private readCollapsed(): boolean {
    try {
      return localStorage.getItem(SIDEBAR_STORAGE_KEY) === 'true';
    } catch {
      return false;
    }
  }
}
