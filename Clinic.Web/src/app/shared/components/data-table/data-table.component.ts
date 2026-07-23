import {
  ChangeDetectionStrategy,
  Component,
  EventEmitter,
  Input,
  Output,
  computed,
  signal
} from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';

import { EmptyStateComponent } from '../empty-state/empty-state.component';
import {
  RowActionEvent,
  SortDirection,
  SortState,
  TableColumn,
  TableRowAction
} from './data-table.model';

/**
 * Generic, config-driven Material data table.
 * Server-side sorting is supported: sortable headers emit {@link SortState}.
 */
@Component({
  selector: 'app-data-table',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatTableModule, MatButtonModule, MatIconModule, MatTooltipModule, EmptyStateComponent],
  templateUrl: './data-table.component.html',
  styleUrl: './data-table.component.scss'
})
export class DataTableComponent<T extends { id: number | string }> {
  private readonly columnsSignal = signal<TableColumn<T>[]>([]);
  private readonly actionsSignal = signal<TableRowAction<T>[]>([]);
  private readonly sortSignal = signal<SortState>({ active: '', direction: '' });

  @Input({ required: true }) set columns(value: TableColumn<T>[]) {
    this.columnsSignal.set(value ?? []);
  }
  @Input() set actions(value: TableRowAction<T>[]) {
    this.actionsSignal.set(value ?? []);
  }
  @Input() rows: T[] = [];
  @Input() loading = false;
  @Input() emptyIcon = 'inbox';
  @Input() emptyTitle = 'No records found';
  @Input() emptyMessage = 'Try adjusting the filters or add a new record.';

  @Output() readonly rowAction = new EventEmitter<RowActionEvent<T>>();
  @Output() readonly sortChange = new EventEmitter<SortState>();

  readonly cols = computed(() => this.columnsSignal());
  readonly sort = computed(() => this.sortSignal());
  readonly hasActions = computed(() => this.actionsSignal().length > 0);

  readonly displayedColumns = computed(() => {
    const keys = this.columnsSignal().map((column) => column.key);
    return this.hasActions() ? [...keys, '__actions'] : keys;
  });

  visibleActions(row: T): TableRowAction<T>[] {
    return this.actionsSignal().filter((action) => (action.visible ? action.visible(row) : true));
  }

  onSort(column: TableColumn<T>): void {
    if (!column.sortKey) {
      return;
    }

    const current = this.sortSignal();
    let direction: SortDirection = 'asc';
    if (current.active === column.sortKey) {
      direction = current.direction === 'asc' ? 'desc' : current.direction === 'desc' ? '' : 'asc';
    }

    const next: SortState = { active: direction ? column.sortKey : '', direction };
    this.sortSignal.set(next);
    this.sortChange.emit(next);
  }

  sortIcon(column: TableColumn<T>): string {
    const current = this.sortSignal();
    if (!column.sortKey || current.active !== column.sortKey || !current.direction) {
      return 'unfold_more';
    }
    return current.direction === 'asc' ? 'arrow_upward' : 'arrow_downward';
  }

  trackByRow = (_index: number, row: T): number | string => row.id;
  trackByCol = (_index: number, column: TableColumn<T>): string => column.key;
}
