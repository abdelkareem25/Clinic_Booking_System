import { ChangeDetectionStrategy, Component, computed, input, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatTooltipModule } from '@angular/material/tooltip';
import { TranslatePipe } from '@ngx-translate/core';

import { IconComponent } from '../icon/icon.component';

export interface ChartPoint {
  /** Axis label — short, already localised. */
  label: string;
  value: number;
  /** Optional richer label for the tooltip and table view. */
  detail?: string;
}

export type ChartType = 'column' | 'line';

// A fixed coordinate space keeps mark geometry exact; the SVG scales
// proportionally to its container, so nothing is distorted.
const VIEW_W = 640;
const PLOT_H = 180;
const AXIS_H = 26;
const PAD_L = 44;
const PAD_R = 12;
const PAD_T = 14;
const MAX_BAR_W = 24;

interface Column {
  index: number;
  label: string;
  value: number;
  detail?: string;
  x: number;
  y: number;
  width: number;
  height: number;
  path: string;
  hitX: number;
  hitWidth: number;
  centre: number;
}

/**
 * The dashboard's charts.
 *
 * Deliberately single-series: every chart here answers one question, so there
 * is one hue and no legend — the card title names what is plotted. Marks follow
 * fixed specs (bars capped at 24px with a 4px rounded cap and a square
 * baseline, 2px lines, an 8px end marker ringed in the surface colour) and the
 * grid is a recessive hairline.
 *
 * Every chart ships a table view. A tooltip may enhance a value but must never
 * be the only way to read it.
 */
@Component({
  selector: 'ui-chart',
  imports: [MatButtonModule, MatTooltipModule, TranslatePipe, IconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './chart.component.html',
  styleUrl: './chart.component.scss',
})
export class ChartComponent {
  readonly points = input.required<readonly ChartPoint[]>();
  readonly type = input<ChartType>('column');
  /** Accessible description of what is plotted. */
  readonly caption = input.required<string>();
  /** Appended to values in tooltips and the table, e.g. a currency code. */
  readonly unit = input<string | null>(null);
  readonly tone = input<'primary' | 'accent'>('primary');

  protected readonly showTable = signal(false);
  protected readonly hovered = signal<number | null>(null);

  protected readonly viewW = VIEW_W;
  protected readonly viewH = PLOT_H + AXIS_H + PAD_T;
  protected readonly plotBottom = PLOT_H + PAD_T;

  /** Rounded to a clean number so the axis reads 0 / 50 / 100, never 0 / 47 / 94. */
  protected readonly maxValue = computed(() => {
    const highest = Math.max(0, ...this.points().map((point) => point.value));
    if (highest <= 0) {
      return 1;
    }
    const magnitude = 10 ** Math.floor(Math.log10(highest));
    return Math.ceil(highest / magnitude) * magnitude;
  });

  protected readonly ticks = computed(() => {
    const max = this.maxValue();
    return [0, max / 2, max].map((value) => ({
      value,
      label: formatCompact(value),
      y: this.plotBottom - (value / max) * PLOT_H,
    }));
  });

  protected readonly columns = computed<Column[]>(() => {
    const points = this.points();
    if (!points.length) {
      return [];
    }

    const max = this.maxValue();
    const band = (VIEW_W - PAD_L - PAD_R) / points.length;
    // 2px of the band is surrendered to the surface gap between neighbours.
    const width = Math.min(MAX_BAR_W, Math.max(4, band - 2));

    return points.map((point, index) => {
      const hitX = PAD_L + band * index;
      const centre = hitX + band / 2;
      const height = max > 0 ? (Math.max(0, point.value) / max) * PLOT_H : 0;
      const x = centre - width / 2;
      const y = this.plotBottom - height;

      return {
        index,
        label: point.label,
        value: point.value,
        detail: point.detail,
        x,
        y,
        width,
        height,
        path: roundedTopBar(x, y, width, height),
        hitX,
        hitWidth: band,
        centre,
      };
    });
  });

  /** Polyline through the column centres — the line chart shares the geometry. */
  protected readonly linePath = computed(() =>
    this.columns()
      .map((column, index) => `${index === 0 ? 'M' : 'L'}${column.centre} ${column.y}`)
      .join(' ')
  );

  protected readonly areaPath = computed(() => {
    const columns = this.columns();
    if (columns.length < 2) {
      return '';
    }
    const first = columns[0]!;
    const last = columns[columns.length - 1]!;
    return `${this.linePath()} L${last.centre} ${this.plotBottom} L${first.centre} ${this.plotBottom} Z`;
  });

  protected readonly endPoint = computed(() => this.columns().at(-1) ?? null);

  protected readonly isEmpty = computed(() =>
    this.points().every((point) => point.value === 0)
  );

  /** Only every nth label is drawn once they would collide. */
  protected readonly labelStep = computed(() => {
    const count = this.points().length;
    return count <= 8 ? 1 : Math.ceil(count / 8);
  });

  protected readonly hoveredColumn = computed(() => {
    const index = this.hovered();
    return index === null ? null : (this.columns()[index] ?? null);
  });

  protected toggleTable(): void {
    this.showTable.update((value) => !value);
  }

  protected formatValue(value: number): string {
    const unit = this.unit();
    return unit ? `${formatCompact(value)} ${unit}` : formatCompact(value);
  }

  protected showLabel(index: number): boolean {
    return index % this.labelStep() === 0;
  }
}

/** A bar with a 4px rounded cap and square corners at the baseline. */
function roundedTopBar(x: number, y: number, width: number, height: number): string {
  if (height <= 0) {
    return '';
  }
  const radius = Math.min(4, width / 2, height);
  return (
    `M${x} ${y + height}` +
    `L${x} ${y + radius}` +
    `A${radius} ${radius} 0 0 1 ${x + radius} ${y}` +
    `L${x + width - radius} ${y}` +
    `A${radius} ${radius} 0 0 1 ${x + width} ${y + radius}` +
    `L${x + width} ${y + height}Z`
  );
}

function formatCompact(value: number): string {
  if (Math.abs(value) >= 1_000_000) {
    return `${trim(value / 1_000_000)}M`;
  }
  if (Math.abs(value) >= 10_000) {
    return `${trim(value / 1000)}K`;
  }
  return new Intl.NumberFormat(undefined, { maximumFractionDigits: 2 }).format(value);
}

function trim(value: number): string {
  return Number(value.toFixed(1)).toString();
}
