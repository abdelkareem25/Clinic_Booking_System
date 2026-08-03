import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatTabsModule } from '@angular/material/tabs';
import { MatTooltipModule } from '@angular/material/tooltip';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { catchError, forkJoin, of } from 'rxjs';

import { PermissionService } from '../../core/authz/permission.service';
import { AccountsStore, InvoiceWithBalance } from '../../core/data/accounts.store';
import { ClinicSettingsStore } from '../../core/data/clinic-settings.store';
import {
  DailyClosing,
  EXPENSE_CATEGORY_LABELS,
  Expense,
  INVOICE_STATUS_LABELS,
  InvoiceStatus,
  PAYMENT_METHOD_LABELS,
  Payment,
  Refund,
} from '../../core/data/finance.model';
import { Doctor } from '../../core/models/doctor.model';
import { Patient } from '../../core/models/patient.model';
import { AuthService } from '../../core/services/auth.service';
import { DoctorsService } from '../../core/services/doctors.service';
import { NotificationService } from '../../core/services/notification.service';
import { PatientsService } from '../../core/services/patients.service';
import { addDays, startOfDay, startOfMonth } from '../../core/utils/date.util';
import { CardComponent } from '../../shared/ui/card/card.component';
import { ChartComponent, ChartPoint } from '../../shared/ui/chart/chart.component';
import { confirmDialog } from '../../shared/ui/confirm-dialog/confirm-dialog.component';
import { DataTableComponent } from '../../shared/ui/data-table/data-table.component';
import {
  BadgeTone,
  RowActionEvent,
  TableColumn,
  TableRowAction,
} from '../../shared/ui/data-table/data-table.model';
import { EmptyStateComponent } from '../../shared/ui/empty-state/empty-state.component';
import { IconComponent } from '../../shared/ui/icon/icon.component';
import { PageHeaderComponent } from '../../shared/ui/page-header/page-header.component';
import { StatCardComponent } from '../../shared/ui/stat-card/stat-card.component';
import {
  InvoiceDialogData,
  InvoiceFormDialogComponent,
} from './invoice-form-dialog/invoice-form-dialog.component';
import {
  TransactionDialogComponent,
  TransactionDialogData,
  TransactionKind,
} from './transaction-dialog/transaction-dialog.component';

const EMPTY_PAGE = { pageIndex: 1, pageSize: 0, count: 0, data: [] };
const TREND_DAYS = 14;

const STATUS_TONES: Record<InvoiceStatus, BadgeTone> = {
  draft: 'neutral',
  unpaid: 'danger',
  partial: 'warning',
  paid: 'success',
  refunded: 'info',
  void: 'neutral',
};

