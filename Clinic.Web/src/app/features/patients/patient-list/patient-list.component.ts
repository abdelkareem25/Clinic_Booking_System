import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { Router, RouterLink } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';

import { PermissionService } from '../../../core/authz/permission.service';
import { NotificationService } from '../../../core/services/notification.service';
import { confirmDialog } from '../../../shared/ui/confirm-dialog/confirm-dialog.component';
import { DataTableComponent } from '../../../shared/ui/data-table/data-table.component';
import {
  CellTemplateDirective,
  PageState,
  RowActionEvent,
  SortState,
  TableColumn,
  TableRowAction,
} from '../../../shared/ui/data-table/data-table.model';
import { IconComponent } from '../../../shared/ui/icon/icon.component';
import { PageHeaderComponent } from '../../../shared/ui/page-header/page-header.component';
import {
  SearchEvent,
  SearchFieldComponent,
} from '../../../shared/ui/search-field/search-field.component';
import { PatientView, PatientsFacade } from '../patients.facade';

type GenderFilter = 'all' | 'Male' | 'Female' | 'Other';

@Component({
  selector: 'app-patient-list',
  imports: [
    RouterLink,
    MatButtonModule,
    MatFormFieldModule,
    MatSelectModule,
    TranslatePipe,
    CellTemplateDirective,
    DataTableComponent,
    IconComponent,
    PageHeaderComponent,
    SearchFieldComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './patient-list.component.html',
  styleUrl: './patient-list.component.scss',
})
export class PatientListComponent {
  private readonly dialog = inject(MatDialog);
  private readonly facade = inject(PatientsFacade);
  private readonly notifications = inject(NotificationService);
  private readonly router = inject(Router);
  private readonly translate = inject(TranslateService);

  protected readonly permissions = inject(PermissionService);

  protected readonly rows = signal<PatientView[]>([]);
  protected readonly total = signal(0);
  protected readonly loading = signal(true);
  protected readonly search = signal<SearchEvent | null>(null);
  protected readonly gender = signal<GenderFilter>('all');
  protected readonly page = signal<PageState>({ pageIndex: 1, pageSize: 10 });
  protected readonly sort = signal<SortState>({ key: 'name', direction: 'asc' });

  /** Gender is filtered locally — the endpoint has no parameter for it. */
  protected readonly visibleRows = computed(() => {
    const gender = this.gender();
    return gender === 'all' ? this.rows() : this.rows().filter((row) => row.gender === gender);
  });

  protected readonly hasFilters = computed(
    () => Boolean(this.search()?.term) || this.gender() !== 'all'
  );

  protected readonly columns: TableColumn<PatientView>[] = [
    {
      key: 'fileNumber',
      header: 'patients.fileNumber',
      value: (row) => row.profile.fileNumber,
      variant: 'mono',
      width: '120px',
      hideBelow: 'md',
    },
    { key: 'name', header: 'patients.name', sortKey: 'name', value: (row) => row.name },
    {
      key: 'age',
      header: 'patients.age',
      value: (row) => (row.age === null ? '—' : row.age),
      align: 'center',
      width: '80px',
      hideBelow: 'sm',
    },
    {
      key: 'gender',
      header: 'patients.gender',
      variant: 'badge',
      width: '110px',
      hideBelow: 'md',
      badge: (row) => ({
        label:
          row.gender === 'Male'
            ? 'patients.genderMale'
            : row.gender === 'Female'
              ? 'patients.genderFemale'
              : 'patients.genderOther',
        tone: 'neutral',
      }),
    },
    { key: 'phone', header: 'patients.phone', value: (row) => row.phone, hideBelow: 'sm' },
    {
      key: 'medical',
      header: 'patients.sectionMedical',
      value: () => '',
      width: '170px',
      hideBelow: 'lg',
    },
  ];

  protected readonly rowActions: TableRowAction<PatientView>[] = [
    { id: 'view', icon: 'show', label: 'common.view' },
    { id: 'edit', icon: 'edit', label: 'common.edit' },
    { id: 'delete', icon: 'delete', label: 'common.delete', tone: 'danger' },
  ];

  constructor() {
    this.load();
  }

  protected load(): void {
    this.loading.set(true);

    this.facade
      .list({
        ...this.page(),
        sort: this.sort().direction === 'desc' ? 'Desc' : 'Asc',
        search: this.search(),
      })
      .subscribe({
        next: (result) => {
          this.rows.set(result.data);
          this.total.set(result.count);
          this.loading.set(false);
        },
        error: () => {
          this.rows.set([]);
          this.total.set(0);
          this.loading.set(false);
        },
      });
  }

  protected onSearch(event: SearchEvent): void {
    this.search.set(event.term ? event : null);
    this.page.update((page) => ({ ...page, pageIndex: 1 }));
    this.load();
  }

  protected onGender(value: GenderFilter): void {
    this.gender.set(value);
  }

  protected onSort(sort: SortState): void {
    this.sort.set(sort);
    this.load();
  }

  protected onPage(page: PageState): void {
    this.page.set(page);
    this.load();
  }

  protected clearFilters(): void {
    this.search.set(null);
    this.gender.set('all');
    this.page.update((page) => ({ ...page, pageIndex: 1 }));
    this.load();
  }

  protected onRowAction(event: RowActionEvent<PatientView>): void {
    switch (event.action) {
      case 'view':
        void this.router.navigate(['/patients', event.row.id]);
        break;
      case 'edit':
        void this.router.navigate(['/patients', event.row.id, 'edit']);
        break;
      case 'delete':
        this.confirmDelete(event.row);
        break;
    }
  }

  protected openPatient(row: PatientView): void {
    void this.router.navigate(['/patients', row.id]);
  }

  /** Compact summary of the clinical flags that change how a patient is handled. */
  protected medicalFlags(row: PatientView): { label: string; tone: string }[] {
    const flags: { label: string; tone: string }[] = [];

    if (row.profile.allergies.length) {
      flags.push({ label: `${row.profile.allergies.length}`, tone: 'danger' });
    }
    if (row.profile.chronicDiseases.length) {
      flags.push({ label: `${row.profile.chronicDiseases.length}`, tone: 'warning' });
    }
    if (row.profile.bloodGroup) {
      flags.push({ label: row.profile.bloodGroup, tone: 'info' });
    }

    return flags;
  }

  private confirmDelete(row: PatientView): void {
    confirmDialog(this.dialog, {
      title: 'patients.delete',
      message: 'patients.deleteConfirm',
      messageParams: { name: row.name },
      confirmLabel: 'common.delete',
      tone: 'danger',
    }).subscribe((confirmed) => {
      if (!confirmed) {
        return;
      }

      this.facade.remove(row.id).subscribe({
        next: () => {
          this.notifications.success(this.translate.instant('patients.deleted'));
          this.load();
        },
      });
    });
  }
}
