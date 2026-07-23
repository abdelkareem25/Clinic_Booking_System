import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { Specialization, UpsertSpecializationRequest } from '../models/specialization.model';
import { ApiService } from './api.service';

@Injectable({ providedIn: 'root' })
export class SpecializationsService {
  private readonly api = inject(ApiService);

  getSpecializations(): Observable<Specialization[]> {
    return this.api.get<Specialization[]>('Specializations');
  }

  createSpecialization(payload: UpsertSpecializationRequest): Observable<Specialization> {
    return this.api.post<Specialization>('Specializations', payload);
  }

  updateSpecialization(id: number, payload: UpsertSpecializationRequest): Observable<Specialization> {
    return this.api.put<Specialization>(`Specializations/${id}`, payload);
  }

  deleteSpecialization(id: number): Observable<void> {
    return this.api.delete<void>(`Specializations/${id}`);
  }
}

