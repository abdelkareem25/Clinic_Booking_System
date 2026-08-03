import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatChipInputEvent, MatChipsModule } from '@angular/material/chips';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTimepickerModule } from '@angular/material/timepicker';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';

import {
  MedicalRecord,
  MedicalRecordsStore,
  RECORD_TYPES,
  RECORD_TYPE_META,
  RecordType,
} from '../../../core/data/medical-records.store';
import { Doctor } from '../../../core/models/doctor.model';
import { AuthService } from '../../../core/services/auth.service';
import { NotificationService } from '../../../core/services/notification.service';
import { notFutureValidator } from '../../../core/utils/validators';
import { FieldErrorComponent } from '../../../shared/ui/field-error/field-error.component';
import { IconComponent } from '../../../shared/ui/icon/icon.component';

export interface RecordDialogData {
  patientId: number;
  patientName: string;
  doctors: Doctor[];
  /** Present when editing. */
  record?: MedicalRecord;
}

@Component({
  selector: 'app-record-form-dialog',
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatChipsModule,
    MatDatepickerModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatTimepickerModule,
    TranslatePipe,
    FieldErrorComponent,
    IconComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './record-form-dialog.component.html',
  styleUrl: './record-form-dialog.component.scss',
})
export class RecordFormDialogComponent {
  private readonly auth = inject(AuthService);
  private readonly formBuilder = inject(FormBuilder);
  private readonly notifications = inject(NotificationService);
  private readonly store = inject(MedicalRecordsStore);
  private readonly translate = inject(TranslateService);

  readonly dialogRef = inject<MatDialogRef<RecordFormDialogComponent, boolean>>(MatDialogRef);
  readonly data = inject<RecordDialogData>(MAT_DIALOG_DATA);

  protected readonly recordTypes = RECORD_TYPES;
  protected readonly typeMeta = RECORD_TYPE_META;
  protected readonly maxDate = new Date();
  protected readonly isEdit = Boolean(this.data.record);

  protected readonly submitted = signal(false);
  protected readonly tags = signal<string[]>([...(this.data.record?.tags ?? [])]);

  protected readonly form = this.formBuilder.nonNullable.group({
    type: [this.data.record?.type ?? ('visit' as RecordType), [Validators.required]],
    // Split into date and time so both use a picker; neither is ever typed.
    occurredDate: this.formBuilder.control<Date | null>(
      this.data.record ? new Date(this.data.record.occurredAt) : new Date(),
      [Validators.required, notFutureValidator]
    ),
    occurredTime: this.formBuilder.control<Date | null>(
      this.data.record ? new Date(this.data.record.occurredAt) : new Date(),
      [Validators.required]
    ),
    doctorId: this.formBuilder.control<number | null>(this.data.record?.doctorId ?? null),
    title: [this.data.record?.title ?? '', [Validators.required, Validators.maxLength(120)]],
    complaint: [this.data.record?.complaint ?? '', [Validators.maxLength(500)]],
    diagnosis: [this.data.record?.diagnosis ?? '', [Validators.maxLength(500)]],
    treatment: [this.data.record?.treatment ?? '', [Validators.maxLength(500)]],
    prescription: [this.data.record?.prescription ?? '', [Validators.maxLength(1000)]],
    bloodPressure: [this.data.record?.vitals?.bloodPressure ?? '', [Validators.maxLength(12)]],
    temperature: this.formBuilder.control<number | null>(
      this.data.record?.vitals?.temperature ?? null,
      [Validators.min(30), Validators.max(45)]
    ),
    pulse: this.formBuilder.control<number | null>(this.data.record?.vitals?.pulse ?? null, [
      Validators.min(20),
      Validators.max(250),
    ]),
    weight: this.formBuilder.control<number | null>(this.data.record?.vitals?.weight ?? null, [
      Validators.min(1),
      Validators.max(400),
    ]),
  });

  protected addTag(event: MatChipInputEvent): void {
    const value = event.value.trim();
    event.chipInput.clear();

    if (value && !this.tags().some((tag) => tag.toLowerCase() === value.toLowerCase())) {
      this.tags.update((tags) => [...tags, value]);
    }
  }

  protected removeTag(tag: string): void {
    this.tags.update((tags) => tags.filter((entry) => entry !== tag));
  }

  protected submit(): void {
    this.submitted.set(true);

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const raw = this.form.getRawValue();
    const doctor = this.data.doctors.find((entry) => entry.id === raw.doctorId);

    const vitals = {
      bloodPressure: raw.bloodPressure.trim() || undefined,
      temperature: raw.temperature ?? undefined,
      pulse: raw.pulse ?? undefined,
      weight: raw.weight ?? undefined,
    };
    const hasVitals = Object.values(vitals).some((value) => value !== undefined);

    const payload = {
      patientId: this.data.patientId,
      patientName: this.data.patientName,
      doctorId: doctor?.id,
      doctorName: doctor?.name,
      type: raw.type,
      occurredAt: this.combine(raw.occurredDate!, raw.occurredTime!),
      title: raw.title.trim(),
      complaint: raw.complaint.trim() || undefined,
      diagnosis: raw.diagnosis.trim() || undefined,
      treatment: raw.treatment.trim() || undefined,
      prescription: raw.prescription.trim() || undefined,
      vitals: hasVitals ? vitals : undefined,
      tags: this.tags(),
      recordedBy: this.auth.currentUser?.displayName ?? '',
    };

    if (this.data.record) {
      this.store.update(this.data.record.id, payload);
      this.notifications.success(this.translate.instant('records.updated'));
    } else {
      this.store.create(payload);
      this.notifications.success(this.translate.instant('records.created'));
    }

    this.dialogRef.close(true);
  }

  /** Merges the two pickers into the single instant the record is filed under. */
  private combine(date: Date, time: Date): string {
    const merged = new Date(date);
    merged.setHours(time.getHours(), time.getMinutes(), 0, 0);
    return merged.toISOString();
  }
}
