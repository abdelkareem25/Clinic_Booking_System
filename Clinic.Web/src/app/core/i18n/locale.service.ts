import { Directionality } from '@angular/cdk/bidi';
import { DOCUMENT } from '@angular/common';
import { Injectable, computed, effect, inject, signal } from '@angular/core';
import { DateAdapter } from '@angular/material/core';
import { TranslateService } from '@ngx-translate/core';

import {
  AppLanguage,
  DEFAULT_LANGUAGE,
  LANGUAGES,
  LANGUAGE_STORAGE_KEY,
  LanguageDefinition,
  findLanguage,
} from './locale.model';

/**
 * Owns the active language and, by extension, the writing direction.
 *
 * Switching language has to move four things in lockstep or the UI tears:
 *   1. the ngx-translate catalogue,
 *   2. `<html lang>` / `<html dir>` (which drives every logical CSS property),
 *   3. the CDK `Directionality` (which flips Material overlays, menus,
 *      datepickers and the sidenav), and
 *   4. the Material `DateAdapter` locale (month names, first day of week).
 *
 * Doing it in one place is what makes the language toggle instant and complete
 * — no reload, no half-translated frame.
 */
@Injectable({ providedIn: 'root' })
export class LocaleService {
  private readonly document = inject(DOCUMENT);
  private readonly translate = inject(TranslateService);
  private readonly directionality = inject(Directionality);
  private readonly dateAdapter = inject<DateAdapter<unknown>>(DateAdapter);

  private readonly language = signal<AppLanguage>(this.resolveInitialLanguage());

  readonly current = this.language.asReadonly();
  readonly definition = computed<LanguageDefinition>(() => findLanguage(this.language()));
  readonly direction = computed(() => this.definition().direction);
  readonly isRtl = computed(() => this.direction() === 'rtl');
  readonly available = LANGUAGES;

  constructor() {
    this.translate.addLangs(LANGUAGES.map((lang) => lang.code));
    this.translate.setFallbackLang(DEFAULT_LANGUAGE);

    effect(() => {
      const definition = this.definition();

      this.translate.use(definition.code);
      this.dateAdapter.setLocale(definition.locale);

      const root = this.document.documentElement;
      root.lang = definition.code;
      root.dir = definition.direction;

      // Material's overlay-based components read direction from the CDK
      // service, not from the DOM, so it has to be pushed explicitly.
      if (this.directionality.valueSignal() !== definition.direction) {
        this.directionality.valueSignal.set(definition.direction);
        this.directionality.change.emit(definition.direction);
      }

      this.persist(definition.code);
    });
  }

  use(language: AppLanguage): void {
    this.language.set(language);
  }

  toggle(): void {
    this.language.update((code) => (code === 'ar' ? 'en' : 'ar'));
  }

  /** Instant translation for places that cannot use the pipe (chart labels, exports). */
  instant(key: string, params?: Record<string, unknown>): string {
    return this.translate.instant(key, params);
  }

  private resolveInitialLanguage(): AppLanguage {
    const stored = this.safeRead();
    if (stored && LANGUAGES.some((lang) => lang.code === stored)) {
      return stored as AppLanguage;
    }

    const browser = this.translate.getBrowserLang();
    return browser === 'ar' ? 'ar' : DEFAULT_LANGUAGE;
  }

  private safeRead(): string | null {
    try {
      return localStorage.getItem(LANGUAGE_STORAGE_KEY);
    } catch {
      return null;
    }
  }

  private persist(language: AppLanguage): void {
    try {
      localStorage.setItem(LANGUAGE_STORAGE_KEY, language);
    } catch {
      /* storage blocked — the language simply will not survive a reload */
    }
  }
}
