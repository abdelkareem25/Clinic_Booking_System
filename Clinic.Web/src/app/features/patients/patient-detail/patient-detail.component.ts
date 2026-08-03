import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatMenuModule } from '@angular/material/menu';
import { MatTabsModule } from '@angular/material/tabs';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Router, RouterLink } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { catchError, forkJoin, of } from 'rxjs';

import { PermissionService } from '../../../core/authz/permission.service';
import { AccountsStore } from '../../../core/data/accounts.store';
import { ClinicSettingsStore } from '../../../core/data/clinic-settings.store';
import {
  MedicalRecord,
  MedicalRecordsStore,
  RECORD_TYPE_META,
} from '../../../core/data/medical-records.store';
import { Appointment } from '../../../core/models/appointment.model';
import { Doctor } from '../../../core/models/doctor.model';
import { NotificationService } from '../../../core/services/notification.service';
import { AppointmentsService } from '../../../core/services/appointments.service';
import { DoctorsService } from '../../../core/services/doctors.service';
import { parseDate } from '../../../core/utils/date.util';
import { CardComponent } from '../../../shared/ui/card/card.component';
import { confirmDialog } from '../../../shared/ui/confirm-dialog/confirm-dialog.component';
import { CellTemplateDirective } from '../../../shared/ui/data-table/data-table.model';
import { DetailItem, DetailListComponent } from '../../../shared/ui/detail-list/detail-list.component';
import { EmptyStateComponent } from '../../../shared/ui/empty-state/empty-state.component';
import { IconComponent } from '../../../shared/ui/icon/icon.component';
import { PageHeaderComponent } from '../../../shared/ui/page-header/page-header.component';
import { TimelineComponent, TimelineEntry } from '../../../shared/ui/timeline/timeline.component';
import {
  RecordDialogData,
  RecordFormDialogComponent,
} from '../../records/record-form-dialog/record-form-dialog.component';
import { PatientView, PatientsFacade } from '../patients.facade';

