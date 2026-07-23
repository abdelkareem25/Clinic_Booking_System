import { ChangeDetectionStrategy, Component, Inject, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatNativeDateModule } from '@angular/material/core';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';

import { Observable } from 'rxjs';

import { GENDERS, Patient } from '../../../core/models/patient.model';
import { NotificationService } from '../../../core/services/notification.service';
import { PatientsService } from '../../../core/services/patients.service';
import { toDateOnly } from '../../../core/utils/date.util';
import { applyServerValidationErrors } from '../../../core/utils/form-errors.util';

export interface PatientFormDialogData {
  patient?: Patient;
}

@Component({
  selector: 'app-patient-form-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatDatepickerModule,
    MatNativeDateModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule
  ],
  templateUrl: './patient-form-dialog.component.html'
})
export class PatientFormDialogComponent {
  private readonly patientsService = inject(PatientsService);
  private readonly notifications = inject(NotificationService);
  private readonly fb = inject(FormBuilder);
  private readonly dialogRef = inject(MatDialogRef<PatientFormDialogComponent, boolean>);

  readonly genders = GENDERS;
  readonly maxDate = new Date();
  readonly submitting = signal(false);
  readonly isEdit: boolean;

  readonly form = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.minLength(2)]],
    phone: ['', [Validators.required, Validators.pattern(/^[0-9+\-\s()]{7,20}$/)]],
    dateOfBirth: this.fb.control<Date | null>(null, [Validators.required]),
    gender: ['', [Validators.required]]
  });

  constructor(@Inject(MAT_DIALOG_DATA) private readonly data: PatientFormDialogData) {
    this.isEdit = !!data?.patient;
    if (data?.patient) {
      this.form.patchValue({
        name: data.patient.name,
        phone: data.patient.phone,
        dateOfBirth: data.patient.dateOfBirth ? new Date(data.patient.dateOfBirth) : null,
        gender: data.patient.gender
      });
    }
  }

  submit(): void {
    if (this.form.invalid || this.submitting()) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    const value = this.form.getRawValue();
    const payload = {
      name: value.name,
      phone: value.phone,
      dateOfBirth: toDateOnly(value.dateOfBirth as Date),
      gender: value.gender
    };

    const request$: Observable<unknown> = this.isEdit
      ? this.patientsService.updatePatient(this.data.patient!.id, { id: this.data.patient!.id, ...payload })
      : this.patientsService.createPatient(payload);

    request$.subscribe({
      next: () => {
        this.notifications.success(this.isEdit ? 'Patient updated.' : 'Patient created.');
        this.dialogRef.close(true);
      },
      error: (error: unknown) => {
        this.submitting.set(false);
        const unmatched = applyServerValidationErrors(this.form, error);
        if (unmatched.length) {
          this.notifications.error(unmatched.join(' '));
        }
      }
    });
  }
}
