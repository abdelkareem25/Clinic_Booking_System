import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormArray, FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { MatButtonModule } from '@angular/material/button';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTooltipModule } from '@angular/material/tooltip';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';

import { AccountsStore } from '../../../core/data/accounts.store';
import { ClinicSettingsStore } from '../../../core/data/clinic-settings.store';
import { Invoice, round2 } from '../../../core/data/finance.model';
import { Doctor } from '../../../core/models/doctor.model';
import { Patient } from '../../../core/models/patient.model';
import { NotificationService } from '../../../core/services/notification.service';
import { toDateOnly } from '../../../core/utils/date.util';
import { FieldErrorComponent } from '../../../shared/ui/field-error/field-error.component';
import { IconComponent } from '../../../shared/ui/icon/icon.component';

export interface InvoiceDialogData {
  patients: Patient[];
  doctors: Doctor[];
  invoice?: Invoice;
}

@Component({
  selector: 'app-invoice-form-dialog',
  imports: [
    ReactiveFormsModule,
    MatAutocompleteModule,
    MatButtonModule,
    MatDatepickerModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatTooltipModule,
    TranslatePipe,
    FieldErrorComponent,
    IconComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './invoice-form-dialog.component.html',
  styleUrl: './invoice-form-dialog.component.scss',
})
export class InvoiceFormDialogComponent {
  private readonly formBuilder = inject(FormBuilder);
  private readonly notifications = inject(NotificationService);
  private readonly settings = inject(ClinicSettingsStore);
  private readonly store = inject(AccountsStore);
  private readonly translate = inject(TranslateService);

  readonly dialogRef = inject<MatDialogRef<InvoiceFormDialogComponent, boolean>>(MatDialogRef);
  readonly data = inject<InvoiceDialogData>(MAT_DIALOG_DATA);

  protected readonly isEdit = Boolean(this.data.invoice);
  protected readonly submitted = signal(false);
  protected readonly currency = this.settings.settings().currency;

  protected readonly form = this.formBuilder.nonNullable.group({
    patientId: this.formBuilder.control<number | null>(this.data.invoice?.patientId ?? null, [
      Validators.required,
    ]),
    doctorId: this.formBuilder.control<number | null>(this.data.invoice?.doctorId ?? null),
    issuedAt: this.formBuilder.control<Date | null>(
      this.data.invoice ? new Date(this.data.invoice.issuedAt) : new Date(),
      [Validators.required]
    ),
    dueAt: this.formBuilder.control<Date | null>(
      this.data.invoice ? new Date(this.data.invoice.dueAt) : new Date(),
      [Validators.required]
    ),
    discount: [this.data.invoice?.discount ?? 0, [Validators.min(0)]],
    taxRate: [this.data.invoice?.taxRate ?? this.settings.settings().taxRate, [
      Validators.min(0),
      Validators.max(100),
    ]],
    notes: [this.data.invoice?.notes ?? '', [Validators.maxLength(300)]],
    items: this.formBuilder.array(
      (this.data.invoice?.items ?? [{ description: '', quantity: 1, unitPrice: 0 }]).map((item) =>
        this.createItem(item.description, item.quantity, item.unitPrice)
      )
    ),
  });

  /** Mirrors the item array into a signal so totals recompute as you type. */
  protected readonly itemsValue = signal(this.form.controls.items.getRawValue());

  protected readonly subtotal = computed(() =>
    round2(
      this.itemsValue().reduce(
        (sum, item) => sum + (Number(item.quantity) || 0) * (Number(item.unitPrice) || 0),
        0
      )
    )
  );

  protected readonly discountValue = signal(this.form.controls.discount.value);
  protected readonly taxRateValue = signal(this.form.controls.taxRate.value);

  protected readonly taxAmount = computed(() =>
    round2(Math.max(0, this.subtotal() - (this.discountValue() || 0)) * ((this.taxRateValue() || 0) / 100))
  );

  protected readonly total = computed(() =>
    round2(Math.max(0, this.subtotal() - (this.discountValue() || 0)) + this.taxAmount())
  );

  constructor() {
    this.form.controls.items.valueChanges.subscribe(() =>
      this.itemsValue.set(this.form.controls.items.getRawValue())
    );
    this.form.controls.discount.valueChanges.subscribe((value) => this.discountValue.set(value));
    this.form.controls.taxRate.valueChanges.subscribe((value) => this.taxRateValue.set(value));
  }

  protected get items(): FormArray {
    return this.form.controls.items;
  }

  protected addItem(): void {
    this.items.push(this.createItem('', 1, 0));
  }

  protected removeItem(index: number): void {
    // An invoice with no lines has no total; keep at least one row.
    if (this.items.length > 1) {
      this.items.removeAt(index);
    }
  }

  protected submit(): void {
    this.submitted.set(true);

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const raw = this.form.getRawValue();
    const patient = this.data.patients.find((entry) => entry.id === raw.patientId);
    const doctor = this.data.doctors.find((entry) => entry.id === raw.doctorId);

    const items = raw.items
      .filter((item) => item.description.trim() && Number(item.quantity) > 0)
      .map((item) => ({
        description: item.description.trim(),
        quantity: Number(item.quantity),
        unitPrice: Number(item.unitPrice),
      }));

    if (!items.length) {
      this.notifications.error(this.translate.instant('validation.required'));
      return;
    }

    const payload = {
      patientId: raw.patientId!,
      patientName: patient?.name ?? '',
      doctorId: doctor?.id,
      doctorName: doctor?.name,
      issuedAt: toDateOnly(raw.issuedAt!),
      dueAt: toDateOnly(raw.dueAt!),
      items,
      discount: Number(raw.discount) || 0,
      taxRate: Number(raw.taxRate) || 0,
      notes: raw.notes.trim() || undefined,
      status: 'unpaid' as const,
    };

    if (this.data.invoice) {
      this.store.updateInvoice(this.data.invoice.id, payload);
    } else {
      this.store.createInvoice(payload);
    }

    this.notifications.success(this.translate.instant('settings.saved'));
    this.dialogRef.close(true);
  }

  private createItem(description: string, quantity: number, unitPrice: number) {
    return this.formBuilder.nonNullable.group({
      description: [description, [Validators.required, Validators.maxLength(120)]],
      quantity: [quantity, [Validators.required, Validators.min(1)]],
      unitPrice: [unitPrice, [Validators.required, Validators.min(0)]],
    });
  }
}
