import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { Router } from '@angular/router';
import { of, switchMap } from 'rxjs';

import { DEFAULT_PAGE_SIZE } from '../../../core/models/pagination.model';
import { Patient, calculateAge } from '../../../core/models/patient.model';
import { NotificationService } from '../../../core/services/notification.service';
import { PatientsService } from '../../../core/services/patients.service';
import {
  ConfirmDialogComponent,
  ConfirmDialogData
} from '../../../shared/components/confirm-dialog/confirm-dialog.component';
import { DataTableComponent } from '../../../shared/components/data-table/data-table.component';
import {
  RowActionEvent,
  SortState,
  TableColumn,
  TableRowAction
} from '../../../shared/components/data-table/data-table.model';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import {
  SearchFilterBarComponent,
  SearchFilterValue
} from '../../../shared/components/search-filter-bar/search-filter-bar.component';
import {
  PatientFormDialogComponent,
  PatientFormDialogData
} from '../dialogs/patient-form-dialog.component';

@Component({
  selector: 'app-patient-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatPaginatorModule, PageHeaderComponent, SearchFilterBarComponent, DataTableComponent],
  templateUrl: './patient-list.component.html',
  styleUrl: './patient-list.component.scss'
})
export class PatientListComponent {
  private readonly patientsService = inject(PatientsService);
  private readonly notifications = inject(NotificationService);
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);

  readonly patients = signal<Patient[]>([]);
  readonly total = signal(0);
  readonly loading = signal(false);

  pageIndex = 0;
  pageSize = DEFAULT_PAGE_SIZE;
  private filters: SearchFilterValue = { search: '', filter: null, sort: '' };
  private sort = '';

  readonly columns: TableColumn<Patient>[] = [
    { key: 'id', header: '#', value: (row) => row.id },
    { key: 'name', header: 'Name', value: (row) => row.name, variant: 'strong', sortKey: 'name' },
    { key: 'phone', header: 'Phone', value: (row) => row.phone },
    {
      key: 'dateOfBirth',
      header: 'Date of birth',
      value: (row) => new Date(row.dateOfBirth).toLocaleDateString()
    },
    { key: 'age', header: 'Age', align: 'center', value: (row) => calculateAge(row.dateOfBirth) ?? '—' },
    {
      key: 'gender',
      header: 'Gender',
      align: 'center',
      value: (row) => row.gender,
      variant: 'chip',
      chip: (row) => ({ label: String(row.gender), tone: 'neutral' })
    }
  ];

  readonly actions: TableRowAction<Patient>[] = [
    { id: 'view', icon: 'visibility', tooltip: 'View details', color: 'primary' },
    { id: 'edit', icon: 'edit', tooltip: 'Edit patient', color: 'accent' },
    { id: 'delete', icon: 'delete', tooltip: 'Delete patient', color: 'warn' }
  ];

  constructor() {
    this.load();
  }

  onFiltersChanged(filters: SearchFilterValue): void {
    this.filters = filters;
    this.pageIndex = 0;
    this.load();
  }

  onSortChanged(sort: SortState): void {
    this.sort = sort.direction ? (sort.direction === 'asc' ? 'Asc' : 'Desc') : '';
    this.load();
  }

  onPage(event: PageEvent): void {
    this.pageIndex = event.pageIndex;
    this.pageSize = event.pageSize;
    this.load();
  }

  openCreate(): void {
    this.openForm();
  }

  onRowAction(event: RowActionEvent<Patient>): void {
    switch (event.action) {
      case 'view':
        void this.router.navigate(['/patients', event.row.id]);
        break;
      case 'edit':
        this.openForm(event.row);
        break;
      case 'delete':
        this.confirmDelete(event.row);
        break;
    }
  }

  private openForm(patient?: Patient): void {
    const data: PatientFormDialogData = { patient };
    this.dialog
      .open(PatientFormDialogComponent, { width: '560px', data })
      .afterClosed()
      .subscribe((saved) => {
        if (saved) {
          this.load();
        }
      });
  }

  private confirmDelete(patient: Patient): void {
    const data: ConfirmDialogData = {
      title: 'Delete patient',
      message: `Delete ${patient.name}? This cannot be undone.`,
      confirmText: 'Delete',
      icon: 'delete'
    };

    this.dialog
      .open(ConfirmDialogComponent, { width: '420px', data })
      .afterClosed()
      .pipe(switchMap((confirmed) => (confirmed ? this.patientsService.deletePatient(patient.id) : of(null))))
      .subscribe((result) => {
        if (result !== null) {
          this.notifications.success('Patient deleted.');
          if (this.patients().length === 1 && this.pageIndex > 0) {
            this.pageIndex -= 1;
          }
          this.load();
        }
      });
  }

  private load(): void {
    this.loading.set(true);
    this.patientsService
      .getPatients({
        pageIndex: this.pageIndex + 1,
        pageSize: this.pageSize,
        search: this.filters.search,
        sort: this.sort
      })
      .subscribe({
        next: (page) => {
          this.patients.set(page.data);
          this.total.set(page.count);
          this.loading.set(false);
        },
        error: () => {
          this.patients.set([]);
          this.total.set(0);
          this.loading.set(false);
        }
      });
  }
}
