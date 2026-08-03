import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatMenuModule } from '@angular/material/menu';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RouterOutlet } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';

import { AppLanguage } from '../../core/i18n/locale.model';
import { LocaleService } from '../../core/i18n/locale.service';
import { ThemeService } from '../../core/services/theme.service';
import { IconComponent } from '../../shared/ui/icon/icon.component';

/**
 * The signed-out shell: a brand panel beside the form.
 *
 * The panel is not decoration — it states what the product does and carries the
 * language and theme controls, because a user who cannot read the English login
 * form needs to switch language *before* signing in. On narrow screens the
 * panel collapses to a single header line so the form stays above the fold.
 */
@Component({
  selector: 'app-auth-layout',
  imports: [
    RouterOutlet,
    MatButtonModule,
    MatMenuModule,
    MatTooltipModule,
    TranslatePipe,
    IconComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './auth-layout.component.html',
  styleUrl: './auth-layout.component.scss',
})
export class AuthLayoutComponent {
  protected readonly locale = inject(LocaleService);
  protected readonly theme = inject(ThemeService);

  protected readonly year = new Date().getFullYear();

  protected readonly features = [
    { icon: 'appointments', label: 'auth.featureScheduling' },
    { icon: 'records', label: 'auth.featureRecords' },
    { icon: 'accounts', label: 'auth.featureFinance' },
  ] as const;

  protected setLanguage(language: AppLanguage): void {
    this.locale.use(language);
  }
}
