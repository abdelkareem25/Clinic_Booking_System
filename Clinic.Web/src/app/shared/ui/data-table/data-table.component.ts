import { NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, contentChildren, input, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatMenuModule } from '@angular/material/menu';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatTooltipModule } from '@angular/material/tooltip';
import { TranslatePipe } from '@ngx-translate/core';

import { IconComponent } from '../icon/icon.component';
import { EmptyStateComponent } from '../empty-state/empty-state.component';
import {
  CellTemplateDirective,
  PageState,
  RowActionEvent,
  SortState,
  TableColumn,
  TableRowAction,
} from './data-table.model';

/**
 * The one table in this application.
 *
 * Sticky header, sortable headers, row actions, a skeleton while loading, a
 * real empty state, and a card layout below the `md` breakpoint so a table is
 * still usable on a phone at the front desk. Every list screen uses it, which
 * is what makes sorting, spacing and pagination behave identically everywhere.
 *
 * Data is *not* sorted or paged here: the parent owns the query and reacts to
 * `sortChange` / `pageChange`. That keeps client-side lists and server-paged
 * endpoints on exactly the same contract.
 */
@Component({
  selector: 'ui-data-table',
  imports: [
    NgTemplateOutlet,
    MatButtonModule,
    MatMenuModule,
    MatPaginatorModule,
    MatTooltipModule,
    TranslatePipe,
    IconComponent,
    EmptyStateComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './data-table.component.html',
  styleUrl: './data-table.component.scss',
})
export class DataTableComponent<T extends { id: string | number }> {
  readonly columns = input.required<TableColumn<T>[]>();
  readonly rows = input<readonly T[]>([]);
  readonly loading = input(false);
  readonly rowActions = input<TableRowAction<T>[]>([]);
  readonly sort = input<SortState | null>(null);
  readonly page = input<PageState | null>(null);
  readonly total = input(0);
  readonly pageSizeOptions = input<number[]>([10, 25, 50, 100]);
  readonly clickableRows = input(false);
  readonly emptyTitle = input('common.noResults');
  readonly emptyMessage = input<string | null>(null);
  readonly emptyIcon = input<'empty' | 'noResults'>('empty');
  /** Skeleton row count while `loading` and no rows are held yet. */
  readonly skeletonRows = input(6);

  readonly sortChange = output<SortState>();
  readonly pageChange = output<PageState>();
  readonly rowClick = output<T>();
  readonly action = output<RowActionEvent<T>>();

  private readonly cellTemplates = contentChildren(CellTemplateDirective);

  protected readonly templateMap = computed(() => {
    const map = new Map<string, CellTemplateDirective>();
    for (const directive of this.cellTemplates()) {
      map.set(directive.uiCell(), directive);
    }
    return map;
  });

  protected readonly hasActions = computed(() => this.rowActions().length > 0);

  protected readonly showSkeleton = computed(() => this.loading() && this.rows().length === 0);

  protected readonly showEmpty = computed(() => !this.loading() && this.rows().length === 0);

  protected readonly skeletonIndices = computed(() =>
    Array.from({ length: this.skeletonRows() }, (_, index) => index)
  );

  protected readonly columnCount = computed(
    () => this.columns().length + (this.hasActions() ? 1 : 0)
  );

  protected onSort(column: TableColumn<T>): void {
    const key = column.sortKey;
    if (!key) {
      return;
    }

    const current = this.sort();
    const direction =
      current?.key === key && current.direction === 'asc' ? 'desc' : 'asc';

    this.sortChange.emit({ key, direction });
  }

  protected sortDirectionFor(column: TableColumn<T>): 'asc' | 'desc' | null {
    const current = this.sort();
    return current && column.sortKey === current.key ? current.direction : null;
  }

  protected ariaSortFor(column: TableColumn<T>): string | null {
    if (!column.sortKey) {
      return null;
    }
    const direction = this.sortDirectionFor(column);
    return direction === 'asc' ? 'ascending' : direction === 'desc' ? 'descending' : 'none';
  }

  protected onPage(event: PageEvent): void {
    // Material's paginator is zero-based; the API and every service here are
    // one-based, so the boundary is converted in exactly this one place.
    this.pageChange.emit({ pageIndex: event.pageIndex + 1, pageSize: event.pageSize });
  }

  protected onRowClick(row: T): void {
    if (this.clickableRows()) {
      this.rowClick.emit(row);
    }
  }

  protected onRowKeydown(event: KeyboardEvent, row: T): void {
    if (!this.clickableRows()) {
      return;
    }
    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      this.rowClick.emit(row);
    }
  }

  protected visibleActions(row: T): TableRowAction<T>[] {
    return this.rowActions().filter((action) => action.visible?.(row) ?? true);
  }

  protected cellText(column: TableColumn<T>, row: T): string {
    const value = column.value?.(row);
    return value === null || value === undefined || value === '' ? '—' : String(value);
  }

  protected secondaryText(column: TableColumn<T>, row: T): string | null {
    const value = column.secondary?.(row);
    return value === null || value === undefined || value === '' ? null : String(value);
  }

  protected trackRow = (_: number, row: T) => row.id;
}
