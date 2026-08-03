import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  effect,
  inject,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatTooltipModule } from '@angular/material/tooltip';
import { TranslatePipe } from '@ngx-translate/core';
import { debounceTime, distinctUntilChanged, map } from 'rxjs';

import { isCompletePhone, normalisePhone } from '../../../core/utils/validators';
import { IconComponent } from '../icon/icon.component';

export type SearchKind = 'text' | 'phone' | 'mrn';

export interface SearchEvent {
  /** Normalised term: digits only for a phone, trimmed text otherwise. */
  term: string;
  kind: SearchKind;
}

/** File numbers are issued as `2026-00001`; the year prefix is optional to type. */
const MRN_PATTERN = /^(?:\d{4}-)?\d{4,6}$/;

const DEBOUNCE_MS = 320;

/**
 * The search input used by every list screen.
 *
 * It recognises what is being typed and reacts accordingly:
 *
 *   • a complete phone number fires **immediately**, with no debounce and no
 *     Search button — the front desk types 11 digits from a caller and the
 *     patient is on screen before they finish saying their name;
 *   • a file number is recognised by shape and searched as an exact match;
 *   • anything else is treated as a name and debounced normally.
 *
 * The detected kind travels with the event so the caller can query the right
 * field instead of running one fuzzy match across everything.
 */
@Component({
  selector: 'ui-search-field',
  imports: [ReactiveFormsModule, MatButtonModule, MatTooltipModule, TranslatePipe, IconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="search" [class.search-focused]="focused()">
      <ui-icon name="search" size="md" class="lead" />

      <input
        #input
        type="search"
        class="input"
        autocomplete="off"
        spellcheck="false"
        enterkeyhint="search"
        [formControl]="control"
        [placeholder]="placeholder() | translate"
        [attr.aria-label]="placeholder() | translate"
        (focus)="focused.set(true)"
        (blur)="focused.set(false)"
        (keydown.enter)="emitNow()"
        (keydown.escape)="clear()"
      />

      @if (detected(); as kind) {
        <span class="kind badge badge-info">{{ kindLabel(kind) | translate }}</span>
      }

      @if (control.value) {
        <button
          mat-icon-button
          type="button"
          class="clear"
          [matTooltip]="'common.clear' | translate"
          [attr.aria-label]="'common.clear' | translate"
          (click)="clear()"
        >
          <ui-icon name="close" size="sm" />
        </button>
      }
    </div>

    @if (hint(); as text) {
      <p class="hint">{{ text | translate }}</p>
    }
  `,
  styles: `
    :host {
      display: block;
      min-width: 0;
    }

    .search {
      display: flex;
      align-items: center;
      gap: var(--sp-2);
      height: 40px;
      padding-inline: var(--sp-3);
      background: var(--c-surface);
      border: 1px solid var(--c-border-strong);
      border-radius: var(--r-lg);
      transition:
        border-color var(--dur-micro) var(--ease-standard),
        box-shadow var(--dur-micro) var(--ease-standard);
    }

    .search:hover {
      border-color: var(--c-text-subtle);
    }

    .search-focused {
      border-color: var(--c-primary);
      box-shadow: var(--sh-focus);
    }

    .lead {
      color: var(--c-text-subtle);
      flex: 0 0 auto;
    }

    .input {
      flex: 1 1 auto;
      min-width: 0;
      border: 0;
      outline: none;
      background: none;
      font-size: var(--fs-base);
      color: var(--c-text);
    }

    .input::placeholder {
      color: var(--c-text-subtle);
    }

    /* The browser's own clear affordance duplicates ours. */
    .input::-webkit-search-cancel-button {
      display: none;
    }

    .kind {
      flex: 0 0 auto;
      text-transform: uppercase;
      letter-spacing: 0.04em;
      font-size: var(--fs-2xs);
    }

    .clear {
      flex: 0 0 auto;
      color: var(--c-text-subtle);
    }

    .hint {
      margin-block-start: var(--sp-1);
      font-size: var(--fs-xs);
      color: var(--c-text-subtle);
    }
  `,
})
export class SearchFieldComponent {
  private readonly destroyRef = inject(DestroyRef);

  readonly placeholder = input('common.search');
  readonly hint = input<string | null>(null);
  /** Two-way seed, e.g. restoring a term from the URL. */
  readonly value = input('');

  readonly search = output<SearchEvent>();

  protected readonly control = new FormControl('', { nonNullable: true });
  protected readonly focused = signal(false);
  protected readonly detected = signal<SearchKind | null>(null);

  private readonly inputEl = viewChild<ElementRef<HTMLInputElement>>('input');
  private lastEmitted = '';

  constructor() {
    effect(() => {
      const seed = this.value();
      if (seed !== this.control.value) {
        this.control.setValue(seed, { emitEvent: false });
        this.lastEmitted = seed;
      }
    });

    // A complete phone number bypasses the debounce entirely; everything else
    // waits, so a name search does not fire a request per keystroke.
    this.control.valueChanges
      .pipe(
        map((raw) => this.classify(raw)),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe(({ kind }) => this.detected.set(kind === 'text' ? null : kind));

    this.control.valueChanges
      .pipe(
        debounceTime(DEBOUNCE_MS),
        map((raw) => this.classify(raw)),
        distinctUntilChanged((a, b) => a.term === b.term),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe((event) => this.emit(event));

    this.control.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((raw) => {
      const event = this.classify(raw);
      if (event.kind === 'phone') {
        this.emit(event);
      }
    });
  }

  clear(): void {
    this.control.setValue('');
    this.detected.set(null);
    this.inputEl()?.nativeElement.focus();
  }

  focus(): void {
    this.inputEl()?.nativeElement.focus();
  }

  protected emitNow(): void {
    this.emit(this.classify(this.control.value));
  }

  protected kindLabel(kind: SearchKind): string {
    return kind === 'phone' ? 'patients.phone' : 'patients.fileNumber';
  }

  private emit(event: SearchEvent): void {
    if (event.term === this.lastEmitted) {
      return;
    }
    this.lastEmitted = event.term;
    this.search.emit(event);
  }

  private classify(raw: string): SearchEvent {
    const trimmed = (raw ?? '').trim();

    if (!trimmed) {
      return { term: '', kind: 'text' };
    }

    if (isCompletePhone(trimmed)) {
      return { term: normalisePhone(trimmed), kind: 'phone' };
    }

    if (MRN_PATTERN.test(trimmed)) {
      return { term: trimmed, kind: 'mrn' };
    }

    return { term: trimmed, kind: 'text' };
  }
}
