import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { forkJoin, of, switchMap } from 'rxjs';
import { catchError } from 'rxjs/operators';

import { Appointment } from '../../../core/models/appointment.model';
import { Pagination } from '../../../core/models/pagination.model';
import { Patient, calculateAge } from '../../../core/models/patient.model';
import { AppointmentsService } from '../../../core/services/appointments.service';
import { NotificationService } from '../../../core/services/notification.service';
import { PatientsService } from '../../../core/services/patients.service';
import {
  appointmentStatusTone,
  deriveAppointmentStatus
} from '../../../core/utils/appointment-status.util';
import {
  ConfirmDialogComponent,
  ConfirmDialogData
} from '../../../shared/components/confirm-dialog/confirm-dialog.component';
import { DataTableComponent } from '../../../shared/components/data-table/data-table.component';
import { TableColumn } from '../../../shared/components/data-table/data-table.model';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import {
  PatientFormDialogComponent,
  PatientFormDialogData
} from '../dialogs/patient-form-dialog.component';

const EMPTY_APPOINTMENTS: Pagination<Appointment> = { pageIndex: 1, pageSize: 0, count: 0, data: [] };

@Component({
  selector: 'app-patient-detail',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    RouterLink,
    MatButtonModule,
    MatCardModule,
    MatIconModule,
    EmptyStateComponent,
    PageHeaderComponent,
    DataTableComponent
  ],
  templateUrl: './patient-detail.component.html',
  styleUrl: './patient-detail.component.scss'
})
export class PatientDetailComponent {
  private readonly patientsService = inject(PatientsService);
  private readonly appointmentsService = inject(AppointmentsService);
  private readonly dialog = inject(MatDialog);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly notifications = inject(NotificationService);

  readonly patient = signal<Patient | null>(null);
  readonly appointments = signal<Appointment[]>([]);
  readonly loading = signal(true);
  readonly notFound = signal(false);

  readonly age = () => calculateAge(this.patient()?.dateOfBirth);

  readonly appointmentColumns: TableColumn<Appointment>[] = [
    { key: 'doctorName', header: 'Doctor', value: (row) => row.doctorName, variant: 'strong' },
    {
      key: 'appointmentDate',
      header: 'Date & time',
      value: (row) => new Date(row.appointmentDate).toLocaleString()
    },
    {
      key: 'status',
      header: 'Status',
      align: 'center',
      value: (row) => deriveAppointmentStatus(row.appointmentDate),
      variant: 'chip',
      chip: (row) => {
        const status = deriveAppointmentStatus(row.appointmentDate);
        return { label: status, tone: appointmentStatusTone(status) };
      }
    }
  ];

  constructor() {
    this.route.paramMap
      .pipe(
        switchMap((params) => {
          const id = Number(params.get('id'));
          this.loading.set(true);
          this.notFound.set(false);
          return forkJoin({
            patient: this.patientsService.getPatient(id).pipe(catchError(() => of(null))),
            appointments: this.appointmentsService
              .getAppointments({ patientId: id, pageIndex: 1, pageSize: 20, sort: 'Descending' })
              .pipe(catchError(() => of(EMPTY_APPOINTMENTS)))
          });
        })
      )
      .subscribe(({ patient, appointments }) => {
        this.patient.set(patient);
        this.appointments.set(appointments.data);
        this.notFound.set(!patient);
        this.loading.set(false);
      });
  }

  edit(): void {
    const current = this.patient();
    if (!current) {
      return;
    }

    const data: PatientFormDialogData = { patient: current };
    this.dialog
      .open(PatientFormDialogComponent, { width: '560px', data })
      .afterClosed()
      .subscribe((saved) => {
        if (saved) {
          this.patientsService.getPatient(current.id).subscribe((patient) => this.patient.set(patient));
        }
      });
  }

  remove(): void {
    const current = this.patient();
    if (!current) {
      return;
    }

    const data: ConfirmDialogData = {
      title: 'Delete patient',
      message: `Delete ${current.name}? This cannot be undone.`,
      confirmText: 'Delete',
      icon: 'delete'
    };

    this.dialog
      .open(ConfirmDialogComponent, { width: '420px', data })
      .afterClosed()
      .pipe(switchMap((confirmed) => (confirmed ? this.patientsService.deletePatient(current.id) : of(null))))
      .subscribe((result) => {
        if (result !== null) {
          this.notifications.success('Patient deleted.');
          void this.router.navigate(['/patients']);
        }
      });
  }
}
