import { Injectable, computed, effect, signal } from '@angular/core';

import { WeekDay } from '../models/schedule.model';

export interface ClinicSettings {
  clinicName: string;
  clinicPhone: string;
  clinicAddress: string;
  headDoctor: string;

  /** Minutes since midnight — the shared time representation everywhere. */
  openingMinutes: number;
  closingMinutes: number;
  /** Length of one bookable slot. */
  slotMinutes: number;
  /** Gap enforced between consecutive appointments. */
  bufferMinutes: number;
  workingDays: WeekDay[];

  currency: string;
  taxRate: number;
  invoicePrefix: string;

  appointmentReminders: boolean;
  reminderLeadMinutes: number;
}

const STORAGE_KEY = 'clinic.settings';

export const DEFAULT_SETTINGS: ClinicSettings = {
  clinicName: 'Clinic',
  clinicPhone: '',
  clinicAddress: '',
  headDoctor: '',

  openingMinutes: 9 * 60,
  closingMinutes: 21 * 60,
  slotMinutes: 30,
  bufferMinutes: 0,
  workingDays: [
    WeekDay.Saturday,
    WeekDay.Sunday,
    WeekDay.Monday,
    WeekDay.Tuesday,
    WeekDay.Wednesday,
    WeekDay.Thursday,
  ],

  currency: 'EGP',
  taxRate: 0,
  invoicePrefix: 'INV',

  appointmentReminders: true,
  reminderLeadMinutes: 60,
};

/**
 * Clinic-wide configuration.
 *
 * Slot length lives here rather than per doctor because it is a property of how
 * the clinic runs its diary: change it once and every booking screen re-slices
 * the day. The Appointments module reads `slotMinutes` and `bufferMinutes` when
 * generating available times, so this is the single knob that controls booking
 * granularity.
 */
@Injectable({ providedIn: 'root' })
export class ClinicSettingsStore {
  private readonly state = signal<ClinicSettings>(this.load());

  readonly settings = this.state.asReadonly();

  readonly currency = computed(() => this.state().currency);
  readonly slotMinutes = computed(() => this.state().slotMinutes);

  constructor() {
    effect(() => {
      const settings = this.state();
      try {
        localStorage.setItem(STORAGE_KEY, JSON.stringify(settings));
      } catch {
        /* storage blocked — settings apply for this session only */
      }
    });
  }

  update(patch: Partial<ClinicSettings>): void {
    this.state.update((current) => ({ ...current, ...patch }));
  }

  reset(): void {
    this.state.set({ ...DEFAULT_SETTINGS });
  }

  private load(): ClinicSettings {
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      if (raw) {
        // Merged over the defaults so a settings object written by an older
        // version gains new keys instead of leaving them undefined.
        return { ...DEFAULT_SETTINGS, ...(JSON.parse(raw) as Partial<ClinicSettings>) };
      }
    } catch {
      /* corrupt payload — fall through to defaults */
    }
    return { ...DEFAULT_SETTINGS };
  }
}
