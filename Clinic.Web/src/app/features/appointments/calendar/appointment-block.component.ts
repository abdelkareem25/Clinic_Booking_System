import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

import { formatTime12 } from '../../../core/utils/date.util';
import { statusConfig } from '../../../core/utils/appointment-status.util';
import { IconComponent } from '../../../shared/ui/icon/icon.component';
import { PositionedItem } from './calendar.model';

/**
 * One appointment on the grid.
 *
 * Presentational: it is handed a box that the layout engine already resolved and
 * draws it. Position comes in as `topPx`/`heightPx` and never from a hard-coded
 * size, so a 90-minute appointment is three rows tall because it is 90 minutes
 * long, not because anything said "90 means three".
 *
 * Status is carried by an icon and a word as well as a colour. Colour alone
 * fails for the ~8% of men with a colour vision deficiency, and this is a screen
 * where confusing "cancelled" with "confirmed" wastes a patient's morning.
 */
@Component({
  selector: 'app-appointment-block',
  imports: [TranslatePipe, IconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'block-host',
    '[style.top.px]': 'item().topPx',
    '[style.height.px]': 'item().heightPx',
    '[style.inset-inline-start.%]': 'item().offsetPct',
    '[style.width.%]': 'item().widthPct',
    '[style.z-index]': 'item().zIndex',
  },
  template: `
    <div
      class="block"
      [class.block-compact]="compact()"
      [class.block-narrow]="item().clusterSize > 1"
      [class.block-muted]="muted()"
      [class.block-draggable]="draggable()"
      [style.--block-accent]="config().accent"
      [style.--block-surface]="config().surface"
      role="button"
      [attr.tabindex]="0"
      [attr.aria-label]="ariaLabel()"
      (click)="open.emit()"
      (keydown.enter)="onActivate($event)"
      (keydown.space)="onActivate($event)"
    >
      <span class="block-rail" aria-hidden="true"></span>

      <span class="block-time">
        <ui-icon [name]="config().icon" size="sm" class="block-status-icon" />
        {{ timeRange() }}
      </span>

      @if (!compact()) {
        <span class="block-patient">{{ item().appointment.patientName }}</span>
        <span class="block-doctor">{{ item().appointment.doctorName }}</span>

        @if (showStatus()) {
          <span class="block-status">{{ config().label | translate }}</span>
        }
      }
    </div>
  `,
  styles: `
    :host {
      position: absolute;
      padding-inline-end: 3px;
      /* Blocks are positioned, not laid out: the lane owns the coordinate space
         and a block must never push its neighbours around. */
    }

    .block {
      position: relative;
      display: flex;
      flex-direction: column;
      gap: 1px;
      height: 100%;
      padding: 5px var(--sp-2) 5px calc(var(--sp-2) + 3px);
      border: 1px solid color-mix(in srgb, var(--block-accent) 32%, transparent);
      border-radius: var(--r-sm);
      background: var(--block-surface);
      color: var(--c-text);
      overflow: hidden;
      text-align: start;
      cursor: pointer;
      transition:
        box-shadow var(--dur-micro) var(--ease-standard),
        transform var(--dur-micro) var(--ease-standard);
    }

    /* The padding above is physical-start on purpose — mirrored here rather than
       written as a logical property so the rail and its inset stay in step. */
    :host-context(html[dir='rtl']) .block {
      padding: 5px calc(var(--sp-2) + 3px) 5px var(--sp-2);
    }

    .block:hover {
      box-shadow: var(--sh-raised);
    }

    .block:focus-visible {
      outline: none;
      box-shadow: var(--sh-focus);
    }

    /* The colour bar. Its own element rather than a border-inline-start so it
       keeps its radius and flips with the writing direction for free. */
    .block-rail {
      position: absolute;
      inset-block: 0;
      inset-inline-start: 0;
      width: 3px;
      background: var(--block-accent);
    }

    .block-time {
      display: flex;
      align-items: center;
      gap: 4px;
      font-size: var(--fs-2xs);
      font-weight: var(--fw-semibold);
      color: color-mix(in srgb, var(--block-accent) 78%, var(--c-text));
      font-variant-numeric: tabular-nums;
      white-space: nowrap;
    }

    .block-status-icon {
      flex: 0 0 auto;
    }

    .block-patient {
      font-size: var(--fs-sm);
      font-weight: var(--fw-semibold);
      line-height: var(--lh-tight);
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }

    .block-doctor,
    .block-status {
      font-size: var(--fs-2xs);
      color: var(--c-text-muted);
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }

    .block-status {
      margin-block-start: auto;
      font-weight: var(--fw-medium);
      color: color-mix(in srgb, var(--block-accent) 70%, var(--c-text-muted));
    }

    /* Too short for three lines: the time and the icon are what still has to fit. */
    .block-compact {
      flex-direction: row;
      align-items: center;
      gap: var(--sp-2);
      padding-block: 2px;
    }

    .block-narrow .block-doctor {
      display: none;
    }

    /* Cancelled and completed recede: they are history, not today's plan. */
    .block-muted {
      opacity: 0.68;
    }

    .block-muted .block-patient {
      text-decoration: line-through;
      text-decoration-thickness: 1px;
    }

    .block-draggable {
      cursor: grab;
    }

    :host(.cdk-drag-dragging) {
      /* Let elementFromPoint see the lane underneath when the drag lands. */
      pointer-events: none;
      z-index: 200;
    }

    :host(.cdk-drag-dragging) .block {
      box-shadow: var(--sh-overlay);
      transform: scale(1.02);
    }
  `,
})
export class AppointmentBlockComponent {
  readonly item = input.required<PositionedItem>();
  readonly draggable = input(false);

  readonly open = output<void>();

  protected readonly config = computed(() => statusConfig(this.item().appointment.status));

  /** One row of grid is 56px; below ~46 there is no room for a second line. */
  protected readonly compact = computed(() => this.item().heightPx < 46);

  /** The status word needs a fourth line, which only a 60-minute block has. */
  protected readonly showStatus = computed(
    () => this.item().heightPx >= 88 && this.item().clusterSize === 1
  );

  protected readonly muted = computed(() => {
    const status = this.item().appointment.status;
    return status === 'Cancelled' || status === 'Completed';
  });

  protected readonly timeRange = computed(() => {
    const item = this.item();
    return `${formatTime12(item.startMinutes)} – ${formatTime12(item.endMinutes)}`;
  });

  /**
   * Everything the block shows, in one string.
   *
   * Screen readers announce the host, not the visual arrangement, and a compact
   * block hides the names entirely — so the label carries them regardless of how
   * much of the card is drawn.
   */
  protected readonly ariaLabel = computed(() => {
    const { appointment } = this.item();
    return `${appointment.patientName}, ${appointment.doctorName}, ${this.timeRange()}`;
  });

  protected onActivate(event: Event): void {
    // Space scrolls the grid otherwise, which moves the thing under the cursor.
    event.preventDefault();
    this.open.emit();
  }
}
