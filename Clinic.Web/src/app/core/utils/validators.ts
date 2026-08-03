import { AbstractControl, ValidationErrors, ValidatorFn, Validators } from '@angular/forms';

/**
 * Domain validation rules.
 *
 * Formats here are Egyptian, matching where this clinic system is deployed:
 * an 11-digit mobile beginning 010/011/012/015, and the 14-digit national ID
 * whose first 7 digits encode century and date of birth.
 */

export const PHONE_PATTERN = /^01[0125]\d{8}$/;
export const NATIONAL_ID_PATTERN = /^[23]\d{13}$/;

/** Strip formatting a user may paste in (spaces, dashes, +20 country code). */
export function normalisePhone(value: string): string {
  const digits = String(value ?? '').replace(/[\s()+-]/g, '');
  if (digits.startsWith('0020')) {
    return `0${digits.slice(4)}`;
  }
  if (digits.startsWith('20') && digits.length === 12) {
    return `0${digits.slice(2)}`;
  }
  return digits;
}

export function isCompletePhone(value: string): boolean {
  return PHONE_PATTERN.test(normalisePhone(value));
}

export const phoneValidator: ValidatorFn = (control: AbstractControl): ValidationErrors | null => {
  const raw = control.value;
  if (raw === null || raw === undefined || raw === '') {
    return null;
  }
  return isCompletePhone(String(raw)) ? null : { phone: true };
};

/**
 * National ID: 14 digits, and the embedded birth date must be a real date.
 * Length alone lets typos through that a receptionist would never catch.
 */
export const nationalIdValidator: ValidatorFn = (
  control: AbstractControl
): ValidationErrors | null => {
  const raw = String(control.value ?? '').trim();
  if (!raw) {
    return null;
  }

  if (!NATIONAL_ID_PATTERN.test(raw)) {
    return { nationalId: true };
  }

  const century = raw[0] === '2' ? 1900 : 2000;
  const year = century + Number(raw.slice(1, 3));
  const month = Number(raw.slice(3, 5));
  const day = Number(raw.slice(5, 7));

  const date = new Date(year, month - 1, day);
  const valid =
    date.getFullYear() === year && date.getMonth() === month - 1 && date.getDate() === day;

  return valid ? null : { nationalId: true };
};

/** 8+ chars with an uppercase letter, a digit and a symbol — matches Identity's policy. */
export const strongPasswordValidator: ValidatorFn = (
  control: AbstractControl
): ValidationErrors | null => {
  const value = String(control.value ?? '');
  if (!value) {
    return null;
  }

  const strong =
    value.length >= 8 &&
    /[A-Z]/.test(value) &&
    /[a-z]/.test(value) &&
    /\d/.test(value) &&
    /[^A-Za-z0-9]/.test(value);

  return strong ? null : { passwordWeak: true };
};

/** Cross-field: `confirm` must equal `password`. Attach to the group. */
export function passwordMatchValidator(
  passwordKey = 'password',
  confirmKey = 'confirmPassword'
): ValidatorFn {
  return (group: AbstractControl): ValidationErrors | null => {
    const password = group.get(passwordKey)?.value;
    const confirm = group.get(confirmKey);

    if (!confirm || !confirm.value) {
      return null;
    }

    if (password === confirm.value) {
      // Clear only our own error so other validators on the control survive.
      if (confirm.hasError('passwordMismatch')) {
        const { passwordMismatch: _removed, ...rest } = confirm.errors ?? {};
        confirm.setErrors(Object.keys(rest).length ? rest : null);
      }
      return null;
    }

    confirm.setErrors({ ...(confirm.errors ?? {}), passwordMismatch: true });
    return { passwordMismatch: true };
  };
}

/** A date that must not be in the future — dates of birth, visit dates. */
export const notFutureValidator: ValidatorFn = (
  control: AbstractControl
): ValidationErrors | null => {
  const value = control.value as Date | string | null;
  if (!value) {
    return null;
  }

  const date = value instanceof Date ? value : new Date(value);
  if (Number.isNaN(date.getTime())) {
    return { dateInvalid: true };
  }

  const endOfToday = new Date();
  endOfToday.setHours(23, 59, 59, 999);

  return date.getTime() > endOfToday.getTime() ? { dateFuture: true } : null;
};

/** A date that must not be in the past — appointment dates. */
export const notPastValidator: ValidatorFn = (
  control: AbstractControl
): ValidationErrors | null => {
  const value = control.value as Date | string | null;
  if (!value) {
    return null;
  }

  const date = value instanceof Date ? value : new Date(value);
  if (Number.isNaN(date.getTime())) {
    return { dateInvalid: true };
  }

  const startOfToday = new Date();
  startOfToday.setHours(0, 0, 0, 0);

  return date.getTime() < startOfToday.getTime() ? { datePast: true } : null;
};

/** Cross-field: `endKey` must be strictly after `startKey`. */
export function timeRangeValidator(startKey = 'startTime', endKey = 'endTime'): ValidatorFn {
  return (group: AbstractControl): ValidationErrors | null => {
    const start = group.get(startKey)?.value as Date | null;
    const end = group.get(endKey)?.value as Date | null;

    if (!start || !end) {
      return null;
    }

    const startMinutes = start.getHours() * 60 + start.getMinutes();
    const endMinutes = end.getHours() * 60 + end.getMinutes();

    return endMinutes > startMinutes ? null : { timeRange: true };
  };
}

export const positiveAmountValidator: ValidatorFn = (
  control: AbstractControl
): ValidationErrors | null => {
  const value = Number(control.value);
  if (control.value === null || control.value === '' || control.value === undefined) {
    return null;
  }
  return Number.isFinite(value) && value > 0 ? null : { positiveAmount: true };
};

/** The rule set reused by every free-text name field. */
export const nameValidators = [
  Validators.required,
  Validators.minLength(3),
  Validators.maxLength(80),
];
