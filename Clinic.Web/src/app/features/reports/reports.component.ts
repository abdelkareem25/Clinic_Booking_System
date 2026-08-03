import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { catchError, forkJoin, of } from 'rxjs';

import { PermissionService } from '../../core/authz/permission.service';
import { AccountsStore } from '../../core/data/accounts.store';
import { ClinicSettingsStore } from '../../core/data/clinic-settings.store';
import { MedicalRecordsStore } from '../../core/data/medical-records.store';
import { Appointment } from '../../core/models/appointment.model';
import { Doctor } from '../../core/models/doctor.model';
import { Patient } from '../../core/models/patient.model';
import { AppointmentsService } from '../../core/services/appointments.service';
import { DoctorsService } from '../../core/services/doctors.service';
import { NotificationService } from '../../core/services/notification.service';
import { PatientsService } from '../../core/services/patients.service';
import { addDays, eachDay, parseDate, startOfDay, startOfMonth } from '../../core/utils/date.util';
import { CardComponent } from '../../shared/ui/card/card.component';
import { ChartComponent, ChartPoint } from '../../shared/ui/chart/chart.component';
import { DataTableComponent } from '../../shared/ui/data-table/data-table.component';
import { TableColumn } from '../../shared/ui/data-table/data-table.model';
import { EmptyStateComponent } from '../../shared/ui/empty-state/empty-state.component';
import { IconComponent } from '../../shared/ui/icon/icon.component';
import { PageHeaderComponent } from '../../shared/ui/page-header/page-header.component';
import { StatCardComponent } from '../../shared/ui/stat-card/stat-card.component';

interface DoctorPerformance {
  id: string;
  name: string;
  specialization: string;
  appointments: number;
  patients: number;
  share: number;
}

const EMPTY_PAGE = { pageIndex: 1, pageSize: 0, count: 0, data: [] };

/**
 * Operational and financial reporting over an arbitrary period.
 *
 * One date range scopes every figure on the page — the filter row sits above
 * the cards rather than inside them, so nothing can be read against a different
 * window than its neighbour.
 */
