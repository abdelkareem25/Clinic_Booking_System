import { Component, DestroyRef, EventEmitter, Input, Output, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { debounceTime } from 'rxjs';

export interface SelectOption {
  label: string;
  value: string | number;
}

export interface SearchFilterValue {
  search: string;
  filter: string | number | null;
  sort: string;
}

@Component({
  selector: 'app-search-filter-bar',
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule
  ],
  templateUrl: './search-filter-bar.component.html',
  styleUrl: './search-filter-bar.component.scss'
})
export class SearchFilterBarComponent {
  private readonly fb = inject(FormBuilder);
  private readonly destroyRef = inject(DestroyRef);

  @Input() searchPlaceholder = 'Search';
  @Input() filterLabel = 'Filter';
  @Input() filterOptions: SelectOption[] = [];
  @Input() sortOptions: SelectOption[] = [];
  @Output() readonly filtersChanged = new EventEmitter<SearchFilterValue>();

  readonly form = this.fb.nonNullable.group({
    search: [''],
    filter: ['' as string | number | null],
    sort: ['']
  });

  constructor() {
    this.form.valueChanges.pipe(debounceTime(250), takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      this.emitValue();
    });
  }

  reset(): void {
    this.form.reset({ search: '', filter: '', sort: '' });
    this.emitValue();
  }

  private emitValue(): void {
    const value = this.form.getRawValue();
    this.filtersChanged.emit({
      search: value.search.trim(),
      filter: value.filter || null,
      sort: value.sort
    });
  }
}

