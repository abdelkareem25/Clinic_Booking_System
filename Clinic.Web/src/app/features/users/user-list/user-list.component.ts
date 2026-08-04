import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { catchError, of } from 'rxjs';

import { PermissionService } from '../../../core/authz/permission.service';
import {
  ACCOUNT_ROLES,
  Account,
  AccountStatusFilter,
} from '../../../core/models/account.model';
import { AccountsService } from '../../../core/services/accounts.service';
import { AuthService } from '../../../core/services/auth.service';
import { JwtService } from '../../../core/services/jwt.service';
import { NotificationService } from '../../../core/services/notification.service';
import { confirmDialog } from '../../../shared/ui/confirm-dialog/confirm-dialog.component';
import { DataTableComponent } from '../../../shared/ui/data-table/data-table.component';
import {
  CellTemplateDirective,
  PageState,
  RowActionEvent,
  SortState,
  TableColumn,
  TableRowAction,
} from '../../../shared/ui/data-table/data-table.model';
import { IconComponent } from '../../../shared/ui/icon/icon.component';
import { PageHeaderComponent } from '../../../shared/ui/page-header/page-header.component';
import {
  SearchEvent,
  SearchFieldComponent,
} from '../../../shared/ui/search-field/search-field.component';
import {
  AccountDialogData,
  AccountFormDialogComponent,
} from '../dialogs/account-form-dialog.component';

const EMPTY_PAGE = { pageIndex: 1, pageSize: 0, count: 0, data: [] as Account[] };

/**
 * Staff account administration.
 *
 * The roster is now a real endpoint (`GET /api/Accounts`), so this screen pages,
 * sorts and filters on the server rather than showing only the signed-in user
 * as it used to. Search, role and status are all query parameters: the list can
 * outgrow one page, and filtering a single page client-side would silently
 * search only that page.
 */
