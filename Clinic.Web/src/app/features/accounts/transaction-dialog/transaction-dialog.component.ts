import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';

import { AccountsStore, InvoiceWithBalance } from '../../../core/data/accounts.store';
import { ClinicSettingsStore } from '../../../core/data/clinic-settings.store';
import {
  EXPENSE_CATEGORY_LABELS,
  ExpenseCategory,
  PAYMENT_METHOD_LABELS,
  PaymentMethod,
} from '../../../core/data/finance.model';
import { AuthService } from '../../../core/services/auth.service';
import { NotificationService } from '../../../core/services/notification.service';
import { toDateOnly } from '../../../core/utils/date.util';
import { positiveAmountValidator } from '../../../core/utils/validators';
import { FieldErrorComponent } from '../../../shared/ui/field-error/field-error.component';
import { IconComponent } from '../../../shared/ui/icon/icon.component';

export type TransactionKind = 'payment' | 'expense' | 'refund';

export interface TransactionDialogData {
  kind: TransactionKind;
  invoices: InvoiceWithBalance[];
  /** Preselected invoice, when opened from an invoice row. */
  invoiceId?: string;
}

/**
 * One dialog for the three money movements.
 *
 * Payments, expenses and refunds share the same skeleton — an amount, a date, a
 * note, and who recorded it — and differ by one or two fields. Three near-identical
 * dialogs would drift apart on validation and formatting; this keeps the money
 * rules in one place and switches only the fields that genuinely differ.
 */
@Component({
  selector: 'app-transaction-dialog',
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatDatepickerModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    TranslatePipe,
    FieldErrorComponent,
    IconComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './transaction-dialog.component.html',
  styleUrl: './transaction-dialog.component.scss',
})
export class TransactionDialogComponent {
  private readonly auth = inject(AuthService);
  private readonly formBuilder = inject(FormBuilder);
  private readonly notifications = inject(NotificationService);
  private readonly settings = inject(ClinicSettingsStore);
  private readonly store = inject(AccountsStore);
  private readonly translate = inject(TranslateService);

  readonly dialogRef = inject<MatDialogRef<TransactionDialogComponent, boolean>>(MatDialogRef);
  readonly data = inject<TransactionDialogData>(MAT_DIALOG_DATA);

  protected readonly kind = this.data.kind;
  protected readonly currency = this.settings.settings().currency;
  protected readonly submitted = signal(false);

  protected readonly methods = Object.entries(PAYMENT_METHOD_LABELS) as [PaymentMethod, string][];
  protected readonly categories = Object.entries(EXPENSE_CATEGORY_LABELS) as [
    ExpenseCategory,
    string,
  ][];

  protected readonly title = computed(() =>
    this.kind === 'payment'
      ? 'accounts.newPayment'
      : this.kind === 'expense'
        ? 'accounts.newExpense'
        : 'accounts.newRefund'
  );

  /** Refunds may only target an invoice that actually took money. */
  protected readonly selectableInvoices = computed(() =>
    this.kind === 'refund'
      ? this.data.invoices.filter((invoice) => invoice.paid > 0)
      : this.data.invoices.filter((invoice) => invoice.remaining > 0)
  );

  protected readonly form = this.formBuilder.nonNullable.group({
    invoiceId: this.formBuilder.control<string | null>(this.data.invoiceId ?? null),
    amount: this.formBuilder.control<number | null>(null, [
      Validators.required,
      positiveAmountValidator,
    ]),
    occurredAt: this.formBuilder.control<Date | null>(new Date(), [Validators.required]),
    method: this.formBuilder.nonNullable.control<PaymentMethod>('cash'),
    category: this.formBuilder.nonNullable.control<ExpenseCategory>('supplies'),
    reference: ['', [Validators.maxLength(60)]],
    description: ['', [Validators.maxLength(160)]],
    vendor: ['', [Validators.maxLength(80)]],
    reason: ['', [Validators.maxLength(200)]],
  });

  protected readonly selectedInvoice = computed(() =>
    this.data.invoices.find((invoice) => invoice.id === this.invoiceIdValue()) ?? null
  );

  private readonly invoiceIdValue = signal(this.form.controls.invoiceId.value);

  constructor() {
    this.form.controls.invoiceId.valueChanges.subscribe((value) => {
      this.invoiceIdValue.set(value);

      // Default the amount to what is actually outstanding (or refundable):
      // it is the value staff intend the overwhelming majority of the time.
      const invoice = this.data.invoices.find((entry) => entry.id === value);
      if (invoice) {
        this.form.controls.amount.setValue(
          this.kind === 'refund' ? invoice.paid : invoice.remaining
        );
      }
    });

    if (this.kind !== 'expense') {
      this.form.controls.invoiceId.addValidators(Validators.required);
    } else {
      this.form.controls.description.addValidators(Validators.required);
    }

    if (this.kind === 'refund') {
      this.form.controls.reason.addValidators(Validators.required);
    }
  }

  protected submit(): void {
    this.submitted.set(true);

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const raw = this.form.getRawValue();
    const amount = Number(raw.amount);
    const recordedBy = this.auth.currentUser?.displayName ?? '';
    const invoice = this.selectedInvoice();

    if (this.kind === 'payment') {
      // Overpaying is almost always a typo, and it silently corrupts every
      // revenue figure downstream.
      if (invoice && amount > invoice.remaining + 0.005) {
        this.form.controls.amount.setErrors({ max: { max: invoice.remaining } });
        return;
      }

      this.store.recordPayment({
        invoiceId: invoice!.id,
        invoiceNumber: invoice!.number,
        patientName: invoice!.patientName,
        amount,
        method: raw.method,
        reference: raw.reference.trim() || undefined,
        paidAt: new Date(raw.occurredAt!).toISOString(),
        recordedBy,
      });
    } else if (this.kind === 'refund') {
      if (invoice && amount > invoice.paid + 0.005) {
        this.form.controls.amount.setErrors({ max: { max: invoice.paid } });
        return;
      }

      this.store.recordRefund({
        invoiceId: invoice!.id,
        invoiceNumber: invoice!.number,
        patientName: invoice!.patientName,
        amount,
        reason: raw.reason.trim(),
        refundedAt: new Date(raw.occurredAt!).toISOString(),
        recordedBy,
      });
    } else {
      this.store.recordExpense({
        description: raw.description.trim(),
        category: raw.category,
        vendor: raw.vendor.trim() || undefined,
        amount,
        spentAt: toDateOnly(raw.occurredAt!),
        recordedBy,
      });
    }

    this.notifications.success(this.translate.instant('settings.saved'));
    this.dialogRef.close(true);
  }
}
