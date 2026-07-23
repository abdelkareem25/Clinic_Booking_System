import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog } from '@angular/material/dialog';
import { MatDividerModule } from '@angular/material/divider';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { forkJoin, of, switchMap } from 'rxjs';
import { catchError } from 'rxjs/operators';

import { Doctor } from '../../../core/models/doctor.model';
import { Pagination } from '../../../core/models/pagination.model';
import { DoctorSchedule } from '../../../core/models/schedule.model';
import { DoctorsService } from '../../../core/services/doctors.service';
import { NotificationService } from '../../../core/services/notification.service';
import { SchedulesService } from '../../../core/services/schedules.service';
import {
  ConfirmDialogComponent,
  ConfirmDialogData
} from '../../../shared/components/confirm-dialog/confirm-dialog.component';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { WeekdayPipe } from '../../../shared/pipes/weekday.pipe';
import { DoctorFormDialogComponent } from '../dialogs/doctor-form-dialog.component';

const EMPTY_SCHEDULES: Pagination<DoctorSchedule> = { pageIndex: 1, pageSize: 0, count: 0, data: [] };

@Component({
  selector: 'app-doctor-detail',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterLink,
    MatButtonModule,
    MatCardModule,
    MatDividerModule,
    MatIconModule,
    MatListModule,
    EmptyStateComponent,
    PageHeaderComponent,
    WeekdayPipe
  ],
  templateUrl: './doctor-detail.component.html',
  styleUrl: './doctor-detail.component.scss'
})
export class DoctorDetailComponent {
  private readonly doctorsService = inject(DoctorsService);
  private readonly schedulesService = inject(SchedulesService);
  private readonly dialog = inject(MatDialog);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly notifications = inject(NotificationService);

  readonly doctor = signal<Doctor | null>(null);
  readonly schedules = signal<DoctorSchedule[]>([]);
  readonly loading = signal(true);
  readonly notFound = signal(false);

  constructor() {
    this.route.paramMap
      .pipe(
        switchMap((params) => {
          const id = Number(params.get('id'));
          this.loading.set(true);
          this.notFound.set(false);
          return forkJoin({
            doctor: this.doctorsService.getDoctor(id).pipe(catchError(() => of(null))),
            schedules: this.schedulesService
              .getSchedules({ doctorId: id, pageIndex: 1, pageSize: 20 })
              .pipe(catchError(() => of(EMPTY_SCHEDULES)))
          });
        })
      )
      .subscribe(({ doctor, schedules }) => {
        this.doctor.set(doctor);
        this.schedules.set(schedules.data);
        this.notFound.set(!doctor);
        this.loading.set(false);
      });
  }

  edit(): void {
    const current = this.doctor();
    if (!current) {
      return;
    }

    this.dialog
      .open(DoctorFormDialogComponent, { width: '480px', data: { doctor: current } })
      .afterClosed()
      .subscribe((saved) => {
        if (saved) {
          this.reload(current.id);
        }
      });
  }

  remove(): void {
    const current = this.doctor();
    if (!current) {
      return;
    }

    const data: ConfirmDialogData = {
      title: 'Delete doctor',
      message: `Delete Dr. ${current.name}? This cannot be undone.`,
      confirmText: 'Delete',
      icon: 'delete'
    };

    this.dialog
      .open(ConfirmDialogComponent, { width: '420px', data })
      .afterClosed()
      .pipe(switchMap((confirmed) => (confirmed ? this.doctorsService.deleteDoctor(current.id) : of(null))))
      .subscribe((result) => {
        if (result !== null) {
          this.notifications.success('Doctor deleted.');
          void this.router.navigate(['/doctors']);
        }
      });
  }

  private reload(id: number): void {
    this.doctorsService.getDoctor(id).subscribe((doctor) => this.doctor.set(doctor));
  }
}