@Component({
  selector: 'app-accounts',
  imports: [
    MatButtonModule,
    MatTabsModule,
    MatTooltipModule,
    TranslatePipe,
    CardComponent,
    ChartComponent,
    DataTableComponent,
    EmptyStateComponent,
    IconComponent,
    PageHeaderComponent,
    StatCardComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './accounts.component.html',
  styleUrl: './accounts.component.scss',
})
export class AccountsComponent {
  private readonly auth = inject(AuthService);
  private readonly dialog = inject(MatDialog);
  private readonly doctorsApi = inject(DoctorsService);
  private readonly notifications = inject(NotificationService);
  private readonly patientsApi = inject(PatientsService);
  private readonly translate = inject(TranslateService);

  protected readonly permissions = inject(PermissionService);
  protected readonly settings = inject(ClinicSettingsStore);
  protected readonly store = inject(AccountsStore);

  protected readonly patients = signal<Patient[]>([]);
  protected readonly doctors = signal<Doctor[]>([]);

  protected readonly today = new Date();
  protected readonly currency = computed(() => this.settings.settings().currency);

  // -------------------------------------------------------------- figures --

  protected readonly monthTotals = computed(() =>
    this.store.totalsBetween(startOfMonth(this.today), this.today)
  );

  protected readonly todayTotals = computed(() =>
    this.store.totalsBetween(this.today, this.today)
  );

  private readonly trendDays = computed(() =>
    Array.from({ length: TREND_DAYS }, (_, index) =>
      startOfDay(addDays(this.today, index - (TREND_DAYS - 1)))
    )
  );

  protected readonly revenueTrend = computed<ChartPoint[]>(() =>
    this.store.dailyIncome(this.trendDays()).map(({ date, value }) => ({
      label: `${date.getDate()}`,
      detail: date.toLocaleDateString(undefined, { day: 'numeric', month: 'short' }),
      value,
    }))
  );

  /** Expenses by category over the current month — a horizontal magnitude read. */
  protected readonly expenseBreakdown = computed<ChartPoint[]>(() => {
    const from = startOfMonth(this.today);
    const totals = new Map<string, number>();

    for (const expense of this.store.expenses()) {
      const day = new Date(expense.spentAt);
      if (day < from || day > this.today) {
        continue;
      }
      totals.set(expense.category, (totals.get(expense.category) ?? 0) + expense.amount);
    }

    return [...totals.entries()]
      .sort((a, b) => b[1] - a[1])
      .map(([category, value]) => ({
        label: this.translate.instant(
          EXPENSE_CATEGORY_LABELS[category as keyof typeof EXPENSE_CATEGORY_LABELS]
        ),
        value,
      }));
  });

  protected readonly todaysClosing = computed(() => this.store.closingFor(this.today));

  // --------------------------------------------------------------- tables --

  protected readonly invoiceColumns: TableColumn<InvoiceWithBalance>[] = [
    { key: 'number', header: 'accounts.invoiceNumber', value: (row) => row.number, variant: 'mono', width: '150px' },
    { key: 'patientName', header: 'appointments.patient', value: (row) => row.patientName, variant: 'strong' },
    {
      key: 'issuedAt',
      header: 'accounts.issuedOn',
      value: (row) => new Date(row.issuedAt).toLocaleDateString(),
      width: '130px',
      hideBelow: 'md',
    },
    { key: 'total', header: 'common.total', value: (row) => this.money(row.total), align: 'end', width: '130px' },
    {
      key: 'remaining',
      header: 'accounts.remaining',
      value: (row) => this.money(row.remaining),
      align: 'end',
      width: '130px',
      hideBelow: 'sm',
    },
    {
      key: 'status',
      header: 'common.status',
      variant: 'badge',
      width: '140px',
      badge: (row) => ({
        label: INVOICE_STATUS_LABELS[row.effectiveStatus],
        tone: STATUS_TONES[row.effectiveStatus],
      }),
    },
  ];

  protected readonly invoiceActions: TableRowAction<InvoiceWithBalance>[] = [
    {
      id: 'pay',
      icon: 'payment',
      label: 'accounts.newPayment',
      visible: (row) => row.remaining > 0,
    },
    {
      id: 'refund',
      icon: 'undo',
      label: 'accounts.newRefund',
      visible: (row) => row.paid > 0,
    },
    { id: 'edit', icon: 'edit', label: 'common.edit' },
    { id: 'delete', icon: 'delete', label: 'common.delete', tone: 'danger' },
  ];

  protected readonly paymentColumns: TableColumn<Payment>[] = [
    {
      key: 'paidAt',
      header: 'common.date',
      value: (row) => new Date(row.paidAt).toLocaleDateString(),
      secondary: (row) =>
        new Date(row.paidAt).toLocaleTimeString(undefined, {
          hour: 'numeric',
          minute: '2-digit',
          hour12: true,
        }),
      width: '150px',
    },
    { key: 'invoiceNumber', header: 'accounts.invoiceNumber', value: (row) => row.invoiceNumber, variant: 'mono' },
    { key: 'patientName', header: 'appointments.patient', value: (row) => row.patientName },
    {
      key: 'method',
      header: 'accounts.paymentMethod',
      variant: 'badge',
      width: '150px',
      hideBelow: 'sm',
      badge: (row) => ({ label: PAYMENT_METHOD_LABELS[row.method], tone: 'info' }),
    },
    { key: 'amount', header: 'accounts.amount', value: (row) => this.money(row.amount), align: 'end', width: '140px' },
  ];

  protected readonly expenseColumns: TableColumn<Expense>[] = [
    {
      key: 'spentAt',
      header: 'common.date',
      value: (row) => new Date(row.spentAt).toLocaleDateString(),
      width: '140px',
    },
    { key: 'description', header: 'accounts.itemDescription', value: (row) => row.description, variant: 'strong' },
    {
      key: 'category',
      header: 'accounts.category',
      variant: 'badge',
      width: '150px',
      badge: (row) => ({ label: EXPENSE_CATEGORY_LABELS[row.category], tone: 'warning' }),
    },
    { key: 'vendor', header: 'accounts.vendor', value: (row) => row.vendor ?? '—', hideBelow: 'md' },
    { key: 'amount', header: 'accounts.amount', value: (row) => this.money(row.amount), align: 'end', width: '140px' },
  ];

  protected readonly refundColumns: TableColumn<Refund>[] = [
    {
      key: 'refundedAt',
      header: 'common.date',
      value: (row) => new Date(row.refundedAt).toLocaleDateString(),
      width: '140px',
    },
    { key: 'invoiceNumber', header: 'accounts.invoiceNumber', value: (row) => row.invoiceNumber, variant: 'mono' },
    { key: 'patientName', header: 'appointments.patient', value: (row) => row.patientName },
    { key: 'reason', header: 'accounts.refundReason', value: (row) => row.reason, hideBelow: 'md' },
    { key: 'amount', header: 'accounts.amount', value: (row) => this.money(row.amount), align: 'end', width: '140px' },
  ];

  protected readonly closingColumns: TableColumn<DailyClosing>[] = [
    { key: 'date', header: 'common.date', value: (row) => new Date(row.date).toLocaleDateString(), width: '140px' },
    { key: 'openingBalance', header: 'accounts.openingBalance', value: (row) => this.money(row.openingBalance), align: 'end' },
    { key: 'cashIn', header: 'accounts.income', value: (row) => this.money(row.cashIn), align: 'end' },
    { key: 'cashOut', header: 'accounts.expenses', value: (row) => this.money(row.cashOut), align: 'end' },
    { key: 'closingBalance', header: 'accounts.closingBalance', value: (row) => this.money(row.closingBalance), align: 'end' },
    { key: 'closedBy', header: 'accounts.closedBy', value: (row) => row.closedBy, variant: 'muted', hideBelow: 'md' },
  ];

  protected readonly deleteAction: TableRowAction<{ id: string }>[] = [
    { id: 'delete', icon: 'delete', label: 'common.delete', tone: 'danger' },
  ];

  constructor() {
    forkJoin({
      patients: this.patientsApi
        .getPatients({ pageIndex: 1, pageSize: 500 })
        .pipe(catchError(() => of(EMPTY_PAGE))),
      doctors: this.doctorsApi
        .getDoctors({ pageIndex: 1, pageSize: 200 })
        .pipe(catchError(() => of(EMPTY_PAGE))),
    }).subscribe(({ patients, doctors }) => {
      this.patients.set(patients.data as Patient[]);
      this.doctors.set(doctors.data as Doctor[]);
    });
  }

  // -------------------------------------------------------------- actions --

  protected newInvoice(invoice?: InvoiceWithBalance): void {
    this.dialog.open<InvoiceFormDialogComponent, InvoiceDialogData, boolean>(
      InvoiceFormDialogComponent,
      { data: { patients: this.patients(), doctors: this.doctors(), invoice } }
    );
  }

  protected openTransaction(kind: TransactionKind, invoiceId?: string): void {
    this.dialog.open<TransactionDialogComponent, TransactionDialogData, boolean>(
      TransactionDialogComponent,
      { data: { kind, invoices: this.store.invoices(), invoiceId } }
    );
  }

  protected onInvoiceAction(event: RowActionEvent<InvoiceWithBalance>): void {
    switch (event.action) {
      case 'pay':
        this.openTransaction('payment', event.row.id);
        break;
      case 'refund':
        this.openTransaction('refund', event.row.id);
        break;
      case 'edit':
        this.newInvoice(event.row);
        break;
      case 'delete':
        this.confirmRemove('accounts.invoice', () => this.store.removeInvoice(event.row.id));
        break;
    }
  }

  protected onPaymentAction(event: RowActionEvent<Payment>): void {
    this.confirmRemove('accounts.payment', () => this.store.removePayment(event.row.id));
  }

  protected onExpenseAction(event: RowActionEvent<Expense>): void {
    this.confirmRemove('accounts.expense', () => this.store.removeExpense(event.row.id));
  }

  protected onRefundAction(event: RowActionEvent<Refund>): void {
    this.confirmRemove('accounts.refund', () => this.store.removeRefund(event.row.id));
  }

  protected closeDay(): void {
    const previous = this.store.closings()[0];
    const opening = previous?.closingBalance ?? 0;

    this.store.closeDay(this.today, opening, this.auth.currentUser?.displayName ?? '');
    this.notifications.success(this.translate.instant('accounts.dayClosed'));
  }

  protected money(value: number): string {
    return `${value.toLocaleString(undefined, {
      minimumFractionDigits: 2,
      maximumFractionDigits: 2,
    })} ${this.currency()}`;
  }

  private confirmRemove(titleKey: string, action: () => void): void {
    confirmDialog(this.dialog, {
      title: titleKey,
      message: 'patients.deleteConfirm',
      messageParams: { name: this.translate.instant(titleKey) },
      confirmLabel: 'common.delete',
      tone: 'danger',
    }).subscribe((confirmed) => {
      if (confirmed) {
        action();
      }
    });
  }
}
