import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import {
  CreateScheduleRequest,
  DoctorSchedule,
  ScheduleQuery,
  UpdateScheduleRequest
} from '../models/schedule.model';
import { Pagination } from '../models/pagination.model';
import { ApiService } from './api.service';

@Injectable({ providedIn: 'root' })
export class SchedulesService {
  private readonly api = inject(ApiService);

  getSchedules(query: ScheduleQuery = {}): Observable<Pagination<DoctorSchedule>> {
    return this.api.get<Pagination<DoctorSchedule>>('Schedule', {
      PageIndex: query.pageIndex,
      PageSize: query.pageSize,
      DoctorId: query.doctorId,
      WeekDay: query.weekDay,
      Sort: query.sort
    });
  }

  getSchedule(id: number): Observable<DoctorSchedule> {
    return this.api.get<DoctorSchedule>(`Schedule/${id}`);
  }

  createSchedule(payload: CreateScheduleRequest): Observable<DoctorSchedule> {
    return this.api.post<DoctorSchedule>('Schedule', payload);
  }

  updateSchedule(id: number, payload: UpdateScheduleRequest): Observable<void> {
    return this.api.put<void>(`Schedule/${id}`, payload);
  }

  deleteSchedule(id: number): Observable<void> {
    return this.api.delete<void>(`Schedule/${id}`);
  }
}

