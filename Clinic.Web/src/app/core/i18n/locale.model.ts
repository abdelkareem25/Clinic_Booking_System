import { Direction } from '@angular/cdk/bidi';

export type AppLanguage = 'en' | 'ar';

export interface LanguageDefinition {
  code: AppLanguage;
  /** Name written in its own language — never translated. */
  nativeName: string;
  englishName: string;
  direction: Direction;
  /** BCP-47 tag used for Intl date/number formatting and the Material date adapter. */
  locale: string;
}

export const LANGUAGES: readonly LanguageDefinition[] = [
  {
    code: 'en',
    nativeName: 'English',
    englishName: 'English',
    direction: 'ltr',
    locale: 'en-GB',
  },
  {
    code: 'ar',
    nativeName: 'العربية',
    englishName: 'Arabic',
    direction: 'rtl',
    // Egypt: Gregorian calendar with Arabic month names, which is what clinics
    // here actually use — `ar-SA` would switch to the Hijri calendar.
    locale: 'ar-EG',
  },
] as const;

export const DEFAULT_LANGUAGE: AppLanguage = 'en';

export const LANGUAGE_STORAGE_KEY = 'clinic.lang';

export function findLanguage(code: string | null | undefined): LanguageDefinition {
  return LANGUAGES.find((lang) => lang.code === code) ?? LANGUAGES[0];
}
