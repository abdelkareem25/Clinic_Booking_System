import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';

import { IconComponent } from '../icon/icon.component';
import { IconName } from '../icon/icon.registry';

export interface Breadcrumb {
  label: string;
  route?: string;
}

/**
 * The standard top of every page: an optional back affordance and breadcrumb
 * trail, the title, a one-line explanation, and a slot for actions.
 *
 * Every screen uses it, which is what makes the app feel like one product
 * rather than fourteen. Actions are projected via `[actions]` so each page
 * decides its own buttons while the spacing and alignment stay fixed.
 */
@Component({
  selector: 'ui-page-header',
  imports: [RouterLink, TranslatePipe, IconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="header">
      @if (breadcrumbs().length) {
        <nav class="crumbs" aria-label="Breadcrumb">
          @for (crumb of breadcrumbs(); track crumb.label; let last = $last) {
            @if (crumb.route && !last) {
              <a [routerLink]="crumb.route">{{ crumb.label | translate }}</a>
              <ui-icon name="chevronRight" size="sm" class="crumb-sep" />
            } @else {
              <span aria-current="page">{{ crumb.label | translate }}</span>
            }
          }
        </nav>
      }

      <div class="row">
        @if (backRoute(); as route) {
          <a
            class="back"
            [routerLink]="route"
            [attr.aria-label]="'common.back' | translate"
          >
            <ui-icon name="arrowLeft" size="md" />
          </a>
        }

        @if (icon(); as name) {
          <span class="icon-tile icon-tile-lg icon-tile-primary">
            <ui-icon [name]="name" size="lg" />
          </span>
        }

        <div class="titles">
          <h1 class="text-page-title">{{ title() | translate }}</h1>
          @if (subtitle(); as text) {
            <p class="subtitle">{{ text | translate }}</p>
          }
        </div>

        <div class="actions">
          <ng-content select="[actions]" />
        </div>
      </div>

      <ng-content />
    </header>
  `,
  styles: `
    .header {
      display: flex;
      flex-direction: column;
      gap: var(--sp-3);
    }

    .crumbs {
      display: flex;
      align-items: center;
      gap: var(--sp-1);
      font-size: var(--fs-xs);
      color: var(--c-text-muted);
    }

    .crumbs a:hover {
      color: var(--c-primary);
      text-decoration: none;
    }

    .crumbs [aria-current] {
      color: var(--c-text);
      font-weight: var(--fw-medium);
    }

    .crumb-sep {
      color: var(--c-text-subtle);
    }

    /* Chevrons point at the reading direction, so RTL has to mirror them. */
    :host-context(html[dir='rtl']) .crumb-sep,
    :host-context(html[dir='rtl']) .back ui-icon {
      transform: scaleX(-1);
    }

    .row {
      display: flex;
      align-items: center;
      gap: var(--sp-3);
      flex-wrap: wrap;
    }

    .back {
      display: grid;
      place-items: center;
      width: 34px;
      height: 34px;
      border-radius: var(--r-sm);
      border: 1px solid var(--c-border);
      background: var(--c-surface);
      color: var(--c-text-muted);
      transition:
        background-color var(--dur-micro) var(--ease-standard),
        color var(--dur-micro) var(--ease-standard);
    }

    .back:hover {
      background: var(--c-surface-2);
      color: var(--c-text);
      text-decoration: none;
    }

    .titles {
      display: flex;
      flex-direction: column;
      gap: 2px;
      min-width: 0;
      flex: 1 1 260px;
    }

    .subtitle {
      font-size: var(--fs-sm);
      color: var(--c-text-muted);
    }

    .actions {
      display: flex;
      align-items: center;
      gap: var(--sp-2);
      flex-wrap: wrap;
    }

    @media (max-width: 640px) {
      .actions {
        width: 100%;
      }
    }
  `,
})
export class PageHeaderComponent {
  readonly title = input.required<string>();
  readonly subtitle = input<string | null>(null);
  readonly icon = input<IconName | null>(null);
  readonly backRoute = input<string | null>(null);
  readonly breadcrumbs = input<Breadcrumb[]>([]);
}
