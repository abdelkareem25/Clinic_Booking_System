import { ChangeDetectorRef, OnDestroy, Pipe, PipeTransform, inject } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';
import { Subscription } from 'rxjs';

import { SpecialtyService } from '../../core/i18n/specialty.service';

/**
 * Prints a stored doctor speciality in the active language.
 *
 *   {{ doctor.specialization | specialty }}
 *
 * Impure for the same reason ngx-translate's own `translate` pipe is: the output
 * depends on the catalogue, not only on the input, so a pure pipe would keep
 * showing the previous language until the doctor object itself changed. The
 * language subscription marks the host view so an OnPush component re-renders on
 * a language switch rather than on its next unrelated update.
 */
@Pipe({
  name: 'specialty',
  standalone: true,
  pure: false,
})
export class SpecialtyPipe implements PipeTransform, OnDestroy {
  private readonly specialty = inject(SpecialtyService);
  private readonly translate = inject(TranslateService);
  private readonly changeDetector = inject(ChangeDetectorRef);

  private readonly subscription: Subscription = this.translate.onLangChange.subscribe(() =>
    this.changeDetector.markForCheck()
  );

  transform(value: string | null | undefined): string {
    return this.specialty.label()(value);
  }

  ngOnDestroy(): void {
    this.subscription.unsubscribe();
  }
}
