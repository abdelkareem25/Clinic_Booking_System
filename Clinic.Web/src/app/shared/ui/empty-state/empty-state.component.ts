import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

import { IconComponent } from '../icon/icon.component';
import { IconName } from '../icon/icon.registry';

/**
 * What a list shows when it has nothing to show.
 *
 * The reference app printed a bare sentence on a black panel, which reads as a
 * failure. An empty state should explain the situation and offer the action
 * that resolves it — so this always has room for a projected CTA, and
 * distinguishes "nothing exists yet" from "nothing matched your filters",
 * which need different next steps.
 */
@Component({
  selector: 'ui-empty-state',
  imports: [TranslatePipe, IconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="empty" [class.empty-compact]="compact()">
      <span class="art" [class]="'art-' + tone()">
        <ui-icon [name]="icon()" size="lg" />
      </span>

      <h3 class="title">{{ title() | translate }}</h3>

      @if (message(); as text) {
        <p class="message">{{ text | translate }}</p>
      }

      <div class="cta">
        <ng-content />
      </div>
    </div>
  `,
  styles: `
    :host {
      display: block;
    }

    .empty {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      gap: var(--sp-2);
      padding: var(--sp-9) var(--sp-6);
      text-align: center;
    }

    .empty-compact {
      padding: var(--sp-6) var(--sp-4);
    }

    .art {
      display: grid;
      place-items: center;
      width: 52px;
      height: 52px;
      margin-block-end: var(--sp-2);
      border-radius: var(--r-xl);
      background: var(--c-neutral-soft);
      color: var(--c-text-subtle);
    }

    .art-primary {
      background: var(--c-primary-soft);
      color: var(--c-primary-soft-fg);
    }

    .art-warning {
      background: var(--c-warning-soft);
      color: var(--c-warning-soft-fg);
    }

    .art-danger {
      background: var(--c-danger-soft);
      color: var(--c-danger-soft-fg);
    }

    .title {
      font-size: var(--fs-md);
      font-weight: var(--fw-semibold);
    }

    .message {
      max-width: 42ch;
      font-size: var(--fs-sm);
      color: var(--c-text-muted);
      line-height: var(--lh-snug);
    }

    .cta:not(:empty) {
      margin-block-start: var(--sp-3);
      display: flex;
      gap: var(--sp-2);
      flex-wrap: wrap;
      justify-content: center;
    }
  `,
})
export class EmptyStateComponent {
  readonly title = input.required<string>();
  readonly message = input<string | null>(null);
  readonly icon = input<IconName>('empty');
  readonly tone = input<'neutral' | 'primary' | 'warning' | 'danger'>('neutral');
  readonly compact = input(false);
}
