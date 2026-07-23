import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatSelectModule } from '@angular/material/select';
import { Router } from '@angular/router';
import { of, switchMap } from 'rxjs';

import {
  APPOINTMENT_TIME_STATUSES,
  Appointment,
  AppointmentTimeStatus
} from '../../../core/models/appointment.model';
import { DEFAULT_PAGE_SIZE } from '../../../core/models/pagination.model';
import { AppointmentsService } from '../../../core/services/appointments.service';
import { DoctorsService } from '../../../core/services/doctors.service';
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
import {
  RowActionEvent,
  SortState,
  TableColumn,
  TableRowAction
} from '../../../shared/components/data-table/data-table.model';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { SelectOption } from '../../../shared/components/search-filter-bar/search-filter-bar.component';
import {
  AppointmentFormDialogComponent,
  AppointmentFormDialogData
} from '../dialogs/appointment-form-dialog.component';

@Component({
  selector: 'app-appointment-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatPaginatorModule,
    MatSelectModule,
    PageHeaderComponent,
    DataTableComponent
  ],
  templateUrl: './appointment-list.component.html',
  styleUrl: './appointment-list.component.scss'
})
export class AppointmentListComponent {
  private readonly appointmentsService = inject(AppointmentsService);
  private readonly doctorsService = inject(DoctorsService);
  private readonly patientsService = inject(PatientsService);
  private readonly notifications = inject(NotificationService);
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);
  private readonly fb = inject(FormBuilder);

  private readonly rows = signal<Appointment[]>([]);
  private readonly statusFilter = signal<AppointmentTimeStatus | null>(null);
  readonly total = signal(0);
  readonly loading = signal(false);
  readonly doctorOptions = signal<SelectOption[]>([]);
  readonly patientOptions = signal<SelectOption[]>([]);

  readonly statuses = APPOINTMENT_TIME_STATUSES;

  pageIndex = 0;
  pageSize = DEFAULT_PAGE_SIZE;
  private sort = '';

  readonly filterForm = this.fb.nonNullable.group({
    doctorId: this.fb.control<number | null>(null),
    patientId: this.fb.control<number | null>(null),
    status: this.fb.control<AppointmentTimeStatus | null>(null)
  });

  // Status is derived client-side (the API has no status column), so it is
  // filtered on the fetched page after doctor/patient server-side filtering.
  readonly appointments = computed(() => {
    const status = this.statusFilter();
    const rows = this.rows();
    return status ? rows.filter((row) => deriveAppointmentStatus(row.appointmentDate) === status) : rows;
  });

  readonly columns: TableColumn<Appointment>[] = [
    { key: 'id', header: '#', value: (row) => row.id },
    { key: 'patientName', header: 'Patient', value: (row) => row.patientName, variant: 'strong' },
    { key: 'doctorName', header: 'Doctor', value: (row) => row.doctorName },
    {
      key: 'appointmentDate',
      header: 'Date & time',
      sortKey: 'date',
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

  readonly actions: TableRowAction<Appointment>[] = [
    { id: 'view', icon: 'visibility', tooltip: 'View details', color: 'primary' },
    { id: 'edit', icon: 'edit', tooltip: 'Edit appointment', color: 'accent' },
    { id: 'delete', icon: 'delete', tooltip: 'Cancel appointment', color: 'warn' }
  ];

  constructor() {
    this.loadOptions();
    this.filterForm.controls.doctorId.valueChanges
      .pipe(takeUntilDestroyed())
      .subscribe(() => this.resetAndLoad());
    this.filterForm.controls.patientId.valueChanges
      .pipe(takeUntilDestroyed())
      .subscribe(() => this.resetAndLoad());
    this.filterForm.controls.status.valueChanges
      .pipe(takeUntilDestroyed())
      .subscribe((status) => this.statusFilter.set(status));
    this.load();
  }

  onSortChanged(sort: SortState): void {
    this.sort = sort.direction ? (sort.direction === 'asc' ? 'Ascending' : 'Descending') : '';
    this.load();
  }

  onPage(event: PageEvent): void {
    this.pageIndex = event.pageIndex;
    this.pageSize = event.pageSize;
    this.load();
  }

  resetFilters(): void {
    this.filterForm.reset({ doctorId: null, patientId: null, status: null });
    this.resetAndLoad();
  }

  openCreate(): void {
    this.openForm();
  }

  onRowAction(event: RowActionEvent<Appointment>): void {
    switch (event.action) {
      case 'view':
        void this.router.navigate(['/appointments', event.row.id]);
        break;
      case 'edit':
        this.openForm(event.row);
        break;
      case 'delete':
        this.confirmDelete(event.row);
        break;
    }
  }

  private openForm(appointment?: Appointment): void {
    const data: AppointmentFormDialogData = { appointment };
    this.dialog
      .open(AppointmentFormDialogComponent, { width: '620px', data })
      .afterClosed()
      .subscribe((saved) => {
        if (saved) {
          this.load();
        }
      });
  }

  private confirmDelete(appointment: Appointment): void {
    const data: ConfirmDialogData = {
      title: 'Cancel appointment',
      message: `Cancel the appointment for ${appointment.patientName} with ${appointment.doctorName}?`,
      confirmText: 'Cancel appointment',
      cancelText: 'Keep',
      icon: 'event_busy'
    };

    this.dialog
      .open(ConfirmDialogComponent, { width: '440px', data })
      .afterClosed()
      .pipe(
        switchMap((confirmed) => (confirmed ? this.appointmentsService.cancelAppointment(appointment.id) : of(null)))
      )
      .subscribe((result) => {
        if (result !== null) {
          this.notifications.success('Appointment cancelled.');
          if (this.rows().length === 1 && this.pageIndex > 0) {
            this.pageIndex -= 1;
          }
          this.load();
        }
      });
  }

  private resetAndLoad(): void {
    this.pageIndex = 0;
    this.load();
  }

  private loadOptions(): void {
    this.doctorsService.getDoctors({ pageIndex: 1, pageSize: 20, sort: 'nameAsc' }).subscribe({
      next: (page) =>
        this.doctorOptions.set(page.data.map((doctor) => ({ label: doctor.name, value: doctor.id }))),
      error: () => this.doctorOptions.set([])
    });
    this.patientsService.getPatients({ pageIndex: 1, pageSize: 20, sort: 'Asc' }).subscribe({
      next: (page) =>
        this.patientOptions.set(page.data.map((patient) => ({ label: patient.name, value: patient.id }))),
      error: () => this.patientOptions.set([])
    });
  }

  private load(): void {
    this.loading.set(true);
    const { doctorId, patientId } = this.filterForm.getRawValue();
    this.appointmentsService
      .getAppointments({
        pageIndex: this.pageIndex + 1,
        pageSize: this.pageSize,
        doctorId: doctorId ?? undefined,
        patientId: patientId ?? undefined,
        sort: this.sort
      })
      .subscribe({
        next: (page) => {
          this.rows.set(page.data);
          this.total.set(page.count);
          this.loading.set(false);
        },
        error: () => {
          this.rows.set([]);
          this.total.set(0);
          this.loading.set(false);
        }
      });
  }
}
