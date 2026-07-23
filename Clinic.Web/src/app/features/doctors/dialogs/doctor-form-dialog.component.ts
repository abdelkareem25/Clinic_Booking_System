import { ChangeDetectionStrategy, Component, Inject, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';

import { DEFAULT_SPECIALIZATIONS, Doctor } from '../../../core/models/doctor.model';
import { DoctorsService } from '../../../core/services/doctors.service';
import { NotificationService } from '../../../core/services/notification.service';
import { applyServerValidationErrors } from '../../../core/utils/form-errors.util';

export interface DoctorFormDialogData {
  doctor?: Doctor;
}

@Component({
  selector: 'app-doctor-form-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule
  ],
  templateUrl: './doctor-form-dialog.component.html'
})
export class DoctorFormDialogComponent {
  private readonly doctorsService = inject(DoctorsService);
  private readonly notifications = inject(NotificationService);
  private readonly fb = inject(FormBuilder);
  private readonly dialogRef = inject(MatDialogRef<DoctorFormDialogComponent, boolean>);

  readonly submitting = signal(false);
  readonly isEdit: boolean;
  readonly specializations: string[];

  readonly form = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.minLength(2)]],
    specialization: ['', [Validators.required]]
  });

  constructor(@Inject(MAT_DIALOG_DATA) private readonly data: DoctorFormDialogData) {
    this.isEdit = !!data?.doctor;
    this.specializations = Array.from(
      new Set([...DEFAULT_SPECIALIZATIONS, data?.doctor?.specialization].filter(Boolean) as string[])
    ).sort((a, b) => a.localeCompare(b));

    if (data?.doctor) {
      this.form.patchValue({
        name: data.doctor.name,
        specialization: data.doctor.specialization
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

    const request$ = this.isEdit
      ? this.doctorsService.updateDoctor(this.data.doctor!.id, {
          id: this.data.doctor!.id,
          name: value.name,
          specialization: value.specialization
        })
      : this.doctorsService.createDoctor({ name: value.name, specialization: value.specialization });

    request$.subscribe({
      next: () => {
        this.notifications.success(this.isEdit ? 'Doctor updated.' : 'Doctor created.');
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
