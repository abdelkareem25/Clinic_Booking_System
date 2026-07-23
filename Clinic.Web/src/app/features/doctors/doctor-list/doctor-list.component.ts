import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { Router } from '@angular/router';
import { switchMap } from 'rxjs';

import { DEFAULT_PAGE_SIZE } from '../../../core/models/pagination.model';
import { DEFAULT_SPECIALIZATIONS, Doctor } from '../../../core/models/doctor.model';
import { DoctorsService } from '../../../core/services/doctors.service';
import { NotificationService } from '../../../core/services/notification.service';
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
  SearchFilterValue,
  SelectOption
} from '../../../shared/components/search-filter-bar/search-filter-bar.component';
import {
  DoctorFormDialogComponent,
  DoctorFormDialogData
} from '../dialogs/doctor-form-dialog.component';

@Component({
  selector: 'app-doctor-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    MatPaginatorModule,
    PageHeaderComponent,
    SearchFilterBarComponent,
    DataTableComponent
  ],
  templateUrl: './doctor-list.component.html',
  styleUrl: './doctor-list.component.scss'
})
export class DoctorListComponent {
  private readonly doctorsService = inject(DoctorsService);
  private readonly notifications = inject(NotificationService);
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);

  readonly doctors = signal<Doctor[]>([]);
  readonly total = signal(0);
  readonly loading = signal(false);

  pageIndex = 0;
  pageSize = DEFAULT_PAGE_SIZE;
  private filters: SearchFilterValue = { search: '', filter: null, sort: '' };
  private sort = '';

  readonly specializationOptions: SelectOption[] = DEFAULT_SPECIALIZATIONS.map((specialization) => ({
    label: specialization,
    value: specialization
  }));

  readonly columns: TableColumn<Doctor>[] = [
    { key: 'id', header: '#', value: (row) => row.id },
    { key: 'name', header: 'Name', value: (row) => row.name, variant: 'strong', sortKey: 'name' },
    {
      key: 'specialization',
      header: 'Specialization',
      value: (row) => row.specialization,
      variant: 'chip',
      chip: (row) => ({ label: row.specialization, tone: 'info' })
    }
  ];

  readonly actions: TableRowAction<Doctor>[] = [
    { id: 'view', icon: 'visibility', tooltip: 'View details', color: 'primary' },
    { id: 'edit', icon: 'edit', tooltip: 'Edit doctor', color: 'accent' },
    { id: 'delete', icon: 'delete', tooltip: 'Delete doctor', color: 'warn' }
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
    this.sort = sort.direction ? (sort.direction === 'asc' ? 'nameAsc' : 'nameDesc') : '';
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

  onRowAction(event: RowActionEvent<Doctor>): void {
    switch (event.action) {
      case 'view':
        void this.router.navigate(['/doctors', event.row.id]);
        break;
      case 'edit':
        this.openForm(event.row);
        break;
      case 'delete':
        this.confirmDelete(event.row);
        break;
    }
  }

  private openForm(doctor?: Doctor): void {
    const data: DoctorFormDialogData = { doctor };
    this.dialog
      .open(DoctorFormDialogComponent, { width: '480px', data })
      .afterClosed()
      .subscribe((saved) => {
        if (saved) {
          this.load();
        }
      });
  }

  private confirmDelete(doctor: Doctor): void {
    const data: ConfirmDialogData = {
      title: 'Delete doctor',
      message: `Delete Dr. ${doctor.name}? This cannot be undone.`,
      confirmText: 'Delete',
      icon: 'delete'
    };

    this.dialog
      .open(ConfirmDialogComponent, { width: '420px', data })
      .afterClosed()
      .pipe(switchMap((confirmed) => (confirmed ? this.doctorsService.deleteDoctor(doctor.id) : [])))
      .subscribe({
        next: () => {
          this.notifications.success('Doctor deleted.');
          this.afterDelete();
        }
      });
  }

  private afterDelete(): void {
    if (this.doctors().length === 1 && this.pageIndex > 0) {
      this.pageIndex -= 1;
    }
    this.load();
  }

  private load(): void {
    this.loading.set(true);
    this.doctorsService
      .getDoctors({
        pageIndex: this.pageIndex + 1,
        pageSize: this.pageSize,
        search: this.filters.search,
        specialty: this.filters.filter ? String(this.filters.filter) : undefined,
        sort: this.sort
      })
      .subscribe({
        next: (page) => {
          this.doctors.set(page.data);
          this.total.set(page.count);
          this.loading.set(false);
        },
        error: () => {
          this.doctors.set([]);
          this.total.set(0);
          this.loading.set(false);
        }
      });
  }
}
