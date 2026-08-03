import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatChipInputEvent, MatChipsModule } from '@angular/material/chips';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { Router } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';

import { BLOOD_GROUPS, BloodGroup } from '../../../core/data/patient-profile.store';
import { GENDERS } from '../../../core/models/patient.model';
import { NotificationService } from '../../../core/services/notification.service';
import { toDateOnly } from '../../../core/utils/date.util';
import {
  nameValidators,
  nationalIdValidator,
  notFutureValidator,
  phoneValidator,
} from '../../../core/utils/validators';
import { CardComponent } from '../../../shared/ui/card/card.component';
import { FieldErrorComponent } from '../../../shared/ui/field-error/field-error.component';
import { IconComponent } from '../../../shared/ui/icon/icon.component';
import { PageHeaderComponent } from '../../../shared/ui/page-header/page-header.component';
import { PatientsFacade } from '../patients.facade';

/** The three free-list clinical fields, rendered identically as chip inputs. */
type ChipField = 'allergies' | 'chronicDiseases' | 'currentMedications';

@Component({
  selector: 'app-patient-form',
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatChipsModule,
    MatDatepickerModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    TranslatePipe,
    CardComponent,
    FieldErrorComponent,
    IconComponent,
    PageHeaderComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './patient-form.component.html',
  styleUrl: './patient-form.component.scss',
})
export class PatientFormComponent {
  private readonly facade = inject(PatientsFacade);
  private readonly formBuilder = inject(FormBuilder);
  private readonly notifications = inject(NotificationService);
  private readonly router = inject(Router);
  private readonly translate = inject(TranslateService);

  /** Bound from the route; absent when creating. */
  readonly id = input<string | undefined>();

  protected readonly genders = GENDERS;
  protected readonly bloodGroups = BLOOD_GROUPS;
  protected readonly maxDate = new Date();

  protected readonly loading = signal(false);
  protected readonly saving = signal(false);
  protected readonly submitted = signal(false);

  protected readonly allergies = signal<string[]>([]);
  protected readonly chronicDiseases = signal<string[]>([]);
  protected readonly currentMedications = signal<string[]>([]);

  protected readonly patientId = computed(() => {
    const raw = this.id();
    return raw ? Number(raw) : null;
  });

  protected readonly isEdit = computed(() => this.patientId() !== null);

  protected readonly form = this.formBuilder.nonNullable.group({
    // identity
    name: ['', nameValidators],
    dateOfBirth: this.formBuilder.control<Date | null>(null, [
      Validators.required,
      notFutureValidator,
    ]),
    gender: ['Male', [Validators.required]],
    nationalId: ['', [nationalIdValidator]],

    // contact
    phone: ['', [Validators.required, phoneValidator]],
    address: ['', [Validators.maxLength(200)]],
    emergencyContactName: ['', [Validators.maxLength(80)]],
    emergencyContactPhone: ['', [phoneValidator]],
    emergencyRelation: ['', [Validators.maxLength(40)]],

    // clinical
    bloodGroup: this.formBuilder.control<BloodGroup | ''>(''),
    notes: ['', [Validators.maxLength(1000)]],
  });

  constructor() {
    // `input()` is resolved by the router before the first effect runs, so the
    // record can be fetched straight from the constructor.
    queueMicrotask(() => this.loadIfEditing());
  }

  protected chipsFor(field: ChipField): string[] {
    return field === 'allergies'
      ? this.allergies()
      : field === 'chronicDiseases'
        ? this.chronicDiseases()
        : this.currentMedications();
  }

  protected addChip(field: ChipField, event: MatChipInputEvent): void {
    const value = event.value.trim();
    event.chipInput.clear();

    if (!value) {
      return;
    }

    const signalRef = this.signalFor(field);
    // Case-insensitive de-dupe: "Penicillin" and "penicillin" are one allergy,
    // and a duplicated allergy in a chart is a real safety problem.
    if (signalRef().some((entry) => entry.toLowerCase() === value.toLowerCase())) {
      return;
    }

    signalRef.update((entries) => [...entries, value]);
  }

  protected removeChip(field: ChipField, value: string): void {
    this.signalFor(field).update((entries) => entries.filter((entry) => entry !== value));
  }

  protected submit(): void {
    this.submitted.set(true);

    if (this.form.invalid || this.saving()) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    const raw = this.form.getRawValue();

    const payload = {
      name: raw.name.trim(),
      phone: raw.phone.trim(),
      dateOfBirth: toDateOnly(raw.dateOfBirth!),
      gender: raw.gender,
    };

    const profile = {
      nationalId: raw.nationalId.trim() || undefined,
      bloodGroup: raw.bloodGroup || undefined,
      address: raw.address.trim() || undefined,
      emergencyContactName: raw.emergencyContactName.trim() || undefined,
      emergencyContactPhone: raw.emergencyContactPhone.trim() || undefined,
      emergencyRelation: raw.emergencyRelation.trim() || undefined,
      allergies: this.allergies(),
      chronicDiseases: this.chronicDiseases(),
      currentMedications: this.currentMedications(),
      notes: raw.notes.trim() || undefined,
    };

    const id = this.patientId();
    const request = id
      ? this.facade.update(id, payload, profile)
      : this.facade.create(payload, profile);

    request.subscribe({
      next: (patient) => {
        this.saving.set(false);
        this.notifications.success(
          this.translate.instant(id ? 'patients.updated' : 'patients.created')
        );
        void this.router.navigate(['/patients', patient.id]);
      },
      error: () => this.saving.set(false),
    });
  }

  protected cancel(): void {
    const id = this.patientId();
    void this.router.navigate(id ? ['/patients', id] : ['/patients']);
  }

  private signalFor(field: ChipField) {
    return field === 'allergies'
      ? this.allergies
      : field === 'chronicDiseases'
        ? this.chronicDiseases
        : this.currentMedications;
  }

  private loadIfEditing(): void {
    const id = this.patientId();
    if (id === null) {
      return;
    }

    this.loading.set(true);
    this.facade.get(id).subscribe({
      next: (patient) => {
        this.form.patchValue({
          name: patient.name,
          dateOfBirth: patient.dateOfBirth ? new Date(patient.dateOfBirth) : null,
          gender: String(patient.gender),
          nationalId: patient.profile.nationalId ?? '',
          phone: patient.phone,
          address: patient.profile.address ?? '',
          emergencyContactName: patient.profile.emergencyContactName ?? '',
          emergencyContactPhone: patient.profile.emergencyContactPhone ?? '',
          emergencyRelation: patient.profile.emergencyRelation ?? '',
          bloodGroup: patient.profile.bloodGroup ?? '',
          notes: patient.profile.notes ?? '',
        });

        this.allergies.set([...patient.profile.allergies]);
        this.chronicDiseases.set([...patient.profile.chronicDiseases]);
        this.currentMedications.set([...patient.profile.currentMedications]);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }
}
