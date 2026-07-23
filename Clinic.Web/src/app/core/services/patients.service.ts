import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { CreatePatientRequest, Patient, PatientQuery, UpdatePatientRequest } from '../models/patient.model';
import { Pagination } from '../models/pagination.model';
import { ApiService } from './api.service';

@Injectable({ providedIn: 'root' })
export class PatientsService {
  private readonly api = inject(ApiService);

  getPatients(query: PatientQuery = {}): Observable<Pagination<Patient>> {
    return this.api.get<Pagination<Patient>>('Patients', {
      PageIndex: query.pageIndex,
      PageSize: query.pageSize,
      Search: query.search,
      Sort: query.sort,
      Age: query.age
    });
  }

  getPatient(id: number): Observable<Patient> {
    return this.api.get<Patient>(`Patients/${id}`);
  }

  createPatient(payload: CreatePatientRequest): Observable<Patient> {
    return this.api.post<Patient>('Patients', payload);
  }

  updatePatient(id: number, payload: UpdatePatientRequest): Observable<void> {
    return this.api.put<void>(`Patients/${id}`, payload);
  }

  deletePatient(id: number): Observable<void> {
    return this.api.delete<void>(`Patients/${id}`);
  }
}

