import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';

import { DEFAULT_SPECIALIZATIONS, Doctor } from '../../../core/models/doctor.model';
import { DoctorsService } from '../../../core/services/doctors.service';
import { NotificationService } from '../../../core/services/notification.service';
import { nameValidators } from '../../../core/utils/validators';
import { FieldErrorComponent } from '../../../shared/ui/field-error/field-error.component';

export interface DoctorDialogData {
  doctor?: Doctor;
  /** Specialities already in use, offered before the built-in list. */
  knownSpecializations: string[];
}

@Component({
  selector: 'app-doctor-form-dialog',
  imports: [
    ReactiveFormsModule,
    MatAutocompleteModule,
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    TranslatePipe,
    FieldErrorComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './doctor-form-dialog.component.html',
  styleUrl: './doctor-form-dialog.component.scss',
})
export class DoctorFormDialogComponent {
  private readonly api = inject(DoctorsService);
  private readonly formBuilder = inject(FormBuilder);
  private readonly notifications = inject(NotificationService);
  private readonly translate = inject(TranslateService);

  readonly dialogRef = inject<MatDialogRef<DoctorFormDialogComponent, boolean>>(MatDialogRef);
  readonly data = inject<DoctorDialogData>(MAT_DIALOG_DATA);

  protected readonly isEdit = Boolean(this.data.doctor);
  protected readonly saving = signal(false);
  protected readonly submitted = signal(false);

  /** In-use specialities first, then the defaults, de-duplicated. */
  protected readonly specializations = [
    ...new Set([...this.data.knownSpecializations, ...DEFAULT_SPECIALIZATIONS]),
  ].sort((a, b) => a.localeCompare(b));

  protected readonly form = this.formBuilder.nonNullable.group({
    name: [this.data.doctor?.name ?? '', nameValidators],
    specialization: [
      this.data.doctor?.specialization ?? '',
      [Validators.required, Validators.maxLength(60)],
    ],
  });

  protected submit(): void {
    this.submitted.set(true);

    if (this.form.invalid || this.saving()) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    const raw = this.form.getRawValue();
    const payload = { name: raw.name.trim(), specialization: raw.specialization.trim() };

    const request = this.data.doctor
      ? this.api.updateDoctor(this.data.doctor.id, { ...payload, id: this.data.doctor.id })
      : this.api.createDoctor(payload);

    request.subscribe({
      next: () => {
        this.saving.set(false);
        this.notifications.success(
          this.translate.instant(this.isEdit ? 'doctors.updated' : 'doctors.created')
        );
        this.dialogRef.close(true);
      },
      error: () => this.saving.set(false),
    });
  }
}
