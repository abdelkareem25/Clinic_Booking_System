import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { MatButtonModule } from '@angular/material/button';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatStepperModule } from '@angular/material/stepper';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { catchError, forkJoin, of } from 'rxjs';

import { NotificationsStore } from '../../../core/data/notifications.store';
import { Appointment } from '../../../core/models/appointment.model';
import { Doctor } from '../../../core/models/doctor.model';
import { Patient } from '../../../core/models/patient.model';
import { DoctorSchedule } from '../../../core/models/schedule.model';
import { AppointmentsService } from '../../../core/services/appointments.service';
import { DoctorsService } from '../../../core/services/doctors.service';
import { NotificationService } from '../../../core/services/notification.service';
import { PatientsService } from '../../../core/services/patients.service';
import { SchedulesService } from '../../../core/services/schedules.service';
import {
  formatTimeRange,
  minutesToDate,
  parseDate,
  toLocalIso,
} from '../../../core/utils/date.util';
import { CardComponent } from '../../../shared/ui/card/card.component';
import { EmptyStateComponent } from '../../../shared/ui/empty-state/empty-state.component';
import { FieldErrorComponent } from '../../../shared/ui/field-error/field-error.component';
import { IconComponent } from '../../../shared/ui/icon/icon.component';
import { PageHeaderComponent } from '../../../shared/ui/page-header/page-header.component';
import { AvailabilityService, TimeSlot } from '../availability.service';

const EMPTY_PAGE = { pageIndex: 1, pageSize: 0, count: 0, data: [] };

