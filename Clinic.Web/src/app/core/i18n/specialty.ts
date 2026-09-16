/**
 * Doctor specialities — the single source of truth for turning what the API
 * stores into what the user reads.
 *
 * `Doctor.Specialization` is a free-text `string` column holding an English
 * display name (`"Family Medicine"`). It is not an enum and it is not going to
 * be migrated: the data already exists, other clinics have typed their own
 * values into it, and a destructive migration to win a display concern would be
 * the wrong trade.
 *
 * So the stored string is treated as a *code*. This module normalises it to one
 * of {@link SPECIALTY_CODES} and hands back a translation key; the catalogues in
 * `translations/` own the Arabic and English wording. Nothing else in the
 * application decides how a speciality is spelled, and no component ever
 * branches on the active language to pick a label.
 *
 *   stored "Family Medicine" ─┐
 *   stored "family medicine" ─┼─► FamilyMedicine ─► "specialty.familyMedicine"
 *   stored "طب الأسرة"       ─┘                       ├─ en: "Family Medicine"
 *                                                     └─ ar: "طب الأسرة"
 *
 * A value that matches nothing is not an error — it is a clinic that typed
 * something we do not know. {@link specialtyKey} returns null for it and callers
 * fall back to the stored text, which is still the most useful thing to show.
 */

/** The specialities this application can localise. Stable identifiers. */
export const SPECIALTY_CODES = [
  'Cardiology',
  'Dentistry',
  'Dermatology',
  'ENT',
  'FamilyMedicine',
  'GeneralSurgery',
  'InternalMedicine',
  'Neurology',
  'ObstetricsGynecology',
  'Orthopedics',
  'Pediatrics',
  'Psychiatry',
  'Radiology',
] as const;

export type SpecialtyCode = (typeof SPECIALTY_CODES)[number];

/**
 * What gets written to `Doctor.Specialization` when a speciality is picked from
 * the built-in list.
 *
 * English on purpose: the column is the interchange format between the API, the
 * reports export and any future integration, so it stays language-neutral in the
 * sense that matters — one clinic switching the UI to Arabic must not change the
 * bytes another clinic's report is grouped by.
 */
export const SPECIALTY_STORED_VALUE: Record<SpecialtyCode, string> = {
  Cardiology: 'Cardiology',
  Dentistry: 'Dentistry',
  Dermatology: 'Dermatology',
  ENT: 'ENT',
  FamilyMedicine: 'Family Medicine',
  GeneralSurgery: 'General Surgery',
  InternalMedicine: 'Internal Medicine',
  Neurology: 'Neurology',
  ObstetricsGynecology: 'Obstetrics and Gynecology',
  Orthopedics: 'Orthopedics',
  Pediatrics: 'Pediatrics',
  Psychiatry: 'Psychiatry',
  Radiology: 'Radiology',
};

/** The `specialty.*` catalogue key for a code. */
export const SPECIALTY_TRANSLATION_KEY: Record<SpecialtyCode, string> = {
  Cardiology: 'specialty.cardiology',
  Dentistry: 'specialty.dentistry',
  Dermatology: 'specialty.dermatology',
  ENT: 'specialty.ent',
  FamilyMedicine: 'specialty.familyMedicine',
  GeneralSurgery: 'specialty.generalSurgery',
  InternalMedicine: 'specialty.internalMedicine',
  Neurology: 'specialty.neurology',
  ObstetricsGynecology: 'specialty.obstetricsGynecology',
  Orthopedics: 'specialty.orthopedics',
  Pediatrics: 'specialty.pediatrics',
  Psychiatry: 'specialty.psychiatry',
  Radiology: 'specialty.radiology',
};

/**
 * Every spelling that resolves to a code, in either language.
 *
 * Arabic entries matter because nothing stopped a clinic from typing Arabic into
 * the free-text field before this existed. Those rows have to keep working, and
 * they have to display in English when the UI is English — which only happens if
 * the Arabic text resolves to a code rather than being passed through verbatim.
 *
 * Compared after {@link normalise}, so spacing, casing, the definite article's
 * hamza forms and Arabic diacritics do not need their own entries.
 */
