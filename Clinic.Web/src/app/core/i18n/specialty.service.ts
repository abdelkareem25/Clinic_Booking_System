import { Injectable, Signal, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { TranslateService } from '@ngx-translate/core';

import { specialtyKey } from './specialty';

/** What a speciality filter dropdown renders: the stored code plus its label. */
export interface SpecialtyOption {
  /** The value stored in `Doctor.Specialization` — what filtering compares. */
  value: string;
  /** Localised, ready to print. */
  label: string;
}

/**
 * Localises doctor specialities.
 *
 * One service, used by every screen that shows a speciality, so the same doctor
 * reads "Cardiology" in English and "أمراض القلب" in Arabic without a single
 * component knowing which language is active.
 *
 * {@link label} is a *signal of a function* rather than a plain method. That is
 * what makes it work inside `computed()`: reading the signal registers a
 * dependency on the catalogue, so a derived list of labels recomputes the moment
 * the language changes. A plain method could not be tracked and the UI would
 * keep the previous language until something else happened to invalidate it.
 *
 *   protected readonly title = computed(() =>
 *     this.specialty.label()(this.doctor().specialization)
 *   );
 *
 * Templates use `| specialty` instead — see `SpecialtyPipe`.
 */
@Injectable({ providedIn: 'root' })
export class SpecialtyService {
  private readonly translate = inject(TranslateService);

  /**
   * Ticks after ngx-translate has swapped catalogues.
   *
   * Deliberately not `LocaleService.current`: that signal changes *before* the
   * effect that calls `translate.use()` runs, so a computed reading it could
   * translate against the outgoing catalogue.
   */
  private readonly catalogue = toSignal(this.translate.onLangChange, { initialValue: null });

  readonly label: Signal<(raw: string | null | undefined) => string> = computed(() => {
    this.catalogue();
    const translate = this.translate;

    return (raw: string | null | undefined): string => {
      const key = specialtyKey(raw);
      // Unrecognised values pass through: a clinic's own wording beats a blank.
      return key ? translate.instant(key) : (raw?.trim() ?? '');
    };
  });

  /**
   * The distinct specialities across a set of doctors, sorted by their localised
   * label so the dropdown reads alphabetically in whichever language is on.
   */
  options(specialities: readonly (string | null | undefined)[]): SpecialtyOption[] {
    const label = this.label();
    const byValue = new Map<string, SpecialtyOption>();

    for (const raw of specialities) {
      const value = raw?.trim();
      if (!value) {
        continue;
      }
      if (!byValue.has(value)) {
        byValue.set(value, { value, label: label(value) });
      }
    }

    return [...byValue.values()].sort((a, b) => a.label.localeCompare(b.label));
  }
}
