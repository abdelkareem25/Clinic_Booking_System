import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { Appointment } from '../../core/models/appointment.model';
import { DashboardData, DashboardMetric } from '../../core/models/statistics.model';
import { StatisticsService } from '../../core/services/statistics.service';
import {
  appointmentStatusTone,
  deriveAppointmentStatus
} from '../../core/utils/appointment-status.util';
import { DataTableComponent } from '../../shared/components/data-table/data-table.component';
import { TableColumn } from '../../shared/components/data-table/data-table.model';
import { MetricCardComponent } from '../../shared/components/metric-card/metric-card.component';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';

@Component({
  selector: 'app-dashboard',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, PageHeaderComponent, MetricCardComponent, DataTableComponent],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss'
})
export class DashboardComponent {
  private readonly statistics = inject(StatisticsService);

  readonly loading = signal(true);
  readonly data = signal<DashboardData | null>(null);

  readonly metrics = computed<DashboardMetric[]>(() => {
    const data = this.data();
    return [
      { label: 'Total Doctors', value: data?.totalDoctors ?? 0, icon: 'medical_services', tone: 'primary' },
      { label: 'Total Patients', value: data?.totalPatients ?? 0, icon: 'groups', tone: 'accent' },
      { label: 'Total Appointments', value: data?.totalAppointments ?? 0, icon: 'event_note', tone: 'success' },
      { label: "Today's Appointments", value: data?.todaysAppointments ?? 0, icon: 'today', tone: 'warning' }
    ];
  });

  readonly recent = computed(() => this.data()?.recentAppointments ?? []);

  readonly columns: TableColumn<Appointment>[] = [
    { key: 'patientName', header: 'Patient', value: (row) => row.patientName, variant: 'strong' },
    { key: 'doctorName', header: 'Doctor', value: (row) => row.doctorName },
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
    this.load();
  }

  private load(): void {
    this.loading.set(true);
    this.statistics.getDashboardData().subscribe({
      next: (data) => {
        this.data.set(data);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }
}
