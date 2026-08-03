import { ChangeDetectionStrategy, Component } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';

import { EmptyStateComponent } from '../../shared/ui/empty-state/empty-state.component';

@Component({
  selector: 'app-unauthorized',
  imports: [RouterLink, MatButtonModule, TranslatePipe, EmptyStateComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <ui-empty-state
      icon="lock"
      tone="warning"
      title="errors.forbiddenTitle"
      message="errors.forbiddenBody"
    >
      <a mat-flat-button routerLink="/dashboard">
        {{ 'errors.backToDashboard' | translate }}
      </a>
    </ui-empty-state>
  `,
})
export class UnauthorizedComponent {}