@Component({
  selector: 'app-reports',
  imports: [
    MatButtonModule,
    MatDatepickerModule,
    MatFormFieldModule,
    MatInputModule,
    TranslatePipe,
    CardComponent,
    ChartComponent,
    DataTableComponent,
    EmptyStateComponent,
    IconComponent,
    PageHeaderComponent,
    StatCardComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './reports.component.html',
  styleUrl: './reports.component.scss',
})
export class ReportsComponent {
  private readonly appointmentsApi = inject(AppointmentsService);
  private readonly doctorsApi = inject(DoctorsService);
  private readonly notifications = inject(NotificationService);
  private readonly patientsApi = inject(PatientsService);
  private readonly records = inject(MedicalRecordsStore);
  private readonly translate = inject(TranslateService);

  protected readonly accounts = inject(AccountsStore);
  protected readonly permissions = inject(PermissionService);
  protected readonly settings = inject(ClinicSettingsStore);

  protected readonly appointments = signal<Appointment[]>([]);
  protected readonly doctors = signal<Doctor[]>([]);
  protected readonly patients = signal<Patient[]>([]);
  protected readonly loading = signal(true);

  protected readonly from = signal<Date>(startOfMonth(new Date()));
  protected readonly to = signal<Date>(new Date());

  protected readonly currency = computed(() => this.settings.settings().currency);

  constructor() {
    this.load();
  }

  // ---------------------------------------------------------------- period --

  private readonly inPeriod = computed(() => {
    const from = startOfDay(this.from()).getTime();
    const to = startOfDay(addDays(this.to(), 1)).getTime();

    return this.appointments().filter((appointment) => {
      const when = parseDate(appointment.appointmentDate);
      if (!when) {
        return false;
      }
      const time = when.getTime();
      return time >= from && time < to;
    });
  });

  protected readonly hasData = computed(
    () => this.inPeriod().length > 0 || this.accounts.totalsBetween(this.from(), this.to()).income > 0
  );

  // -------------------------------------------------------------- patients --

  protected readonly totalPatients = computed(() => this.patients().length);

  /** A patient whose earliest appointment falls inside the period. */
  protected readonly newPatients = computed(() => {
    const firstVisit = new Map<string, number>();

    for (const appointment of this.appointments()) {
      const when = parseDate(appointment.appointmentDate);
      if (!when) {
        continue;
      }
      const existing = firstVisit.get(appointment.patientName);
      if (existing === undefined || when.getTime() < existing) {
        firstVisit.set(appointment.patientName, when.getTime());
      }
    }

    const from = startOfDay(this.from()).getTime();
    const to = startOfDay(addDays(this.to(), 1)).getTime();

    return [...firstVisit.values()].filter((time) => time >= from && time < to).length;
  });

  protected readonly returningPatients = computed(() => {
    const distinct = new Set(this.inPeriod().map((appointment) => appointment.patientName));
    return Math.max(0, distinct.size - this.newPatients());
  });

  // ---------------------------------------------------------- appointments --

  protected readonly totalAppointments = computed(() => this.inPeriod().length);

  protected readonly completed = computed(() => {
    const now = Date.now();
    return this.inPeriod().filter((appointment) => {
      const when = parseDate(appointment.appointmentDate);
      return when ? when.getTime() < now : false;
    }).length;
  });

  protected readonly completionRate = computed(() =>
    this.totalAppointments() === 0
      ? 0
      : Math.round((this.completed() / this.totalAppointments()) * 1000) / 10
  );

  protected readonly appointmentTrend = computed<ChartPoint[]>(() => {
    // A long range would produce hundreds of columns; cap at 30 buckets so the
    // axis stays legible rather than silently truncating the period.
    const days = eachDay(this.from(), this.to()).slice(-30);

    return days.map((day) => ({
      label: `${day.getDate()}`,
      detail: day.toLocaleDateString(undefined, { day: 'numeric', month: 'short' }),
      value: this.inPeriod().filter((appointment) => {
        const when = parseDate(appointment.appointmentDate);
        return when ? when.toDateString() === day.toDateString() : false;
      }).length,
    }));
  });

  protected readonly busiestDay = computed(() => {
    const counts = new Map<number, number>();
    for (const appointment of this.inPeriod()) {
      const when = parseDate(appointment.appointmentDate);
      if (when) {
        counts.set(when.getDay(), (counts.get(when.getDay()) ?? 0) + 1);
      }
    }

    const best = [...counts.entries()].sort((a, b) => b[1] - a[1])[0];
    return best ? this.translate.instant(`weekday.${best[0]}`) : '—';
  });

  protected readonly busiestHour = computed(() => {
    const counts = new Map<number, number>();
    for (const appointment of this.inPeriod()) {
      const when = parseDate(appointment.appointmentDate);
      if (when) {
        counts.set(when.getHours(), (counts.get(when.getHours()) ?? 0) + 1);
      }
    }

    const best = [...counts.entries()].sort((a, b) => b[1] - a[1])[0];
    if (!best) {
      return '—';
    }

    const hour = best[0];
    const suffix = hour >= 12 ? 'PM' : 'AM';
    return `${hour % 12 === 0 ? 12 : hour % 12}:00 ${suffix}`;
  });

  // --------------------------------------------------------------- finance --

  protected readonly totals = computed(() => this.accounts.totalsBetween(this.from(), this.to()));

  protected readonly revenueTrend = computed<ChartPoint[]>(() =>
    this.accounts.dailyIncome(eachDay(this.from(), this.to()).slice(-30)).map(({ date, value }) => ({
      label: `${date.getDate()}`,
      detail: date.toLocaleDateString(undefined, { day: 'numeric', month: 'short' }),
      value,
    }))
  );

  protected readonly averageRevenue = computed(() => {
    const visits = this.completed();
    return visits === 0 ? 0 : Math.round((this.totals().income / visits) * 100) / 100;
  });

  // -------------------------------------------------- doctor performance --

  protected readonly performance = computed<DoctorPerformance[]>(() => {
    const total = this.inPeriod().length || 1;

    return this.doctors()
      .map((doctor) => {
        const own = this.inPeriod().filter(
          (appointment) => appointment.doctorName === doctor.name
        );

        return {
          id: String(doctor.id),
          name: doctor.name,
          specialization: doctor.specialization,
          appointments: own.length,
          patients: new Set(own.map((appointment) => appointment.patientName)).size,
          share: Math.round((own.length / total) * 1000) / 10,
        };
      })
      .sort((a, b) => b.appointments - a.appointments);
  });

  protected readonly performanceColumns: TableColumn<DoctorPerformance>[] = [
    { key: 'name', header: 'doctors.name', value: (row) => row.name, variant: 'strong' },
    {
      key: 'specialization',
      header: 'doctors.specialization',
      value: (row) => row.specialization,
      hideBelow: 'sm',
    },
    {
      key: 'appointments',
      header: 'appointments.title',
      value: (row) => row.appointments,
      align: 'end',
      width: '140px',
    },
    {
      key: 'patients',
      header: 'patients.title',
      value: (row) => row.patients,
      align: 'end',
      width: '130px',
    },
    {
      key: 'share',
      header: 'common.total',
      value: (row) => `${row.share}%`,
      align: 'end',
      width: '110px',
    },
  ];

  protected readonly recordCount = computed(() => {
    const from = startOfDay(this.from()).getTime();
    const to = startOfDay(addDays(this.to(), 1)).getTime();

    return this.records.records().filter((record) => {
      const time = new Date(record.occurredAt).getTime();
      return time >= from && time < to;
    }).length;
  });

  // --------------------------------------------------------------- actions --

  protected setFrom(value: Date | null): void {
    if (value) {
      this.from.set(value);
    }
  }

  protected setTo(value: Date | null): void {
    if (value) {
      this.to.set(value);
    }
  }

  protected quickRange(days: number): void {
    this.to.set(new Date());
    this.from.set(startOfDay(addDays(new Date(), -days + 1)));
  }

  protected thisMonth(): void {
    this.from.set(startOfMonth(new Date()));
    this.to.set(new Date());
  }

  /**
   * CSV export of the doctor-performance table.
   *
   * A client-side Blob rather than a server round trip: the figures are already
   * computed here, and there is no reporting endpoint to ask.
   */
  protected exportCsv(): void {
    const rows = this.performance();
    if (!rows.length) {
      this.notifications.info(this.translate.instant('reports.empty'));
      return;
    }

    const header = ['Doctor', 'Specialization', 'Appointments', 'Patients', 'Share %'];
    const body = rows.map((row) => [
      row.name,
      row.specialization,
      row.appointments,
      row.patients,
      row.share,
    ]);

    const csv = [header, ...body]
      .map((line) => line.map((cell) => `"${String(cell).replace(/"/g, '""')}"`).join(','))
      .join('\n');

    // The BOM makes Excel open UTF-8 correctly, which matters for Arabic names.
    const blob = new Blob([`﻿${csv}`], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');

    link.href = url;
    link.download = `doctor-performance-${this.from().toISOString().slice(0, 10)}.csv`;
    link.click();
    URL.revokeObjectURL(url);
  }

  protected print(): void {
    window.print();
  }

  private load(): void {
    this.loading.set(true);

    forkJoin({
      appointments: this.appointmentsApi
        .getAppointments({ pageIndex: 1, pageSize: 1000 })
        .pipe(catchError(() => of(EMPTY_PAGE))),
      doctors: this.doctorsApi
        .getDoctors({ pageIndex: 1, pageSize: 200 })
        .pipe(catchError(() => of(EMPTY_PAGE))),
      patients: this.patientsApi
        .getPatients({ pageIndex: 1, pageSize: 1000 })
        .pipe(catchError(() => of(EMPTY_PAGE))),
    }).subscribe({
      next: ({ appointments, doctors, patients }) => {
        this.appointments.set(appointments.data as Appointment[]);
        this.doctors.set(doctors.data as Doctor[]);
        this.patients.set(patients.data as Patient[]);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }
}