@Component({
  selector: 'app-patient-detail',
  imports: [
    DatePipe,
    RouterLink,
    MatButtonModule,
    MatMenuModule,
    MatTabsModule,
    MatTooltipModule,
    TranslatePipe,
    CardComponent,
    CellTemplateDirective,
    DetailListComponent,
    EmptyStateComponent,
    IconComponent,
    PageHeaderComponent,
    TimelineComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './patient-detail.component.html',
  styleUrl: './patient-detail.component.scss',
})
export class PatientDetailComponent {
  private readonly appointmentsApi = inject(AppointmentsService);
  private readonly dialog = inject(MatDialog);
  private readonly doctorsApi = inject(DoctorsService);
  private readonly facade = inject(PatientsFacade);
  private readonly notifications = inject(NotificationService);
  private readonly records = inject(MedicalRecordsStore);
  private readonly router = inject(Router);
  private readonly translate = inject(TranslateService);

  protected readonly accounts = inject(AccountsStore);
  protected readonly permissions = inject(PermissionService);
  protected readonly settings = inject(ClinicSettingsStore);

  readonly id = input.required<string>();

  protected readonly patient = signal<PatientView | null>(null);
  protected readonly appointments = signal<Appointment[]>([]);
  protected readonly doctors = signal<Doctor[]>([]);
  protected readonly loading = signal(true);

  protected readonly patientId = computed(() => Number(this.id()));

  constructor() {
    queueMicrotask(() => this.load());
  }

  // ------------------------------------------------------------- overview --

  protected readonly identityItems = computed<DetailItem[]>(() => {
    const patient = this.patient();
    if (!patient) {
      return [];
    }

    return [
      { label: 'patients.fileNumber', value: patient.profile.fileNumber, icon: 'note' },
      {
        label: 'patients.dateOfBirth',
        value: patient.dateOfBirth ? new Date(patient.dateOfBirth).toLocaleDateString() : null,
        icon: 'birthday',
      },
      {
        label: 'patients.age',
        value: patient.age === null ? null : this.translate.instant('patients.ageYears', { count: patient.age }),
      },
      {
        label: 'patients.gender',
        value: this.translate.instant(
          patient.gender === 'Male'
            ? 'patients.genderMale'
            : patient.gender === 'Female'
              ? 'patients.genderFemale'
              : 'patients.genderOther'
        ),
      },
      { label: 'patients.nationalId', value: patient.profile.nationalId },
      { label: 'patients.bloodGroup', value: patient.profile.bloodGroup, icon: 'bloodGroup', tone: 'strong' },
    ];
  });

  protected readonly contactItems = computed<DetailItem[]>(() => {
    const profile = this.patient()?.profile;
    const patient = this.patient();
    if (!patient || !profile) {
      return [];
    }

    return [
      { label: 'patients.phone', value: patient.phone, icon: 'phone', tone: 'strong' },
      { label: 'patients.emergencyContactName', value: profile.emergencyContactName },
      { label: 'patients.emergencyContactPhone', value: profile.emergencyContactPhone },
      { label: 'patients.emergencyRelation', value: profile.emergencyRelation },
      { label: 'patients.address', value: profile.address, icon: 'address', wide: true },
    ];
  });

  // ------------------------------------------------------------- timeline --

  protected readonly patientRecords = computed(() => {
    // Touch the store signal so the timeline updates the moment a record is added.
    this.records.records();
    return this.records.forPatient(this.patientId());
  });

  protected readonly timeline = computed<TimelineEntry[]>(() =>
    this.patientRecords().map((record) => {
      const meta = RECORD_TYPE_META[record.type];
      return {
        id: record.id,
        date: record.occurredAt,
        typeLabel: meta.label,
        icon: meta.icon,
        tone: meta.tone,
        title: record.title,
        summary: record.diagnosis || record.complaint,
        author: record.doctorName ?? record.recordedBy,
        tags: record.tags,
        facts: this.factsFor(record),
      };
    })
  );

  // --------------------------------------------------------- appointments --

  protected readonly patientAppointments = computed(() =>
    [...this.appointments()].sort((a, b) => b.appointmentDate.localeCompare(a.appointmentDate))
  );

  protected readonly lastVisit = computed(() => {
    const now = Date.now();
    return this.patientAppointments().find((appointment) => {
      const date = parseDate(appointment.appointmentDate);
      return date ? date.getTime() <= now : false;
    });
  });

  protected readonly nextVisit = computed(() => {
    const now = Date.now();
    return [...this.patientAppointments()]
      .reverse()
      .find((appointment) => {
        const date = parseDate(appointment.appointmentDate);
        return date ? date.getTime() > now : false;
      });
  });

  // ------------------------------------------------------------- billing --

  protected readonly invoices = computed(() =>
    this.accounts.invoices().filter((invoice) => invoice.patientId === this.patientId())
  );

  protected readonly balance = computed(() =>
    this.invoices().reduce((sum, invoice) => sum + invoice.remaining, 0)
  );

  protected readonly currency = computed(() => this.settings.settings().currency);

  // ------------------------------------------------------------- actions --

  protected addRecord(record?: MedicalRecord): void {
    const patient = this.patient();
    if (!patient) {
      return;
    }

    this.dialog.open<RecordFormDialogComponent, RecordDialogData, boolean>(
      RecordFormDialogComponent,
      {
        data: {
          patientId: patient.id,
          patientName: patient.name,
          doctors: this.doctors(),
          record,
        },
      }
    );
  }

  protected deleteRecord(entry: TimelineEntry): void {
    const record = this.records.getById(entry.id);
    if (!record) {
      return;
    }

    confirmDialog(this.dialog, {
      title: 'records.delete',
      message: 'records.deleteConfirm',
      messageParams: { date: new Date(record.occurredAt).toLocaleDateString() },
      confirmLabel: 'common.delete',
      tone: 'danger',
    }).subscribe((confirmed) => {
      if (confirmed) {
        this.records.remove(record.id);
        this.notifications.success(this.translate.instant('records.deleted'));
      }
    });
  }

  protected editRecord(entry: TimelineEntry): void {
    const record = this.records.getById(entry.id);
    if (record) {
      this.addRecord(record);
    }
  }

  protected confirmDelete(): void {
    const patient = this.patient();
    if (!patient) {
      return;
    }

    confirmDialog(this.dialog, {
      title: 'patients.delete',
      message: 'patients.deleteConfirm',
      messageParams: { name: patient.name },
      confirmLabel: 'common.delete',
      tone: 'danger',
    }).subscribe((confirmed) => {
      if (!confirmed) {
        return;
      }

      this.facade.remove(patient.id).subscribe(() => {
        this.notifications.success(this.translate.instant('patients.deleted'));
        void this.router.navigate(['/patients']);
      });
    });
  }

  private load(): void {
    this.loading.set(true);
    const id = this.patientId();

    forkJoin({
      patient: this.facade.get(id),
      // Filtered client-side: the endpoint takes a patient *name*, which is not
      // unique enough to trust for a clinical record.
      appointments: this.appointmentsApi
        .getAppointments({ patientId: id, pageIndex: 1, pageSize: 100 })
        .pipe(catchError(() => of({ pageIndex: 1, pageSize: 0, count: 0, data: [] }))),
      doctors: this.doctorsApi
        .getDoctors({ pageIndex: 1, pageSize: 100 })
        .pipe(catchError(() => of({ pageIndex: 1, pageSize: 0, count: 0, data: [] }))),
    }).subscribe({
      next: ({ patient, appointments, doctors }) => {
        this.patient.set(patient);
        this.appointments.set(appointments.data.filter((entry) => entry.patientId === id || !entry.patientId));
        this.doctors.set(doctors.data);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  private factsFor(record: MedicalRecord): { label: string; value: string }[] {
    const facts: { label: string; value: string }[] = [];
    const vitals = record.vitals;

    if (vitals?.bloodPressure) {
      facts.push({ label: 'records.bloodPressure', value: vitals.bloodPressure });
    }
    if (vitals?.temperature !== undefined) {
      facts.push({ label: 'records.temperature', value: `${vitals.temperature} °C` });
    }
    if (vitals?.pulse !== undefined) {
      facts.push({ label: 'records.pulse', value: `${vitals.pulse} bpm` });
    }
    if (vitals?.weight !== undefined) {
      facts.push({ label: 'records.weight', value: `${vitals.weight} kg` });
    }
    if (record.prescription) {
      facts.push({ label: 'records.prescription', value: record.prescription });
    }

    return facts;
  }
}