@Component({
  selector: 'app-user-list',
  imports: [
    DatePipe,
    MatButtonModule,
    MatFormFieldModule,
    MatSelectModule,
    TranslatePipe,
    CellTemplateDirective,
    DataTableComponent,
    IconComponent,
    PageHeaderComponent,
    SearchFieldComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './user-list.component.html',
  styleUrl: './user-list.component.scss',
})
export class UserListComponent {
  private readonly api = inject(AccountsService);
  private readonly auth = inject(AuthService);
  private readonly dialog = inject(MatDialog);
  private readonly jwt = inject(JwtService);
  private readonly notifications = inject(NotificationService);
  private readonly translate = inject(TranslateService);

  protected readonly permissions = inject(PermissionService);

  protected readonly rows = signal<Account[]>([]);
  protected readonly total = signal(0);
  protected readonly loading = signal(true);

  protected readonly search = signal<SearchEvent | null>(null);
  protected readonly role = signal<string | 'all'>('all');
  protected readonly status = signal<AccountStatusFilter>('all');
  protected readonly page = signal<PageState>({ pageIndex: 1, pageSize: 10 });
  protected readonly sort = signal<SortState>({ key: 'displayName', direction: 'asc' });

  protected readonly roles = ACCOUNT_ROLES;

  /**
   * The signed-in account's id.
   *
   * Used to hide the destructive actions on your own row. The API refuses them
   * too — this only saves the round trip and makes the reason visible.
   */
  protected readonly currentUserId = signal(
    this.jwt.getUserId(this.auth.currentUser?.token)
  );

  protected readonly hasFilters = computed(
    () => Boolean(this.search()?.term) || this.role() !== 'all' || this.status() !== 'all'
  );

  protected readonly columns: TableColumn<Account>[] = [
    {
      key: 'displayName',
      header: 'users.name',
      sortKey: 'displayName',
      value: (row) => row.displayName,
      variant: 'custom',
    },
    {
      key: 'userName',
      header: 'users.username',
      value: (row) => row.userName,
      variant: 'mono',
      hideBelow: 'lg',
    },
    { key: 'email', header: 'users.email', value: (row) => row.email, hideBelow: 'md' },
    { key: 'phoneNumber', header: 'users.phone', value: (row) => row.phoneNumber, hideBelow: 'lg' },
    {
      key: 'role',
      header: 'users.role',
      variant: 'badge',
      width: '140px',
      badge: (row) => ({ label: `users.role${row.role}`, tone: 'primary' }),
    },
    {
      key: 'status',
      header: 'users.status',
      variant: 'badge',
      width: '130px',
      badge: (row) => {
        // Locked out is shown ahead of inactive: it is temporary and clears on
        // its own, so an administrator seeing "Inactive" would reach for the
        // wrong remedy.
        if (row.isLockedOut) {
          return { label: 'users.lockedOut', tone: 'warning', dot: true };
        }
        return row.isActive
          ? { label: 'users.active', tone: 'success', dot: true }
          : { label: 'users.inactive', tone: 'neutral' };
      },
    },
    {
      key: 'createdAtUtc',
      header: 'users.created',
      sortKey: 'createdAtUtc',
      value: (row) => row.createdAtUtc,
      variant: 'custom',
      width: '150px',
      hideBelow: 'md',
    },
  ];

  protected readonly rowActions: TableRowAction<Account>[] = [
    {
      id: 'edit',
      icon: 'edit',
      label: 'common.edit',
      visible: () => this.permissions.can('users.edit'),
    },
    {
      id: 'delete',
      icon: 'delete',
      label: 'common.delete',
      tone: 'danger',
      visible: (row) => this.permissions.can('users.delete') && row.id !== this.currentUserId(),
    },
  ];

  constructor() {
    this.load();
  }

  protected load(): void {
    this.loading.set(true);

    this.api
      .getAccounts({
        ...this.page(),
        search: this.search()?.term,
        role: this.role() === 'all' ? undefined : this.role(),
        status: this.status() === 'all' ? undefined : (this.status() as 'active' | 'inactive'),
        sort: this.sortParam(),
      })
      // List endpoints answer 404 when empty — the house convention, wrapped at
      // every call site.
      .pipe(catchError(() => of(EMPTY_PAGE)))
      .subscribe({
        next: (result) => {
          this.rows.set(result.data);
          this.total.set(result.count);
          this.loading.set(false);
        },
        error: () => {
          this.rows.set([]);
          this.total.set(0);
          this.loading.set(false);
        },
      });
  }

  /** Maps the table's sort state onto the API's `Sort` vocabulary. */
  private sortParam(): string {
    const { key, direction } = this.sort();

    if (key === 'createdAtUtc') {
      return direction === 'desc' ? 'CreatedDesc' : 'CreatedAsc';
    }

    return direction === 'desc' ? 'NameDesc' : 'NameAsc';
  }

  protected onSearch(event: SearchEvent): void {
    this.search.set(event.term ? event : null);
    this.resetPage();
    this.load();
  }

  protected onRole(value: string | 'all'): void {
    this.role.set(value);
    this.resetPage();
    this.load();
  }

  protected onStatus(value: AccountStatusFilter): void {
    this.status.set(value);
    this.resetPage();
    this.load();
  }

  protected onSort(sort: SortState): void {
    this.sort.set(sort);
    this.load();
  }

  protected onPage(page: PageState): void {
    this.page.set(page);
    this.load();
  }

  protected clearFilters(): void {
    this.search.set(null);
    this.role.set('all');
    this.status.set('all');
    this.resetPage();
    this.load();
  }

  protected openForm(account?: Account): void {
    this.dialog
      .open<AccountFormDialogComponent, AccountDialogData, boolean>(AccountFormDialogComponent, {
        data: { account, currentUserId: this.currentUserId() },
      })
      .afterClosed()
      .subscribe((saved) => {
        if (saved) {
          this.load();
        }
      });
  }

  protected onRowAction(event: RowActionEvent<Account>): void {
    switch (event.action) {
      case 'edit':
        this.openForm(event.row);
        break;
      case 'delete':
        this.confirmDelete(event.row);
        break;
    }
  }

  protected initialsOf(account: Account): string {
    const parts = account.displayName.trim().split(/\s+/).filter(Boolean);
    return ((parts[0]?.[0] ?? '') + (parts[1]?.[0] ?? '')).toUpperCase() || '—';
  }

  private confirmDelete(account: Account): void {
    confirmDialog(this.dialog, {
      title: 'users.delete',
      message: 'users.deleteConfirm',
      messageParams: { name: account.displayName },
      confirmLabel: 'common.delete',
      tone: 'danger',
    }).subscribe((confirmed) => {
      if (!confirmed) {
        return;
      }

      this.api.deleteAccount(account.id).subscribe({
        next: () => {
          this.notifications.success(this.translate.instant('users.deleted'));
          this.load();
        },
      });
    });
  }

  private resetPage(): void {
    this.page.update((page) => ({ ...page, pageIndex: 1 }));
  }
}
