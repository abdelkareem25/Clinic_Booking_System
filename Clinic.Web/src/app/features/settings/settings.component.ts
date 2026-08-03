import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatTabsModule } from '@angular/material/tabs';
import { MatTimepickerModule } from '@angular/material/timepicker';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';

import { PermissionService } from '../../core/authz/permission.service';
import { ClinicSettingsStore } from '../../core/data/clinic-settings.store';
import { AppLanguage } from '../../core/i18n/locale.model';
import { LocaleService } from '../../core/i18n/locale.service';
import { WEEK_DAYS, WeekDay } from '../../core/models/schedule.model';
import { NotificationService } from '../../core/services/notification.service';
import { ThemeService } from '../../core/services/theme.service';
import { dateToMinutes, formatTime12, minutesToDate } from '../../core/utils/date.util';
import { timeRangeValidator } from '../../core/utils/validators';
import { CardComponent } from '../../shared/ui/card/card.component';
import { FieldErrorComponent } from '../../shared/ui/field-error/field-error.component';
import { IconComponent } from '../../shared/ui/icon/icon.component';
import { PageHeaderComponent } from '../../shared/ui/page-header/page-header.component';

/** Options offered for slot length, in minutes. */
const SLOT_OPTIONS = [10, 15, 20, 30, 45, 60] as const;
const BUFFER_OPTIONS = [0, 5, 10, 15] as const;
const REMINDER_OPTIONS = [15, 30, 60, 120, 1440] as const;
const CURRENCIES = ['EGP', 'USD', 'EUR', 'SAR', 'AED'] as const;

@Component({
  selector: 'app-settings',
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatSlideToggleModule,
    MatTabsModule,
    MatTimepickerModule,
    TranslatePipe,
    CardComponent,
    FieldErrorComponent,
    IconComponent,
    PageHeaderComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './settings.component.html',
  styleUrl: './settings.component.scss',
})
export class SettingsComponent {
  private readonly formBuilder = inject(FormBuilder);
  private readonly notifications = inject(NotificationService);
  private readonly store = inject(ClinicSettingsStore);
  private readonly translate = inject(TranslateService);

  protected readonly locale = inject(LocaleService);
  protected readonly permissions = inject(PermissionService);
  protected readonly theme = inject(ThemeService);

  protected readonly weekDays = WEEK_DAYS;
  protected readonly slotOptions = SLOT_OPTIONS;
  protected readonly bufferOptions = BUFFER_OPTIONS;
  protected readonly reminderOptions = REMINDER_OPTIONS;
  protected readonly currencies = CURRENCIES;

  protected readonly submitted = signal(false);
  protected readonly canManage = computed(() => this.permissions.can('settings.manage'));

  private readonly current = this.store.settings();

  protected readonly workingDays = signal<Set<WeekDay>>(new Set(this.current.workingDays));

  protected readonly form = this.formBuilder.nonNullable.group(
    {
      clinicName: [this.current.clinicName, [Validators.required, Validators.maxLength(80)]],
      clinicPhone: [this.current.clinicPhone, [Validators.maxLength(20)]],
      clinicAddress: [this.current.clinicAddress, [Validators.maxLength(200)]],
      headDoctor: [this.current.headDoctor, [Validators.maxLength(80)]],

      startTime: this.formBuilder.control<Date | null>(
        minutesToDate(this.current.openingMinutes),
        [Validators.required]
      ),
      endTime: this.formBuilder.control<Date | null>(minutesToDate(this.current.closingMinutes), [
        Validators.required,
      ]),
      slotMinutes: [this.current.slotMinutes, [Validators.required]],
      bufferMinutes: [this.current.bufferMinutes, [Validators.required]],

      currency: [this.current.currency, [Validators.required]],
      taxRate: [this.current.taxRate, [Validators.min(0), Validators.max(100)]],
      invoicePrefix: [this.current.invoicePrefix, [Validators.maxLength(10)]],

      appointmentReminders: [this.current.appointmentReminders],
      reminderLeadMinutes: [this.current.reminderLeadMinutes],
    },
    { validators: timeRangeValidator() }
  );

  protected isWorkingDay(day: WeekDay): boolean {
    return this.workingDays().has(day);
  }

  protected toggleDay(day: WeekDay, checked: boolean): void {
    this.workingDays.update((current) => {
      const next = new Set(current);
      checked ? next.add(day) : next.delete(day);
      return next;
    });
  }

  protected setLanguage(language: AppLanguage): void {
    this.locale.use(language);
  }

  /** 12-hour preview for the reminder lead time and slot options. */
  protected minutesLabel(minutes: number): string {
    if (minutes < 60) {
      return this.translate.instant('appointments.minutes', { count: minutes });
    }
    const hours = minutes / 60;
    return hours === 24 ? '24 h' : `${hours} h`;
  }

  protected timePreview(control: 'startTime' | 'endTime'): string {
    const value = this.form.controls[control].value;
    return value ? formatTime12(value) : '—';
  }

  protected save(): void {
    this.submitted.set(true);

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const raw = this.form.getRawValue();

    this.store.update({
      clinicName: raw.clinicName.trim(),
      clinicPhone: raw.clinicPhone.trim(),
      clinicAddress: raw.clinicAddress.trim(),
      headDoctor: raw.headDoctor.trim(),
      openingMinutes: dateToMinutes(raw.startTime!),
      closingMinutes: dateToMinutes(raw.endTime!),
      slotMinutes: raw.slotMinutes,
      bufferMinutes: raw.bufferMinutes,
      workingDays: [...this.workingDays()].sort((a, b) => a - b),
      currency: raw.currency,
      taxRate: raw.taxRate,
      invoicePrefix: raw.invoicePrefix.trim(),
      appointmentReminders: raw.appointmentReminders,
      reminderLeadMinutes: raw.reminderLeadMinutes,
    });

    this.notifications.success(this.translate.instant('settings.saved'));
  }

  protected resetToDefaults(): void {
    this.store.reset();
    const defaults = this.store.settings();

    this.form.patchValue({
      clinicName: defaults.clinicName,
      clinicPhone: defaults.clinicPhone,
      clinicAddress: defaults.clinicAddress,
      headDoctor: defaults.headDoctor,
      startTime: minutesToDate(defaults.openingMinutes),
      endTime: minutesToDate(defaults.closingMinutes),
      slotMinutes: defaults.slotMinutes,
      bufferMinutes: defaults.bufferMinutes,
      currency: defaults.currency,
      taxRate: defaults.taxRate,
      invoicePrefix: defaults.invoicePrefix,
      appointmentReminders: defaults.appointmentReminders,
      reminderLeadMinutes: defaults.reminderLeadMinutes,
    });

    this.workingDays.set(new Set(defaults.workingDays));
    this.notifications.info(this.translate.instant('settings.saved'));
  }
}
