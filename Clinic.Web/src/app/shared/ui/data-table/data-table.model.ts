import { Directive, TemplateRef, inject, input } from '@angular/core';

import { IconName } from '../icon/icon.registry';

export type BadgeTone =
  | 'primary'
  | 'secondary'
  | 'info'
  | 'success'
  | 'warning'
  | 'danger'
  | 'neutral';

export interface BadgeValue {
  label: string;
  tone: BadgeTone;
  /** Render a leading status dot — for live states like Today / Available. */
  dot?: boolean;
}

export type ColumnVariant = 'text' | 'strong' | 'muted' | 'mono' | 'badge' | 'custom';

export interface TableColumn<T> {
  /** Unique key; also the slot name for a `*uiCell` template. */
  key: string;
  /** Translation key for the header. */
  header: string;
  /** Cell text. Required for every variant except `custom`. */
  value?: (row: T) => string | number | null | undefined;
  /** Secondary line under the value — e.g. a phone number under a name. */
  secondary?: (row: T) => string | number | null | undefined;
  /** Set to make the header sortable; emitted verbatim in `sortChange`. */
  sortKey?: string;
  align?: 'start' | 'end' | 'center';
  variant?: ColumnVariant;
  /** Required when `variant === 'badge'`. */
  badge?: (row: T) => BadgeValue | null;
  /** CSS width, e.g. `'160px'` or `'22%'`. */
  width?: string;
  /** Hide the column below this breakpoint to keep narrow screens readable. */
  hideBelow?: 'sm' | 'md' | 'lg';
}

export interface TableRowAction<T> {
  id: string;
  icon: IconName;
  /** Translation key for the tooltip / menu label. */
  label: string;
  tone?: 'default' | 'danger';
  /** Hide the action for rows it does not apply to. */
  visible?: (row: T) => boolean;
  disabled?: (row: T) => boolean;
}

export type SortDirection = 'asc' | 'desc';

export interface SortState {
  key: string;
  direction: SortDirection;
}

export interface PageState {
  pageIndex: number;
  pageSize: number;
}

export interface RowActionEvent<T> {
  action: string;
  row: T;
}

/**
 * Supplies a custom cell renderer for one column:
 *
 *   <ng-template uiCell="patient" let-row>
 *     <a [routerLink]="['/patients', row.id]">{{ row.name }}</a>
 *   </ng-template>
 *
 * This is what keeps the table generic — anything richer than text, a badge or
 * a two-line cell is expressed by the page, not by growing the table's API.
 */
@Directive({
  selector: '[uiCell]',
})
export class CellTemplateDirective {
  readonly uiCell = input.required<string>();
  readonly template = inject<TemplateRef<{ $implicit: any; index?: number }>>(TemplateRef);
}
