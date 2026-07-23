import { HttpErrorResponse } from '@angular/common/http';
import { FormGroup } from '@angular/forms';

interface ValidationProblem {
  errors?: Record<string, string[] | string>;
  [key: string]: unknown;
}

/**
 * Maps an ASP.NET Core `ValidationProblemDetails` (or FluentValidation 400)
 * payload onto reactive form controls. Control names are matched
 * case-insensitively against the server property names.
 *
 * Returns the messages that could not be matched to a control so the caller can
 * surface them separately.
 */
export function applyServerValidationErrors(form: FormGroup, error: unknown): string[] {
  if (!(error instanceof HttpErrorResponse) || error.status !== 400) {
    return [];
  }

  const body = error.error as ValidationProblem | undefined;
  const serverErrors = body?.errors;
  if (!serverErrors || typeof serverErrors !== 'object') {
    return [];
  }

  const controlMap = new Map<string, string>();
  Object.keys(form.controls).forEach((name) => controlMap.set(name.toLowerCase(), name));

  const unmatched: string[] = [];

  Object.entries(serverErrors).forEach(([field, messages]) => {
    const text = Array.isArray(messages) ? messages.join(' ') : String(messages);
    const controlName = controlMap.get(field.toLowerCase());

    if (controlName) {
      const control = form.get(controlName);
      control?.setErrors({ ...(control.errors ?? {}), server: text });
      control?.markAsTouched();
    } else {
      unmatched.push(text);
    }
  });

  return unmatched;
}
