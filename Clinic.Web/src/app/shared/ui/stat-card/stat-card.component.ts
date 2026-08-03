import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';

import { IconComponent } from '../icon/icon.component';
import { IconName } from '../icon/icon.registry';

export type StatTone = 'primary' | 'info' | 'success' | 'warning' | 'danger' | 'neutral';

/**
 * A single headline figure.
 *
 * The reference dashboards stacked five identical cyan tiles, which made every
 * number look equally urgent. Here the tone is carried by a small icon tile and
 * the delta chip only — the value itself is always plain text, so the eye reads
 * the numbers first and the colour second.
 *
 * `delta` is a signed percentage: positive is not automatically good (a rising
 * cancellation rate is bad), so `invertDelta` decides which direction is green.
 */
@Component({
  selector: 'ui-stat-card',
  imports: [RouterLink, TranslatePipe, IconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="stat" [class.stat-link]="route()">
      <div class="top">
        <span class="label">{{ label() | translate }}</span>
        @if (icon(); as name) {
          <span class="icon-tile" [class]="'icon-tile-' + tone()">
            <ui-icon [name]="name" size="md" />
          </span>
        }
      </div>

      @if (loading()) {
        <div class="skeleton value-skeleton"></div>
      } @else {
        <div class="value metric-value">
          {{ value() }}
          @if (unit(); as u) {
            <span class="unit">{{ u }}</span>
          }
        </div>
      }

      <div class="bottom">
        @if (delta() !== null && !loading()) {
          <span class="badge" [class]="'badge-' + deltaTone()">
            <ui-icon [name]="delta()! >= 0 ? 'trendUp' : 'trendDown'" size="sm" />
            {{ deltaLabel() }}
          </span>
        }
        @if (caption(); as text) {
          <span class="caption">{{ text | translate }}</span>
        }
      </div>

      @if (route(); as target) {
        <a class="overlay-link" [routerLink]="target" [attr.aria-label]="label() | translate"></a>
      }
    </div>
  `,
  styles: `
    :host {
      display: block;
      min-width: 0;
    }

    .stat {
      position: relative;
      display: flex;
      flex-direction: column;
      gap: var(--sp-3);
      height: 100%;
      padding: var(--sp-4) var(--sp-5);
      background: var(--c-surface);
      border: 1px solid var(--c-border);
      border-radius: var(--r-lg);
      box-shadow: var(--sh-card);
      transition:
        border-color var(--dur-micro) var(--ease-standard),
        box-shadow var(--dur-micro) var(--ease-standard);
    }

    .stat-link:hover {
      border-color: var(--c-border-strong);
      box-shadow: var(--sh-raised);
    }

    .top {
      display: flex;
      align-items: flex-start;
      justify-content: space-between;
      gap: var(--sp-3);
    }

    .label {
      font-size: var(--fs-xs);
      font-weight: var(--fw-medium);
      color: var(--c-text-muted);
      letter-spacing: 0.01em;
    }

    .value {
      display: flex;
      align-items: baseline;
      gap: 6px;
      font-size: var(--fs-2xl);
      font-weight: var(--fw-semibold);
      letter-spacing: -0.03em;
      line-height: 1.1;
      color: var(--c-text);
    }

    .unit {
      font-size: var(--fs-sm);
      font-weight: var(--fw-medium);
      color: var(--c-text-muted);
      letter-spacing: 0;
    }

    .value-skeleton {
      width: 84px;
      height: 30px;
      border-radius: var(--r-xs);
    }

    .bottom {
      display: flex;
      align-items: center;
      gap: var(--sp-2);
      flex-wrap: wrap;
      margin-block-start: auto;
    }

    .caption {
      font-size: var(--fs-xs);
      color: var(--c-text-subtle);
    }

    /* Whole-card click target without nesting interactive elements. */
    .overlay-link {
      position: absolute;
      inset: 0;
      border-radius: inherit;
    }

    .overlay-link:focus-visible {
      outline: 2px solid var(--c-primary);
      outline-offset: 2px;
    }
  `,
})
export class StatCardComponent {
  readonly label = input.required<string>();
  readonly value = input.required<string | number>();
  readonly unit = input<string | null>(null);
  readonly icon = input<IconName | null>(null);
  readonly tone = input<StatTone>('neutral');
  readonly caption = input<string | null>(null);
  readonly loading = input(false);
  readonly route = input<string | null>(null);
  /** Signed percentage change against the previous period. */
  readonly delta = input<number | null>(null);
  /** Set when a rise is bad — cancellations, expenses, no-shows. */
  readonly invertDelta = input(false);

  protected readonly deltaTone = computed(() => {
    const delta = this.delta();
    if (delta === null || delta === 0) {
      return 'neutral';
    }
    const positive = delta > 0;
    return positive !== this.invertDelta() ? 'success' : 'danger';
  });

  protected readonly deltaLabel = computed(() => {
    const delta = this.delta();
    if (delta === null) {
      return '';
    }
    return `${delta > 0 ? '+' : ''}${delta.toFixed(1)}%`;
  });
}
