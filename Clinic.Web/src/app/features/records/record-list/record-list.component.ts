import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { Router } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { catchError, forkJoin, of } from 'rxjs';

import { PermissionService } from '../../../core/authz/permission.service';
import {
  MedicalRecord,
  MedicalRecordsStore,
  RECORD_TYPES,
  RECORD_TYPE_META,
  RecordType,
} from '../../../core/data/medical-records.store';
import { NotificationService } from '../../../core/services/notification.service';
import { DoctorsService } from '../../../core/services/doctors.service';
import { PatientsService } from '../../../core/services/patients.service';
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
import {
  RecordDialogData,
  RecordFormDialogComponent,
} from '../record-form-dialog/record-form-dialog.component';

@Component({
  selector: 'app-record-list',
  imports: [
    MatButtonModule,
    MatDatepickerModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    TranslatePipe,
    CellTemplateDirective,
    DataTableComponent,
    IconComponent,
    PageHeaderComponent,
    SearchFieldComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './record-list.component.html',
  styleUrl: './record-list.component.scss',
})
export class RecordListComponent {
  private readonly dialog = inject(MatDialog);
  private readonly doctorsApi = inject(DoctorsService);
  private readonly notifications = inject(NotificationService);
  private readonly patientsApi = inject(PatientsService);
  private readonly router = inject(Router);
  private readonly store = inject(MedicalRecordsStore);
  private readonly translate = inject(TranslateService);

  protected readonly permissions = inject(PermissionService);
  protected readonly recordTypes = RECORD_TYPES;
  protected readonly typeMeta = RECORD_TYPE_META;
  protected readonly maxDate = new Date();

  protected readonly search = signal<SearchEvent | null>(null);
  protected readonly type = signal<RecordType | 'all'>('all');
  protected readonly from = signal<Date | null>(null);
  protected readonly to = signal<Date | null>(null);
  protected readonly page = signal<PageState>({ pageIndex: 1, pageSize: 25 });
  protected readonly sort = signal<SortState>({ key: 'occurredAt', direction: 'desc' });

  /** Guards the add button while the patient and doctor lists are being fetched. */
  protected readonly opening = signal(false);

  protected readonly hasFilters = computed(
    () => Boolean(this.search()?.term) || this.type() !== 'all' || Boolean(this.from() || this.to())
  );

  /**
   * Records live in a local store, so filtering, sorting and paging all happen
   * here rather than over the wire. The table's contract is identical either
   * way, which is what lets this move to an endpoint without touching markup.
   */
  private readonly filtered = computed(() => {
    const term = this.search()?.term.toLowerCase() ?? '';
    const type = this.type();

    let rows = this.store.records();

    if (type !== 'all') {
      rows = rows.filter((record) => record.type === type);
    }

    // Inclusive on both ends: a range of 1 Jan to 1 Jan must return that day's
    // records, so the bounds are widened to the whole day rather than compared
    // against whatever time the picker happened to carry.
    const from = this.startOfDay(this.from());
    const to = this.endOfDay(this.to());

    if (from !== null || to !== null) {
      rows = rows.filter((record) => {
        const occurred = new Date(record.occurredAt).getTime();
        if (Number.isNaN(occurred)) {
          return false;
        }
        return (from === null || occurred >= from) && (to === null || occurred <= to);
      });
    }

    if (term) {
      rows = rows.filter(
        (record) =>
          record.patientName.toLowerCase().includes(term) ||
          record.title.toLowerCase().includes(term) ||
          (record.diagnosis ?? '').toLowerCase().includes(term)
      );
    }

    const { key, direction } = this.sort();
    const factor = direction === 'desc' ? -1 : 1;
    return [...rows].sort(
      (a, b) => String(a[key as keyof MedicalRecord] ?? '')
        .localeCompare(String(b[key as keyof MedicalRecord] ?? '')) * factor
    );
  });

  protected readonly total = computed(() => this.filtered().length);

  protected readonly rows = computed(() => {
    const { pageIndex, pageSize } = this.page();
    const start = (pageIndex - 1) * pageSize;
    return this.filtered().slice(start, start + pageSize);
  });

  protected readonly columns: TableColumn<MedicalRecord>[] = [
    {
      key: 'occurredAt',
      header: 'common.date',
      sortKey: 'occurredAt',
      value: (row) => new Date(row.occurredAt).toLocaleDateString(),
      secondary: (row) =>
        new Date(row.occurredAt).toLocaleTimeString(undefined, {
          hour: 'numeric',
          minute: '2-digit',
          hour12: true,
        }),
      width: '150px',
    },
    { key: 'type', header: 'records.type', variant: 'badge', width: '140px', badge: (row) => ({
      label: RECORD_TYPE_META[row.type].label,
      tone: RECORD_TYPE_META[row.type].tone,
    }) },
    {
      key: 'patientName',
      header: 'appointments.patient',
      sortKey: 'patientName',
      value: (row) => row.patientName,
      variant: 'strong',
    },
    { key: 'title', header: 'common.details', value: (row) => row.title, hideBelow: 'md' },
    {
      key: 'doctorName',
      header: 'appointments.doctor',
      value: (row) => row.doctorName ?? row.recordedBy,
      variant: 'muted',
      hideBelow: 'lg',
    },
  ];

  protected readonly rowActions: TableRowAction<MedicalRecord>[] = [
    { id: 'patient', icon: 'user', label: 'patients.one' },
    { id: 'delete', icon: 'delete', label: 'common.delete', tone: 'danger' },
  ];

  protected onSearch(event: SearchEvent): void {
    this.search.set(event.term ? event : null);
    this.page.update((page) => ({ ...page, pageIndex: 1 }));
  }

  protected onType(value: RecordType | 'all'): void {
    this.type.set(value);
    this.page.update((page) => ({ ...page, pageIndex: 1 }));
  }

  protected onFrom(value: Date | null): void {
    this.from.set(value);
    this.page.update((page) => ({ ...page, pageIndex: 1 }));
  }

  protected onTo(value: Date | null): void {
    this.to.set(value);
    this.page.update((page) => ({ ...page, pageIndex: 1 }));
  }

  protected onSort(sort: SortState): void {
    this.sort.set(sort);
  }

  protected onPage(page: PageState): void {
    this.page.set(page);
  }

  protected clearFilters(): void {
    this.search.set(null);
    this.type.set('all');
    this.from.set(null);
    this.to.set(null);
    this.page.update((page) => ({ ...page, pageIndex: 1 }));
  }

  /**
   * Unlike the patient page, nothing here says which patient the entry belongs
   * to, so both lists are fetched and the dialog asks.
   */
  protected addRecord(): void {
    if (this.opening()) {
      return;
    }

    this.opening.set(true);

    forkJoin({
      patients: this.patientsApi
        .getPatients({ pageIndex: 1, pageSize: 200 })
        .pipe(catchError(() => of({ pageIndex: 1, pageSize: 0, count: 0, data: [] }))),
      doctors: this.doctorsApi
        .getDoctors({ pageIndex: 1, pageSize: 200 })
        .pipe(catchError(() => of({ pageIndex: 1, pageSize: 0, count: 0, data: [] }))),
    }).subscribe({
      next: ({ patients, doctors }) => {
        this.opening.set(false);

        // A record without a patient is not a record, so say so rather than
        // opening a dialog whose only required field cannot be filled.
        if (!patients.data.length) {
          this.notifications.error?.(this.translate.instant('patients.empty'));
          return;
        }

        this.dialog.open<RecordFormDialogComponent, RecordDialogData, boolean>(
          RecordFormDialogComponent,
          {
            data: {
              patients: patients.data,
              doctors: doctors.data,
            },
          }
        );
      },
      error: () => this.opening.set(false),
    });
  }

  protected openPatient(row: MedicalRecord): void {
    void this.router.navigate(['/patients', row.patientId]);
  }

  protected onRowAction(event: RowActionEvent<MedicalRecord>): void {
    if (event.action === 'patient') {
      this.openPatient(event.row);
      return;
    }

    confirmDialog(this.dialog, {
      title: 'records.delete',
      message: 'records.deleteConfirm',
      messageParams: { date: new Date(event.row.occurredAt).toLocaleDateString() },
      confirmLabel: 'common.delete',
      tone: 'danger',
    }).subscribe((confirmed) => {
      if (confirmed) {
        this.store.remove(event.row.id);
        this.notifications.success(this.translate.instant('records.deleted'));
      }
    });
  }

  private startOfDay(value: Date | null): number | null {
    if (!value) {
      return null;
    }
    const date = new Date(value);
    date.setHours(0, 0, 0, 0);
    return date.getTime();
  }

  private endOfDay(value: Date | null): number | null {
    if (!value) {
      return null;
    }
    const date = new Date(value);
    date.setHours(23, 59, 59, 999);
    return date.getTime();
  }
}