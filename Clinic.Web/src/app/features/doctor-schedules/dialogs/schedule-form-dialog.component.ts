import { ChangeDetectionStrategy, Component, Inject, inject, signal } from '@angular/core';
import {
  AbstractControl,
  FormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  Validators
} from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';

import { Observable } from 'rxjs';

import { Doctor } from '../../../core/models/doctor.model';
import { DoctorSchedule, WEEK_DAYS, WeekDay } from '../../../core/models/schedule.model';
import { DoctorsService } from '../../../core/services/doctors.service';
import { NotificationService } from '../../../core/services/notification.service';
import { SchedulesService } from '../../../core/services/schedules.service';
import { applyServerValidationErrors } from '../../../core/utils/form-errors.util';

export interface ScheduleFormDialogData {
  schedule?: DoctorSchedule;
}

@Component({
  selector: 'app-schedule-form-dialog',
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
  templateUrl: './schedule-form-dialog.component.html'
})
export class ScheduleFormDialogComponent {
  private readonly schedulesService = inject(SchedulesService);
  private readonly doctorsService = inject(DoctorsService);
  private readonly notifications = inject(NotificationService);
  private readonly fb = inject(FormBuilder);
  private readonly dialogRef = inject(MatDialogRef<ScheduleFormDialogComponent, boolean>);

  readonly weekDays = WEEK_DAYS;
  readonly doctors = signal<Doctor[]>([]);
  readonly submitting = signal(false);
  readonly isEdit: boolean;

  readonly form = this.fb.nonNullable.group(
    {
      doctorId: this.fb.control<number | null>(null, [Validators.required]),
      weekDay: this.fb.control<WeekDay | null>(null, [Validators.required]),
      startTime: ['09:00', [Validators.required]],
      endTime: ['17:00', [Validators.required]]
    },
    { validators: timeRangeValidator }
  );

  constructor(@Inject(MAT_DIALOG_DATA) private readonly data: ScheduleFormDialogData) {
    this.isEdit = !!data?.schedule;

    if (data?.schedule) {
      const schedule = data.schedule;
      this.form.patchValue({
        doctorId: schedule.doctorId,
        weekDay: schedule.weekDay,
        startTime: this.trimTime(schedule.startTime),
        endTime: this.trimTime(schedule.endTime)
      });
      // The update DTO cannot reassign the doctor.
      this.form.controls.doctorId.disable();
    }

    this.doctorsService.getDoctors({ pageIndex: 1, pageSize: 20, sort: 'nameAsc' }).subscribe({
      next: (page) => {
        const doctors = page.data;
        if (data?.schedule && !doctors.some((doctor) => doctor.id === data.schedule!.doctorId)) {
          doctors.push({
            id: data.schedule.doctorId,
            name: data.schedule.doctorName,
            specialization: ''
          });
        }
        this.doctors.set(doctors);
      },
      error: () => this.doctors.set([])
    });
  }

  submit(): void {
    if (this.form.invalid || this.submitting()) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    const value = this.form.getRawValue();
    const startTime = `${value.startTime}:00`;
    const endTime = `${value.endTime}:00`;

    const request$: Observable<unknown> = this.isEdit
      ? this.schedulesService.updateSchedule(this.data.schedule!.id, {
          weekDay: value.weekDay as WeekDay,
          startTime,
          endTime
        })
      : this.schedulesService.createSchedule({
          doctorId: value.doctorId as number,
          weekDay: value.weekDay as WeekDay,
          startTime,
          endTime
        });

    request$.subscribe({
      next: () => {
        this.notifications.success(this.isEdit ? 'Schedule updated.' : 'Schedule created.');
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

  private trimTime(value: string): string {
    return value ? value.slice(0, 5) : '';
  }
}

function timeRangeValidator(control: AbstractControl): ValidationErrors | null {
  const start = control.get('startTime')?.value as string | null;
  const end = control.get('endTime')?.value as string | null;
  if (start && end && start >= end) {
    return { timeRange: true };
  }
  return null;
}
