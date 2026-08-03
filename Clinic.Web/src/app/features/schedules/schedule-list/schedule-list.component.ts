import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatTooltipModule } from '@angular/material/tooltip';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { catchError, forkJoin, of } from 'rxjs';

import { PermissionService } from '../../../core/authz/permission.service';
import { Doctor } from '../../../core/models/doctor.model';
import { DoctorSchedule, WEEK_DAYS, WeekDay } from '../../../core/models/schedule.model';
import { DoctorsService } from '../../../core/services/doctors.service';
import { NotificationService } from '../../../core/services/notification.service';
import { SchedulesService } from '../../../core/services/schedules.service';
import { formatTime12, timeToMinutes } from '../../../core/utils/date.util';
import { CardComponent } from '../../../shared/ui/card/card.component';
import { confirmDialog } from '../../../shared/ui/confirm-dialog/confirm-dialog.component';
import { EmptyStateComponent } from '../../../shared/ui/empty-state/empty-state.component';
import { IconComponent } from '../../../shared/ui/icon/icon.component';
import { PageHeaderComponent } from '../../../shared/ui/page-header/page-header.component';
import {
  ScheduleDialogData,
  ScheduleFormDialogComponent,
} from '../schedule-form-dialog/schedule-form-dialog.component';

interface DayCell {
  weekDay: WeekDay;
  shifts: (DoctorSchedule & { label: string })[];
}

interface DoctorWeek {
  doctor: Doctor;
  days: DayCell[];
  weeklyHours: number;
}

const EMPTY_PAGE = { pageIndex: 1, pageSize: 0, count: 0, data: [] };

/**
 * The weekly schedule grid.
 *
 * A flat list of `(doctor, day, from, to)` rows — which is how the API stores
 * it — is almost unreadable when you are trying to answer "who is in on
 * Wednesday?". The grid answers that at a glance, and an empty cell is itself
 * the button that fills it, so adding hours takes one click instead of opening
 * a form and re-picking the doctor and day you already had in mind.
 */
@Component({
  selector: 'app-schedule-list',
  imports: [
    MatButtonModule,
    MatFormFieldModule,
    MatSelectModule,
    MatTooltipModule,
    TranslatePipe,
    CardComponent,
    EmptyStateComponent,
    IconComponent,
    PageHeaderComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './schedule-list.component.html',
  styleUrl: './schedule-list.component.scss',
})
export class ScheduleListComponent {
  private readonly api = inject(SchedulesService);
  private readonly dialog = inject(MatDialog);
  private readonly doctorsApi = inject(DoctorsService);
  private readonly notifications = inject(NotificationService);
  private readonly translate = inject(TranslateService);

  protected readonly permissions = inject(PermissionService);
  protected readonly weekDays = WEEK_DAYS;

  protected readonly doctors = signal<Doctor[]>([]);
  protected readonly schedules = signal<DoctorSchedule[]>([]);
  protected readonly loading = signal(true);
  protected readonly doctorFilter = signal<number | 'all'>('all');

  protected readonly weeks = computed<DoctorWeek[]>(() => {
    const filter = this.doctorFilter();
    const doctors =
      filter === 'all' ? this.doctors() : this.doctors().filter((doctor) => doctor.id === filter);

    return doctors.map((doctor) => {
      const own = this.schedules().filter((schedule) => schedule.doctorId === doctor.id);

      const days = WEEK_DAYS.map(({ value }) => ({
        weekDay: value,
        shifts: own
          .filter((schedule) => schedule.weekDay === value)
          .sort((a, b) => timeToMinutes(a.startTime) - timeToMinutes(b.startTime))
          .map((schedule) => ({
            ...schedule,
            label: `${formatTime12(schedule.startTime)} – ${formatTime12(schedule.endTime)}`,
          })),
      }));

      const weeklyMinutes = own.reduce(
        (sum, schedule) =>
          sum + Math.max(0, timeToMinutes(schedule.endTime) - timeToMinutes(schedule.startTime)),
        0
      );

      return { doctor, days, weeklyHours: Math.round((weeklyMinutes / 60) * 10) / 10 };
    });
  });

  protected readonly hasAnySchedule = computed(() => this.schedules().length > 0);

  constructor() {
    this.load();
  }

  protected load(): void {
    this.loading.set(true);

    forkJoin({
      doctors: this.doctorsApi
        .getDoctors({ pageIndex: 1, pageSize: 200 })
        .pipe(catchError(() => of(EMPTY_PAGE))),
      schedules: this.api
        .getSchedules({ pageIndex: 1, pageSize: 500 })
        .pipe(catchError(() => of(EMPTY_PAGE))),
    }).subscribe({
      next: ({ doctors, schedules }) => {
        this.doctors.set(doctors.data as Doctor[]);
        this.schedules.set(schedules.data as DoctorSchedule[]);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  protected addShift(doctorId?: number, weekDay?: WeekDay): void {
    if (!this.permissions.can('schedules.create')) {
      return;
    }
    this.openDialog({ presetDoctorId: doctorId, presetWeekDay: weekDay });
  }

  protected editShift(schedule: DoctorSchedule): void {
    if (!this.permissions.can('schedules.edit')) {
      return;
    }
    this.openDialog({ schedule });
  }

  protected removeShift(schedule: DoctorSchedule, doctorName: string): void {
    confirmDialog(this.dialog, {
      title: 'schedules.delete',
      message: 'schedules.deleteConfirm',
      messageParams: {
        day: this.translate.instant(`weekday.${schedule.weekDay}`),
        from: formatTime12(schedule.startTime),
        to: formatTime12(schedule.endTime),
        doctor: doctorName,
      },
      confirmLabel: 'common.delete',
      tone: 'danger',
    }).subscribe((confirmed) => {
      if (!confirmed) {
        return;
      }

      this.api.deleteSchedule(schedule.id).subscribe(() => {
        this.notifications.success(this.translate.instant('schedules.deleted'));
        this.load();
      });
    });
  }

  private openDialog(partial: Partial<ScheduleDialogData>): void {
    this.dialog
      .open<ScheduleFormDialogComponent, ScheduleDialogData, boolean>(
        ScheduleFormDialogComponent,
        {
          data: {
            doctors: this.doctors(),
            schedules: this.schedules(),
            ...partial,
          },
        }
      )
      .afterClosed()
      .subscribe((saved) => {
        if (saved) {
          this.load();
        }
      });
  }
}
