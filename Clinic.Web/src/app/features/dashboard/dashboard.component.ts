import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { RouterLink } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';

import { PermissionService } from '../../core/authz/permission.service';
import { AccountsStore } from '../../core/data/accounts.store';
import { ClinicSettingsStore } from '../../core/data/clinic-settings.store';
import { Appointment } from '../../core/models/appointment.model';
import { WeekDay } from '../../core/models/schedule.model';
import { AuthService } from '../../core/services/auth.service';
import {
  addDays,
  formatTimeRange,
  isSameDay,
  parseDate,
  startOfDay,
  startOfMonth,
} from '../../core/utils/date.util';
import { CardComponent } from '../../shared/ui/card/card.component';
import { ChartComponent, ChartPoint } from '../../shared/ui/chart/chart.component';
import { EmptyStateComponent } from '../../shared/ui/empty-state/empty-state.component';
import { IconComponent } from '../../shared/ui/icon/icon.component';
import { PageHeaderComponent } from '../../shared/ui/page-header/page-header.component';
import { StatCardComponent } from '../../shared/ui/stat-card/stat-card.component';
import { DashboardData, DashboardSnapshot } from './dashboard.data';

interface DoctorAvailability {
  id: number;
  name: string;
  specialization: string;
  hours: string | null;
  booked: number;
}

/** How many days of history the two trend charts cover. */
const TREND_DAYS = 14;

@Component({
  selector: 'app-dashboard',
  imports: [
    DatePipe,
    RouterLink,
    MatButtonModule,
    TranslatePipe,
    CardComponent,
    ChartComponent,
    EmptyStateComponent,
    IconComponent,
    PageHeaderComponent,
    StatCardComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss',
})
export class DashboardComponent {
  private readonly auth = inject(AuthService);
  private readonly data = inject(DashboardData);
  private readonly translate = inject(TranslateService);

  protected readonly accounts = inject(AccountsStore);
  protected readonly permissions = inject(PermissionService);
  protected readonly settings = inject(ClinicSettingsStore);

  private readonly snapshot = signal<DashboardSnapshot | null>(null);
  protected readonly loading = signal(true);

  protected readonly today = new Date();

  constructor() {
    this.refresh();
  }

  // ------------------------------------------------------------- greeting --

  protected readonly greeting = computed(() => {
    const hour = new Date().getHours();
    const key =
      hour < 12
        ? 'dashboard.greetingMorning'
        : hour < 17
          ? 'dashboard.greetingAfternoon'
          : 'dashboard.greetingEvening';

    const name = this.auth.currentUser?.displayName?.split(/\s+/)[0] ?? '';
    return this.translate.instant(key, { name });
  });

  // ---------------------------------------------------------- appointments --

  private readonly appointments = computed(() => this.snapshot()?.appointments ?? []);

  protected readonly todaysAppointments = computed(() =>
    this.appointments()
      .filter((appointment) => this.isOnDay(appointment, this.today))
      .sort((a, b) => a.appointmentDate.localeCompare(b.appointmentDate))
  );

  protected readonly upcomingAppointments = computed(() => {
    const startOfTomorrow = startOfDay(addDays(this.today, 1)).getTime();
    return this.appointments()
      .filter((appointment) => {
        const date = parseDate(appointment.appointmentDate);
        return date ? date.getTime() >= startOfTomorrow : false;
      })
      .sort((a, b) => a.appointmentDate.localeCompare(b.appointmentDate));
  });

  /** Distinct patients seen today — a patient with two visits is still one person. */
  protected readonly todaysPatients = computed(
    () => new Set(this.todaysAppointments().map((appointment) => appointment.patientName)).size
  );

  protected readonly nextFive = computed(() => this.upcomingAppointments().slice(0, 5));

  // -------------------------------------------------------------- finance --

  protected readonly revenueToday = computed(() =>
    this.accounts.incomeBetween(this.today, this.today)
  );

  protected readonly revenueMonth = computed(() =>
    this.accounts.incomeBetween(startOfMonth(this.today), this.today)
  );

  protected readonly recentPayments = computed(() => this.accounts.payments().slice(0, 5));

  protected readonly currency = computed(() => this.settings.settings().currency);

  // --------------------------------------------------------------- charts --

  private readonly trendDays = computed(() =>
    Array.from({ length: TREND_DAYS }, (_, index) =>
      startOfDay(addDays(this.today, index - (TREND_DAYS - 1)))
    )
  );

  protected readonly appointmentTrend = computed<ChartPoint[]>(() =>
    this.trendDays().map((day) => ({
      label: `${day.getDate()}`,
      detail: day.toLocaleDateString(undefined, { day: 'numeric', month: 'short' }),
      value: this.appointments().filter((appointment) => this.isOnDay(appointment, day)).length,
    }))
  );

  protected readonly revenueTrend = computed<ChartPoint[]>(() =>
    this.accounts.dailyIncome(this.trendDays()).map(({ date, value }) => ({
      label: `${date.getDate()}`,
      detail: date.toLocaleDateString(undefined, { day: 'numeric', month: 'short' }),
      value,
    }))
  );

  // -------------------------------------------------- doctor availability --

  protected readonly availability = computed<DoctorAvailability[]>(() => {
    const snapshot = this.snapshot();
    if (!snapshot) {
      return [];
    }

    const weekday = this.today.getDay() as WeekDay;

    return snapshot.doctors.map((doctor) => {
      const todaysShifts = snapshot.schedules.filter(
        (schedule) => schedule.doctorId === doctor.id && schedule.weekDay === weekday
      );

      return {
        id: doctor.id,
        name: doctor.name,
        specialization: doctor.specialization,
        hours: todaysShifts.length
          ? todaysShifts
              .map((shift) => formatTimeRange(shift.startTime, shift.endTime))
              .join(', ')
          : null,
        booked: this.todaysAppointments().filter(
          (appointment) => appointment.doctorName === doctor.name
        ).length,
      };
    });
  });

  protected readonly totalPatients = computed(() => this.snapshot()?.totalPatients ?? 0);

  // -------------------------------------------------------------- actions --

  protected refresh(): void {
    this.loading.set(true);
    this.data.load().subscribe({
      next: (snapshot) => {
        this.snapshot.set(snapshot);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  private isOnDay(appointment: Appointment, day: Date): boolean {
    const date = parseDate(appointment.appointmentDate);
    return date ? isSameDay(date, day) : false;
  }
}
