import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, of, switchMap } from 'rxjs';

import { MedicalRecordsStore } from '../../core/data/medical-records.store';
import { PatientProfile, PatientProfileStore } from '../../core/data/patient-profile.store';
import { Pagination } from '../../core/models/pagination.model';
import { CreatePatientRequest, Patient, calculateAge } from '../../core/models/patient.model';
import { PatientsService } from '../../core/services/patients.service';
import { SearchEvent } from '../../shared/ui/search-field/search-field.component';

/** An API patient joined to its locally-held clinical profile. */
export interface PatientView extends Patient {
  profile: PatientProfile;
  age: number | null;
}

export interface PatientListQuery {
  pageIndex: number;
  pageSize: number;
  sort?: string;
  search?: SearchEvent | null;
}

export interface PatientListResult {
  data: PatientView[];
  count: number;
}

/** Wide enough to filter locally when the API cannot express the query. */
const CLIENT_FILTER_PAGE_SIZE = 200;

/**
 * The single entry point for patient data.
 *
 * A patient is stored in two halves — identity in the API, clinical detail in
 * the local profile store — and no component should have to know that. This
 * joins them, keeps profile creation and record cleanup in step with the API
 * calls, and is the one file that changes if the backend ever grows the extra
 * columns.
 *
 * It also resolves the three search modes. The API's `Search` matches names
 * only, so a phone number or file number is answered by pulling a wide page and
 * filtering here — correct results beat a fast query that cannot find anyone.
 */
@Injectable({ providedIn: 'root' })
export class PatientsFacade {
  private readonly api = inject(PatientsService);
  private readonly profiles = inject(PatientProfileStore);
  private readonly records = inject(MedicalRecordsStore);

  list(query: PatientListQuery): Observable<PatientListResult> {
    const search = query.search;
    const needsLocalFilter = search?.kind === 'phone' || search?.kind === 'mrn';

    return this.api
      .getPatients({
        pageIndex: needsLocalFilter ? 1 : query.pageIndex,
        pageSize: needsLocalFilter ? CLIENT_FILTER_PAGE_SIZE : query.pageSize,
        search: search?.kind === 'text' ? search.term : undefined,
        sort: query.sort,
      })
      .pipe(
        // These endpoints answer 404 rather than an empty page.
        catchError(() => of<Pagination<Patient>>({ pageIndex: 1, pageSize: 0, count: 0, data: [] })),
        map((page) => {
          const views = page.data.map((patient) => this.toView(patient));

          if (!needsLocalFilter || !search) {
            return { data: views, count: page.count };
          }

          const matches =
            search.kind === 'phone'
              ? views.filter((view) => digits(view.phone).endsWith(search.term))
              : views.filter((view) => matchesFileNumber(view.profile.fileNumber, search.term));

          // Paginate the filtered set so the pager stays honest.
          const start = (query.pageIndex - 1) * query.pageSize;
          return {
            data: matches.slice(start, start + query.pageSize),
            count: matches.length,
          };
        })
      );
  }

  get(id: number): Observable<PatientView> {
    return this.api.getPatient(id).pipe(map((patient) => this.toView(patient)));
  }

  create(
    payload: CreatePatientRequest,
    profile: Partial<PatientProfile>
  ): Observable<PatientView> {
    return this.api.createPatient(payload).pipe(
      map((created) => {
        this.profiles.ensure(created.id);
        this.profiles.save(created.id, profile);
        return this.toView(created);
      })
    );
  }

  update(
    id: number,
    payload: CreatePatientRequest,
    profile: Partial<PatientProfile>
  ): Observable<PatientView> {
    return this.api.updatePatient(id, { ...payload, id }).pipe(
      switchMap(() => {
        this.profiles.save(id, profile);
        return this.get(id);
      })
    );
  }

  /**
   * Deletes the patient and everything that only exists because of them.
   *
   * Local data is cleared *after* the API confirms, so a failed delete cannot
   * leave a patient with their clinical history already gone.
   */
  remove(id: number): Observable<void> {
    return this.api.deletePatient(id).pipe(
      map(() => {
        this.records.removeForPatient(id);
        this.profiles.remove(id);
      })
    );
  }

  private toView(patient: Patient): PatientView {
    return {
      ...patient,
      profile: this.profiles.ensure(patient.id),
      age: calculateAge(patient.dateOfBirth),
    };
  }
}

function digits(value: string): string {
  return String(value ?? '').replace(/\D/g, '');
}

/** Matches `2026-00007`, `00007` and `7`, which is how staff actually type it. */
function matchesFileNumber(fileNumber: string, term: string): boolean {
  const normalised = term.trim().toLowerCase();
  const sequence = fileNumber.split('-')[1] ?? fileNumber;
  return (
    fileNumber.toLowerCase() === normalised ||
    sequence === normalised ||
    sequence === normalised.padStart(sequence.length, '0')
  );
}