@Component({
  selector: 'app-appointment-form',
  imports: [
    ReactiveFormsModule,
    MatAutocompleteModule,
    MatButtonModule,
    MatDatepickerModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatStepperModule,
    TranslatePipe,
    CardComponent,
    EmptyStateComponent,
    FieldErrorComponent,
    IconComponent,
    PageHeaderComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './appointment-form.component.html',
  styleUrl: './appointment-form.component.scss',
})
export class AppointmentFormComponent {
  private readonly api = inject(AppointmentsService);
  private readonly availability = inject(AvailabilityService);
  private readonly doctorsApi = inject(DoctorsService);
  private readonly formBuilder = inject(FormBuilder);
  private readonly notifications = inject(NotificationService);
  private readonly notificationsStore = inject(NotificationsStore);
  private readonly patientsApi = inject(PatientsService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly schedulesApi = inject(SchedulesService);
  private readonly translate = inject(TranslateService);

  /** Present when rescheduling. */
  readonly id = input<string | undefined>();

  protected readonly doctors = signal<Doctor[]>([]);
  protected readonly patients = signal<Patient[]>([]);
  protected readonly schedules = signal<DoctorSchedule[]>([]);
  protected readonly appointments = signal<Appointment[]>([]);

  protected readonly loading = signal(true);
  protected readonly saving = signal(false);
  protected readonly submitted = signal(false);
  protected readonly patientFilter = signal('');

  protected readonly form = this.formBuilder.nonNullable.group({
    doctorId: this.formBuilder.control<number | null>(null, [Validators.required]),
    patientId: this.formBuilder.control<number | null>(null, [Validators.required]),
    date: this.formBuilder.control<Date | null>(null, [Validators.required]),
    startMinutes: this.formBuilder.control<number | null>(null, [Validators.required]),
    reason: ['', [Validators.maxLength(200)]],
  });

  /** Mirrors the reactive controls into signals so `computed` can depend on them. */
  protected readonly selectedDoctorId = signal<number | null>(null);
  protected readonly selectedDate = signal<Date | null>(null);
  protected readonly selectedSlot = signal<number | null>(null);

  protected readonly appointmentId = computed(() => {
    const raw = this.id();
    return raw ? Number(raw) : null;
  });

  protected readonly isEdit = computed(() => this.appointmentId() !== null);

  protected readonly minDate = new Date();

  constructor() {
    this.form.controls.doctorId.valueChanges.subscribe((doctorId) => {
      this.selectedDoctorId.set(doctorId);
      // A date valid for the previous doctor is rarely valid for the new one,
      // and a stale slot would submit a time this doctor does not work.
      this.form.controls.date.setValue(null);
      this.form.controls.startMinutes.setValue(null);
      this.selectedDate.set(null);
      this.selectedSlot.set(null);
    });

    this.form.controls.date.valueChanges.subscribe((date) => {
      this.selectedDate.set(date);
      this.form.controls.startMinutes.setValue(null);
      this.selectedSlot.set(null);
    });

    this.form.controls.startMinutes.valueChanges.subscribe((value) =>
      this.selectedSlot.set(value)
    );

    queueMicrotask(() => this.load());
  }

  // --------------------------------------------------------- derived data --

  protected readonly selectedDoctor = computed(() =>
    this.doctors().find((doctor) => doctor.id === this.selectedDoctorId()) ?? null
  );

  protected readonly selectedPatient = computed(() =>
    this.patients().find((patient) => patient.id === this.form.controls.patientId.value) ?? null
  );

  protected readonly filteredPatients = computed(() => {
    const term = this.patientFilter().trim().toLowerCase();
    if (!term) {
      return this.patients().slice(0, 50);
    }

    // Matches on name or phone, because reception searches by whichever the
    // caller gives them first.
    return this.patients()
      .filter(
        (patient) =>
          patient.name.toLowerCase().includes(term) ||
          patient.phone.replace(/\D/g, '').includes(term.replace(/\D/g, ''))
      )
      .slice(0, 50);
  });

  /** Working days for the chosen doctor, as a datepicker filter. */
  protected readonly dateFilter = computed(() =>
    this.availability.dateFilter(this.schedules(), this.selectedDoctorId())
  );

  protected readonly workingDayLabels = computed(() => {
    const days = [...this.availability.workingDays(this.schedules(), this.selectedDoctorId())];
    return days
      .sort((a, b) => a - b)
      .map((day) => this.translate.instant(`weekday.short.${day}`))
      .join(' · ');
  });

  protected readonly dayAvailability = computed(() =>
    this.availability.slotsFor(
      this.selectedDate(),
      this.selectedDoctorId(),
      this.schedules(),
      this.appointments(),
      this.appointmentId() ?? undefined
    )
  );

  protected readonly shiftLabel = computed(() =>
    this.dayAvailability()
      .shifts.map((shift) => formatTimeRange(shift.startTime, shift.endTime))
      .join(', ')
  );

  protected readonly selectedSlotLabel = computed(() => {
    const minutes = this.selectedSlot();
    return minutes === null
      ? null
      : (this.dayAvailability().slots.find((slot) => slot.start === minutes)?.label ?? null);
  });

  protected readonly hasDoctorsWithSchedules = computed(() =>
    this.doctors().some((doctor) =>
      this.schedules().some((schedule) => schedule.doctorId === doctor.id)
    )
  );

  // -------------------------------------------------------------- actions --

  protected selectSlot(slot: TimeSlot): void {
    if (!slot.available) {
      return;
    }
    this.form.controls.startMinutes.setValue(slot.start);
  }

  protected onPatientInput(value: string): void {
    this.patientFilter.set(value);
  }

  protected displayPatient = (patientId: number | null): string => {
    const patient = this.patients().find((entry) => entry.id === patientId);
    return patient ? `${patient.name} — ${patient.phone}` : '';
  };

  protected submit(): void {
    this.submitted.set(true);

    if (this.form.invalid || this.saving()) {
      this.form.markAllAsTouched();
      return;
    }

    const { doctorId, patientId, date, startMinutes, reason } = this.form.getRawValue();

    // Re-check at submit time: another workstation may have taken this slot
    // while the form was open, and the API has no uniqueness constraint to
    // catch it.
    const stillFree = this.availability.isSlotAvailable(
      date!,
      startMinutes!,
      doctorId!,
      this.schedules(),
      this.appointments(),
      this.appointmentId() ?? undefined
    );

    if (!stillFree) {
      this.form.controls.startMinutes.setErrors({ slotTaken: true });
      this.notifications.error(this.translate.instant('validation.slotTaken'));
      return;
    }

    this.saving.set(true);
    const appointmentDate = toLocalIso(minutesToDate(startMinutes!, date!));
    // The reason for the visit is now persisted. It was collected and silently
    // discarded before, because the API had nowhere to put it.
    const payload = {
      patientId: patientId!,
      doctorId: doctorId!,
      appointmentDate,
      notes: reason.trim() || null,
    };
    const id = this.appointmentId();

    const request = id ? this.api.updateAppointment(id, payload) : this.api.createAppointment(payload);

    request.subscribe({
      next: () => {
        this.saving.set(false);
        this.notifications.success(
          this.translate.instant(id ? 'appointments.updated' : 'appointments.created')
        );

        this.notificationsStore.push({
          title: this.translate.instant(id ? 'appointments.updated' : 'appointments.created'),
          body: `${this.selectedPatient()?.name ?? ''} · ${this.selectedDoctor()?.name ?? ''} · ${
            this.selectedSlotLabel() ?? ''
          }`,
          icon: 'appointments',
          tone: 'success',
          route: '/appointments',
        });

        void this.router.navigate(['/appointments']);
      },
      error: () => this.saving.set(false),
    });
  }

  protected cancel(): void {
    void this.router.navigate(['/appointments']);
  }

  private load(): void {
    this.loading.set(true);

    forkJoin({
      doctors: this.doctorsApi
        .getDoctors({ pageIndex: 1, pageSize: 200 })
        .pipe(catchError(() => of(EMPTY_PAGE))),
      patients: this.patientsApi
        .getPatients({ pageIndex: 1, pageSize: 500 })
        .pipe(catchError(() => of(EMPTY_PAGE))),
      schedules: this.schedulesApi
        .getSchedules({ pageIndex: 1, pageSize: 500 })
        .pipe(catchError(() => of(EMPTY_PAGE))),
      appointments: this.api
        .getAppointments({ pageIndex: 1, pageSize: 500 })
        .pipe(catchError(() => of(EMPTY_PAGE))),
    }).subscribe({
      next: ({ doctors, patients, schedules, appointments }) => {
        this.doctors.set(doctors.data as Doctor[]);
        this.patients.set(patients.data as Patient[]);
        this.schedules.set(schedules.data as DoctorSchedule[]);
        this.appointments.set(appointments.data as Appointment[]);
        this.loading.set(false);

        this.applyPrefill();
      },
      error: () => this.loading.set(false),
    });
  }

  /** Seeds the form from `?patientId=` or from the appointment being edited. */
  private applyPrefill(): void {
    const id = this.appointmentId();

    if (id !== null) {
      const existing = this.appointments().find((appointment) => appointment.id === id);
      if (existing) {
        const when = parseDate(existing.appointmentDate);

        // The ids are used directly. They used to be missing from the list
        // response, so this fell back to matching doctors and patients by name
        // — which silently picked the wrong person whenever two shared one.
        this.form.patchValue({
          doctorId: existing.doctorId,
          patientId: existing.patientId,
          reason: existing.notes ?? '',
        });

        if (when) {
          this.form.controls.date.setValue(when);
          this.form.controls.startMinutes.setValue(when.getHours() * 60 + when.getMinutes());
        }
      }
      return;
    }

    const patientId = Number(this.route.snapshot.queryParamMap.get('patientId'));
    if (patientId) {
      this.form.controls.patientId.setValue(patientId);
    }
  }
}
