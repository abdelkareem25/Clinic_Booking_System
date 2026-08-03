import { Injectable } from '@angular/core';
import { TranslateLoader, TranslationObject } from '@ngx-translate/core';
import { Observable, of } from 'rxjs';

import { AppLanguage } from './locale.model';
import { AR } from './translations/ar';
import { EN } from './translations/en';

const CATALOGUES: Record<AppLanguage, unknown> = {
  en: EN,
  ar: AR,
};

/**
 * Serves the translation catalogues straight from the bundle.
 *
 * The usual `TranslateHttpLoader` fetches JSON from `/assets`, which costs a
 * request on boot, cannot be type-checked, and shows untranslated keys for the
 * first frame after a language switch. The catalogues here are TypeScript, so
 * `ar.ts` is compile-time checked against `en.ts` and switching language is
 * synchronous.
 */
@Injectable({ providedIn: 'root' })
export class StaticTranslateLoader extends TranslateLoader {
  getTranslation(lang: string): Observable<TranslationObject> {
    const catalogue = CATALOGUES[lang as AppLanguage] ?? CATALOGUES.en;
    return of(catalogue as TranslationObject);
  }
}
