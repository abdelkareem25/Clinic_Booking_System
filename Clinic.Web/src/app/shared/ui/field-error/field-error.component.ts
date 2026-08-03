import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { AbstractControl } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';

interface ResolvedError {
  key: string;
  params: Record<string, unknown>;
}

/**
 * Order matters: the first matching error is the one shown, and it should be
 * the most actionable. "Required" always beats a format complaint about an
 * empty field.
 */
const ERROR_PRIORITY: readonly string[] = [
  'required',
  'email',
  'phone',
  'nationalId',
  'passwordWeak',
  'passwordMismatch',
  'minlength',
  'maxlength',
  'min',
  'max',
  'dateInvalid',
  'dateFuture',
  'datePast',
  'timeRange',
  'outsideWorkingDays',
  'outsideWorkingHours',
  'slotTaken',
  'overlap',
  'positiveAmount',
  'pattern',
];

/**
 * Renders one validation message for a control.
 *
 * Every form in the app uses it, so error wording, timing (only after the field
 * is touched or the form submitted) and translation all behave the same. Angular
 * error keys are mapped to `validation.*` keys with their parameters, which is
 * what lets "Must be at least 3 characters" translate correctly into Arabic.
 */
@Component({
  selector: 'ui-field-error',
  imports: [TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (visible() && resolved(); as error) {
      <span class="error" role="alert">{{ error.key | translate: error.params }}</span>
    }
  `,
  styles: `
    :host {
      display: block;
    }

    .error {
      display: block;
      font-size: var(--fs-xs);
      font-weight: var(--fw-medium);
      color: var(--c-danger);
      line-height: 1.4;
    }
  `,
})
export class FieldErrorComponent {
  readonly control = input.required<AbstractControl | null>();
  /** Set when the form has been submitted, so errors show before any blur. */
  readonly submitted = input(false);

  protected readonly visible = computed(() => {
    const control = this.control();
    if (!control || control.valid) {
      return false;
    }
    return control.touched || control.dirty || this.submitted();
  });

  protected readonly resolved = computed<ResolvedError | null>(() => {
    const errors = this.control()?.errors;
    if (!errors) {
      return null;
    }

    const key = ERROR_PRIORITY.find((candidate) => candidate in errors) ?? Object.keys(errors)[0];
    if (!key) {
      return null;
    }

    return { key: `validation.${normalise(key)}`, params: paramsFor(key, errors[key]) };
  });
}

function normalise(key: string): string {
  // Angular emits `minlength` / `maxlength`; the catalogue uses camelCase.
  if (key === 'minlength') return 'minLength';
  if (key === 'maxlength') return 'maxLength';
  return key;
}

function paramsFor(key: string, detail: unknown): Record<string, unknown> {
  if (!detail || typeof detail !== 'object') {
    return {};
  }

  const value = detail as Record<string, unknown>;

  switch (key) {
    case 'minlength':
      return { min: value['requiredLength'] };
    case 'maxlength':
      return { max: value['requiredLength'] };
    case 'min':
      return { min: value['min'] };
    case 'max':
      return { max: value['max'] };
    default:
      return value;
  }
}
