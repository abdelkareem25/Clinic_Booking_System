import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatSelectModule } from '@angular/material/select';
import { of, switchMap } from 'rxjs';

import { DEFAULT_PAGE_SIZE } from '../../../core/models/pagination.model';
import { DoctorSchedule, WEEK_DAYS, WeekDay } from '../../../core/models/schedule.model';
import { DoctorsService } from '../../../core/services/doctors.service';
import { NotificationService } from '../../../core/services/notification.service';
import { SchedulesService } from '../../../core/services/schedules.service';
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
import { SelectOption } from '../../../shared/components/search-filter-bar/search-filter-bar.component';
import {
  ScheduleFormDialogComponent,
  ScheduleFormDialogData
} from '../dialogs/schedule-form-dialog.component';

@Component({
  selector: 'app-schedule-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatPaginatorModule,
    MatSelectModule,
    PageHeaderComponent,
    DataTableComponent
  ],
  templateUrl: './schedule-list.component.html',
  styleUrl: './schedule-list.component.scss'
})
export class ScheduleListComponent {
  private readonly schedulesService = inject(SchedulesService);
  private readonly doctorsService = inject(DoctorsService);
  private readonly notifications = inject(NotificationService);
  private readonly dialog = inject(MatDialog);
  private readonly fb = inject(FormBuilder);

  readonly schedules = signal<DoctorSchedule[]>([]);
  readonly total = signal(0);
  readonly loading = signal(false);
  readonly doctorOptions = signal<SelectOption[]>([]);

  readonly weekDays = WEEK_DAYS;

  pageIndex = 0;
  pageSize = DEFAULT_PAGE_SIZE;
  private sort = '';

  readonly filterForm = this.fb.nonNullable.group({
    doctorId: this.fb.control<number | null>(null),
    weekDay: this.fb.control<WeekDay | null>(null)
  });

  readonly columns: TableColumn<DoctorSchedule>[] = [
    { key: 'id', header: '#', value: (row) => row.id },
    { key: 'doctorName', header: 'Doctor', value: (row) => row.doctorName, variant: 'strong' },
    {
      key: 'weekDay',
      header: 'Week day',
      sortKey: 'weekDay',
      value: (row) => this.weekDayLabel(row.weekDay),
      variant: 'chip',
      chip: (row) => ({ label: this.weekDayLabel(row.weekDay), tone: 'info' })
    },
    { key: 'startTime', header: 'Start', align: 'center', value: (row) => this.trimTime(row.startTime) },
    { key: 'endTime', header: 'End', align: 'center', value: (row) => this.trimTime(row.endTime) }
  ];

  readonly actions: TableRowAction<DoctorSchedule>[] = [
    { id: 'edit', icon: 'edit', tooltip: 'Edit schedule', color: 'accent' },
    { id: 'delete', icon: 'delete', tooltip: 'Delete schedule', color: 'warn' }
  ];

  constructor() {
    this.loadDoctorOptions();
    this.filterForm.valueChanges.pipe(takeUntilDestroyed()).subscribe(() => {
      this.pageIndex = 0;
      this.load();
    });
    this.load();
  }

  onSortChanged(sort: SortState): void {
    this.sort = sort.direction ? (sort.direction === 'asc' ? 'DayAsc' : 'DayDesc') : '';
    this.load();
  }

  onPage(event: PageEvent): void {
    this.pageIndex = event.pageIndex;
    this.pageSize = event.pageSize;
    this.load();
  }

  resetFilters(): void {
    this.filterForm.reset({ doctorId: null, weekDay: null });
  }

  openCreate(): void {
    this.openForm();
  }

  onRowAction(event: RowActionEvent<DoctorSchedule>): void {
    if (event.action === 'edit') {
      this.openForm(event.row);
    } else if (event.action === 'delete') {
      this.confirmDelete(event.row);
    }
  }

  private openForm(schedule?: DoctorSchedule): void {
    const data: ScheduleFormDialogData = { schedule };
    this.dialog
      .open(ScheduleFormDialogComponent, { width: '620px', data })
      .afterClosed()
      .subscribe((saved) => {
        if (saved) {
          this.load();
        }
      });
  }

  private confirmDelete(schedule: DoctorSchedule): void {
    const data: ConfirmDialogData = {
      title: 'Delete schedule',
      message: `Delete ${schedule.doctorName}'s ${this.weekDayLabel(schedule.weekDay)} schedule?`,
      confirmText: 'Delete',
      icon: 'delete'
    };

    this.dialog
      .open(ConfirmDialogComponent, { width: '440px', data })
      .afterClosed()
      .pipe(switchMap((confirmed) => (confirmed ? this.schedulesService.deleteSchedule(schedule.id) : of(null))))
      .subscribe((result) => {
        if (result !== null) {
          this.notifications.success('Schedule deleted.');
          if (this.schedules().length === 1 && this.pageIndex > 0) {
            this.pageIndex -= 1;
          }
          this.load();
        }
      });
  }

  private loadDoctorOptions(): void {
    this.doctorsService.getDoctors({ pageIndex: 1, pageSize: 20, sort: 'nameAsc' }).subscribe({
      next: (page) =>
        this.doctorOptions.set(page.data.map((doctor) => ({ label: doctor.name, value: doctor.id }))),
      error: () => this.doctorOptions.set([])
    });
  }

  private load(): void {
    this.loading.set(true);
    const { doctorId, weekDay } = this.filterForm.getRawValue();
    this.schedulesService
      .getSchedules({
        pageIndex: this.pageIndex + 1,
        pageSize: this.pageSize,
        doctorId: doctorId ?? undefined,
        weekDay: weekDay ?? undefined,
        sort: this.sort
      })
      .subscribe({
        next: (page) => {
          this.schedules.set(page.data);
          this.total.set(page.count);
          this.loading.set(false);
        },
        error: () => {
          this.schedules.set([]);
          this.total.set(0);
          this.loading.set(false);
        }
      });
  }

  private weekDayLabel(value: WeekDay | number): string {
    return WEEK_DAYS.find((day) => day.value === Number(value))?.label ?? String(value);
  }

  private trimTime(value: string): string {
    return value ? value.slice(0, 5) : '—';
  }
}
