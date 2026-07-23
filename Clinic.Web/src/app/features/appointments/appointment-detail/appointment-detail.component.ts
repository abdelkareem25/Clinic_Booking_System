import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { of, switchMap } from 'rxjs';
import { catchError } from 'rxjs/operators';

import { Appointment } from '../../../core/models/appointment.model';
import { AppointmentsService } from '../../../core/services/appointments.service';
import { NotificationService } from '../../../core/services/notification.service';
import {
  appointmentStatusTone,
  deriveAppointmentStatus
} from '../../../core/utils/appointment-status.util';
import {
  ConfirmDialogComponent,
  ConfirmDialogData
} from '../../../shared/components/confirm-dialog/confirm-dialog.component';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import {
  AppointmentFormDialogComponent,
  AppointmentFormDialogData
} from '../dialogs/appointment-form-dialog.component';

@Component({
  selector: 'app-appointment-detail',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    RouterLink,
    MatButtonModule,
    MatCardModule,
    MatIconModule,
    EmptyStateComponent,
    PageHeaderComponent
  ],
  templateUrl: './appointment-detail.component.html',
  styleUrl: './appointment-detail.component.scss'
})
export class AppointmentDetailComponent {
  private readonly appointmentsService = inject(AppointmentsService);
  private readonly dialog = inject(MatDialog);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly notifications = inject(NotificationService);

  readonly appointment = signal<Appointment | null>(null);
  readonly loading = signal(true);
  readonly notFound = signal(false);

  readonly status = computed(() => {
    const appointment = this.appointment();
    return appointment ? deriveAppointmentStatus(appointment.appointmentDate) : null;
  });
  readonly statusTone = computed(() => {
    const status = this.status();
    return status ? appointmentStatusTone(status) : 'neutral';
  });

  constructor() {
    this.route.paramMap
      .pipe(
        switchMap((params) => {
          const id = Number(params.get('id'));
          this.loading.set(true);
          this.notFound.set(false);
          return this.appointmentsService.getAppointment(id).pipe(catchError(() => of(null)));
        })
      )
      .subscribe((appointment) => {
        this.appointment.set(appointment);
        this.notFound.set(!appointment);
        this.loading.set(false);
      });
  }

  edit(): void {
    const current = this.appointment();
    if (!current) {
      return;
    }

    const data: AppointmentFormDialogData = { appointment: current };
    this.dialog
      .open(AppointmentFormDialogComponent, { width: '620px', data })
      .afterClosed()
      .subscribe((saved) => {
        if (saved) {
          this.appointmentsService.getAppointment(current.id).subscribe((appointment) => this.appointment.set(appointment));
        }
      });
  }

  remove(): void {
    const current = this.appointment();
    if (!current) {
      return;
    }

    const data: ConfirmDialogData = {
      title: 'Cancel appointment',
      message: `Cancel the appointment for ${current.patientName} with ${current.doctorName}?`,
      confirmText: 'Cancel appointment',
      cancelText: 'Keep',
      icon: 'event_busy'
    };

    this.dialog
      .open(ConfirmDialogComponent, { width: '440px', data })
      .afterClosed()
      .pipe(switchMap((confirmed) => (confirmed ? this.appointmentsService.cancelAppointment(current.id) : of(null))))
      .subscribe((result) => {
        if (result !== null) {
          this.notifications.success('Appointment cancelled.');
          void this.router.navigate(['/appointments']);
        }
      });
  }
}
