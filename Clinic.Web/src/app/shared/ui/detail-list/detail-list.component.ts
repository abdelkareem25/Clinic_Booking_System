import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

import { IconComponent } from '../icon/icon.component';
import { IconName } from '../icon/icon.registry';

export interface DetailItem {
  /** Translation key. */
  label: string;
  /** Already-localised display value; `null` renders "Not set". */
  value: string | number | null | undefined;
  icon?: IconName;
  /** Render across the full grid width — addresses, long notes. */
  wide?: boolean;
  /** Emphasise the value: blood group, balance, allergy warnings. */
  tone?: 'default' | 'strong' | 'danger';
}

/**
 * A labelled fact grid — the read view of any record.
 *
 * Detail screens in the reference app rendered the same information as a form
 * with disabled inputs, which reads as broken. Facts that cannot be edited in
 * place should look like facts, so this is a real `<dl>`: shorter to scan,
 * correct for screen readers, and free of dead controls.
 */
@Component({
  selector: 'ui-detail-list',
  imports: [TranslatePipe, IconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <dl class="details" [style.--detail-cols]="columns()">
      @for (item of items(); track item.label) {
        <div class="item" [class.item-wide]="item.wide">
          <dt>
            @if (item.icon; as icon) {
              <ui-icon [name]="icon" size="sm" />
            }
            {{ item.label | translate }}
          </dt>
          <dd [class]="'value-' + (item.tone ?? 'default')">
            @if (item.value === null || item.value === undefined || item.value === '') {
              <span class="unset">{{ 'common.notSet' | translate }}</span>
            } @else {
              {{ item.value }}
            }
          </dd>
        </div>
      }
    </dl>
  `,
  styles: `
    :host {
      display: block;
    }

    .details {
      display: grid;
      grid-template-columns: repeat(var(--detail-cols, 2), minmax(0, 1fr));
      gap: var(--sp-4) var(--sp-5);
      margin: 0;
    }

    .item {
      display: flex;
      flex-direction: column;
      gap: 3px;
      min-width: 0;
    }

    .item-wide {
      grid-column: 1 / -1;
    }

    dt {
      display: flex;
      align-items: center;
      gap: 5px;
      font-size: var(--fs-xs);
      font-weight: var(--fw-medium);
      color: var(--c-text-subtle);
      letter-spacing: 0.01em;
    }

    dd {
      margin: 0;
      font-size: var(--fs-base);
      color: var(--c-text);
      overflow-wrap: anywhere;
    }

    .value-strong {
      font-weight: var(--fw-semibold);
    }

    .value-danger {
      font-weight: var(--fw-semibold);
      color: var(--c-danger);
    }

    .unset {
      color: var(--c-text-subtle);
    }

    @media (max-width: 640px) {
      .details {
        grid-template-columns: 1fr;
      }
    }
  `,
})
export class DetailListComponent {
  readonly items = input.required<DetailItem[]>();
  readonly columns = input(2);
}
