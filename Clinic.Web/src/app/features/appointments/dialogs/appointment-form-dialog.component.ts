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
import { forkJoin } from 'rxjs';

import { Appointment } from '../../../core/models/appointment.model';
import { Doctor } from '../../../core/models/doctor.model';
import { Patient } from '../../../core/models/patient.model';
import { AppointmentsService } from '../../../core/services/appointments.service';
import { DoctorsService } from '../../../core/services/doctors.service';
import { NotificationService } from '../../../core/services/notification.service';
import { PatientsService } from '../../../core/services/patients.service';
import { combineDateAndTime, parseDate } from '../../../core/utils/date.util';
import { applyServerValidationErrors } from '../../../core/utils/form-errors.util';

export interface AppointmentFormDialogData {
  appointment?: Appointment;
}

@Component({
  selector: 'app-appointment-form-dialog',
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
  templateUrl: './appointment-form-dialog.component.html'
})
export class AppointmentFormDialogComponent {
  private readonly appointmentsService = inject(AppointmentsService);
  private readonly doctorsService = inject(DoctorsService);
  private readonly patientsService = inject(PatientsService);
  private readonly notifications = inject(NotificationService);
  private readonly fb = inject(FormBuilder);
  private readonly dialogRef = inject(MatDialogRef<AppointmentFormDialogComponent, boolean>);

  readonly doctors = signal<Doctor[]>([]);
  readonly patients = signal<Patient[]>([]);
  readonly submitting = signal(false);
  readonly loadingOptions = signal(true);
  readonly isEdit: boolean;

  readonly form = this.fb.nonNullable.group({
    patientId: this.fb.control<number | null>(null, [Validators.required]),
    doctorId: this.fb.control<number | null>(null, [Validators.required]),
    date: this.fb.control<Date | null>(null, [Validators.required]),
    time: ['09:00', [Validators.required]]
  });

  constructor(@Inject(MAT_DIALOG_DATA) private readonly data: AppointmentFormDialogData) {
    this.isEdit = !!data?.appointment;

    const existingDate = parseDate(data?.appointment?.appointmentDate);
    if (existingDate) {
      this.form.patchValue({
        date: existingDate,
        time: `${`${existingDate.getHours()}`.padStart(2, '0')}:${`${existingDate.getMinutes()}`.padStart(2, '0')}`
      });
    }

    forkJoin({
      doctors: this.doctorsService.getDoctors({ pageIndex: 1, pageSize: 20, sort: 'nameAsc' }),
      patients: this.patientsService.getPatients({ pageIndex: 1, pageSize: 20, sort: 'Asc' })
    }).subscribe({
      next: ({ doctors, patients }) => {
        this.doctors.set(doctors.data);
        this.patients.set(patients.data);
        this.prefillParticipants();
        this.loadingOptions.set(false);
      },
      error: () => this.loadingOptions.set(false)
    });
  }

  submit(): void {
    if (this.form.invalid || this.submitting()) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    const value = this.form.getRawValue();
    const payload = {
      patientId: value.patientId as number,
      doctorId: value.doctorId as number,
      appointmentDate: combineDateAndTime(value.date as Date, value.time)
    };

    const request$ = this.isEdit
      ? this.appointmentsService.updateAppointment(this.data.appointment!.id, payload)
      : this.appointmentsService.createAppointment(payload);

    request$.subscribe({
      next: () => {
        this.notifications.success(this.isEdit ? 'Appointment updated.' : 'Appointment booked.');
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

  /**
   * The appointment DTO returns names, not ids, so on edit we best-effort match
   * the participants back to the loaded doctor/patient lists.
   */
  private prefillParticipants(): void {
    const appointment = this.data.appointment;
    if (!appointment) {
      return;
    }

    const doctor =
      (appointment.doctorId && this.doctors().find((item) => item.id === appointment.doctorId)) ||
      this.doctors().find((item) => item.name === appointment.doctorName);
    const patient =
      (appointment.patientId && this.patients().find((item) => item.id === appointment.patientId)) ||
      this.patients().find((item) => item.name === appointment.patientName);

    this.form.patchValue({
      doctorId: doctor?.id ?? null,
      patientId: patient?.id ?? null
    });
  }
}
