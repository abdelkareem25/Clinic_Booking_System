import { Component, Inject, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatNativeDateModule } from '@angular/material/core';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';

import { Doctor } from '../../../core/models/doctor.model';
import { DoctorSchedule } from '../../../core/models/schedule.model';
import { AppointmentsService } from '../../../core/services/appointments.service';
import { NotificationService } from '../../../core/services/notification.service';
import { WeekdayPipe } from '../../../shared/pipes/weekday.pipe';

export interface BookAppointmentDialogData {
  doctor: Doctor;
  schedules: DoctorSchedule[];
}

@Component({
  selector: 'app-book-appointment-dialog',
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatDatepickerModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatNativeDateModule,
    MatSelectModule,
    WeekdayPipe
  ],
  templateUrl: './book-appointment-dialog.component.html',
  styleUrl: './book-appointment-dialog.component.scss'
})
export class BookAppointmentDialogComponent {
  private readonly appointments = inject(AppointmentsService);
  private readonly dialogRef = inject(MatDialogRef<BookAppointmentDialogComponent>);
  private readonly fb = inject(FormBuilder);
  private readonly notifications = inject(NotificationService);

  submitting = false;

  readonly form = this.fb.nonNullable.group({
    patientId: [0, [Validators.required, Validators.min(1)]],
    appointmentDate: [new Date(), [Validators.required]],
    appointmentTime: ['09:00', [Validators.required]]
  });

  constructor(@Inject(MAT_DIALOG_DATA) readonly data: BookAppointmentDialogData) {}

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    const appointmentDate = this.toIsoDateTime(value.appointmentDate, value.appointmentTime);
    this.submitting = true;

    this.appointments
      .createAppointment({
        patientId: Number(value.patientId),
        doctorId: this.data.doctor.id,
        appointmentDate
      })
      .subscribe({
        next: () => {
          this.notifications.success('Appointment booked successfully.');
          this.dialogRef.close(true);
        },
        error: () => {
          this.submitting = false;
        }
      });
  }

  private toIsoDateTime(date: Date, time: string): string {
    const [hours = '0', minutes = '0'] = time.split(':');
    const result = new Date(date);
    result.setHours(Number(hours), Number(minutes), 0, 0);
    return result.toISOString();
  }
}

