export type ChipTone = 'primary' | 'success' | 'warning' | 'danger' | 'neutral' | 'info';

export interface TableColumn<T> {
  /** Unique column id (also used as the displayedColumns key). */
  key: string;
  header: string;
  /** Cell text accessor. */
  value: (row: T) => string | number;
  /** When set, the header becomes clickable and emits this key on sort. */
  sortKey?: string;
  align?: 'start' | 'end' | 'center';
  variant?: 'text' | 'strong' | 'chip';
  /** Required when variant === 'chip'. */
  chip?: (row: T) => { label: string; tone: ChipTone };
}

export interface TableRowAction<T> {
  id: string;
  icon: string;
  tooltip: string;
  color?: 'primary' | 'accent' | 'warn';
  /** Hide the action for specific rows. */
  visible?: (row: T) => boolean;
}

export type SortDirection = 'asc' | 'desc' | '';

export interface SortState {
  active: string;
  direction: SortDirection;
}

export interface RowActionEvent<T> {
  action: string;
  row: T;
}