const ALIASES: Record<SpecialtyCode, readonly string[]> = {
  Cardiology: ['cardiology', 'cardiac', 'heart', 'القلب', 'قلب', 'أمراض القلب', 'طب القلب'],
  Dentistry: ['dentistry', 'dental', 'dentist', 'طب الأسنان', 'الأسنان', 'أسنان'],
  Dermatology: ['dermatology', 'skin', 'الجلدية', 'الأمراض الجلدية', 'جلدية'],
  ENT: [
    'ent',
    'otolaryngology',
    'earnoseandthroat',
    'earnosethroat',
    'الأنف والأذن والحنجرة',
    'أنف وأذن وحنجرة',
  ],
  FamilyMedicine: ['familymedicine', 'generalpractice', 'gp', 'طب الأسرة', 'الطب العام'],
  GeneralSurgery: ['generalsurgery', 'surgery', 'الجراحة العامة', 'الجراحة', 'جراحة عامة'],
  InternalMedicine: [
    'internalmedicine',
    'internal',
    'الأمراض الباطنة',
    'الباطنة',
    'الباطنية',
    'طب الباطنة',
  ],
  Neurology: ['neurology', 'neuro', 'المخ والأعصاب', 'الأعصاب', 'طب الأعصاب'],
  ObstetricsGynecology: [
    'obstetricsandgynecology',
    'obstetricsgynecology',
    'obgyn',
    'obygn',
    'gynecology',
    'obstetrics',
    'النساء والتوليد',
    'أمراض النساء والتوليد',
    'نساء وتوليد',
  ],
  Orthopedics: ['orthopedics', 'orthopaedics', 'ortho', 'العظام', 'جراحة العظام', 'طب العظام'],
  Pediatrics: ['pediatrics', 'paediatrics', 'pediatric', 'طب الأطفال', 'الأطفال', 'أطفال'],
  Psychiatry: ['psychiatry', 'mentalhealth', 'الطب النفسي', 'النفسية', 'الصحة النفسية'],
  Radiology: ['radiology', 'imaging', 'الأشعة', 'التصوير', 'طب الأشعة'],
};

/**
 * Case, spacing and Arabic orthography folded away.
 *
 * Arabic is written with several interchangeable letter forms — أ إ آ for alef,
 * ى for ي at the end of a word, ة for ه — and optional diacritics. Two people
 * typing the same speciality routinely produce different bytes, so those forms
 * are collapsed here rather than enumerated as aliases.
 */
function normalise(value: string): string {
  return value
    .trim()
    .toLowerCase()
    .replace(/[ً-ٰٟـ]/g, '') // harakat + tatweel
    .replace(/[آأإٱ]/g, 'ا') // آ أ إ ٱ -> ا
    .replace(/ى/g, 'ي') // ى -> ي
    .replace(/ة/g, 'ه') // ة -> ه
    .replace(/[^\p{L}\p{N}]+/gu, ''); // spaces, dashes, punctuation
}

/** Built once: normalised spelling -> code. */
const BY_ALIAS = new Map<string, SpecialtyCode>(
  SPECIALTY_CODES.flatMap((code) => [
    [normalise(SPECIALTY_STORED_VALUE[code]), code] as const,
    ...ALIASES[code].map((alias) => [normalise(alias), code] as const),
  ])
);

/** The code a stored speciality string resolves to, or null when unrecognised. */
export function specialtyCode(raw: string | null | undefined): SpecialtyCode | null {
  if (!raw) {
    return null;
  }
  return BY_ALIAS.get(normalise(raw)) ?? null;
}

/**
 * The translation key for a stored speciality string, or null when it is a value
 * we have no wording for.
 *
 * Null rather than a made-up key: `{{ 'specialty.dermatology' | translate }}`
 * printing its own key on screen is worse than printing what the clinic typed.
 */
export function specialtyKey(raw: string | null | undefined): string | null {
  const code = specialtyCode(raw);
  return code ? SPECIALTY_TRANSLATION_KEY[code] : null;
}

/** The stored value for a code — what a picker writes back to the API. */
export function specialtyStoredValue(code: SpecialtyCode): string {
  return SPECIALTY_STORED_VALUE[code];
}

/** The built-in list, in stored form, for pickers and seed data. */
export const SPECIALTY_STORED_VALUES: readonly string[] = SPECIALTY_CODES.map(
  (code) => SPECIALTY_STORED_VALUE[code]
);
