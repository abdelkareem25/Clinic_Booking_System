import { Injectable, computed } from '@angular/core';

import { Identified, LocalCollection } from './local-collection';

export const BLOOD_GROUPS = ['A+', 'A-', 'B+', 'B-', 'AB+', 'AB-', 'O+', 'O-'] as const;

export type BloodGroup = (typeof BLOOD_GROUPS)[number];

/**
 * The clinical and contact detail the API's `Patient` entity does not carry.
 *
 * The backend stores only name, phone, date of birth and gender. Everything a
 * clinician actually needs before touching a patient — allergies, chronic
 * conditions, current medications, who to call — lives here, keyed by the API's
 * patient id so the two halves join cleanly and can be merged server-side later
 * without touching a single component.
 */
export interface PatientProfile extends Identified {
  /** `String(patientId)` — the API patient this profile extends. */
  id: string;
  patientId: number;
  /** Issued file number, e.g. `2026-00001`. */
  fileNumber: string;
  nationalId?: string;
  bloodGroup?: BloodGroup;
  address?: string;
  emergencyContactName?: string;
  emergencyContactPhone?: string;
  emergencyRelation?: string;
  allergies: string[];
  chronicDiseases: string[];
  currentMedications: string[];
  notes?: string;
  createdAt: string;
}

@Injectable({ providedIn: 'root' })
export class PatientProfileStore {
  private readonly collection = new LocalCollection<PatientProfile>({
    key: 'patient-profiles',
    version: 1,
    seed: () => [],
    searchFields: ['fileNumber', 'nationalId', 'address'],
  });

  readonly profiles = this.collection.all;

  readonly byPatientId = computed(() => {
    const map = new Map<number, PatientProfile>();
    for (const profile of this.profiles()) {
      map.set(profile.patientId, profile);
    }
    return map;
  });

  get(patientId: number): PatientProfile | undefined {
    return this.collection.getById(String(patientId));
  }

  /**
   * Returns the stored profile, creating an empty one on first access.
   *
   * Patients created directly against the API have no profile yet, and a detail
   * screen must never fail because of that — so the profile is materialised on
   * demand with a freshly issued file number.
   */
  ensure(patientId: number, createdAt?: string): PatientProfile {
    const existing = this.get(patientId);
    if (existing) {
      return existing;
    }

    return this.collection.insert({
      id: String(patientId),
      patientId,
      fileNumber: this.issueFileNumber(patientId, createdAt),
      allergies: [],
      chronicDiseases: [],
      currentMedications: [],
      createdAt: createdAt ?? new Date().toISOString(),
    });
  }

  save(patientId: number, patch: Partial<Omit<PatientProfile, 'id' | 'patientId'>>): PatientProfile {
    this.ensure(patientId);
    return this.collection.update(String(patientId), patch) ?? this.ensure(patientId);
  }

  remove(patientId: number): void {
    this.collection.remove(String(patientId));
  }

  /** Reverse lookup for the search field's file-number mode. */
  findByFileNumber(fileNumber: string): PatientProfile | undefined {
    const needle = fileNumber.trim().toLowerCase();
    return this.profiles().find(
      (profile) =>
        profile.fileNumber.toLowerCase() === needle ||
        // Staff routinely type just the sequence, without the year prefix.
        profile.fileNumber.split('-')[1] === needle.padStart(5, '0')
    );
  }

  /** `YYYY-NNNNN`, where the year is the registration year, not the current one. */
  private issueFileNumber(patientId: number, createdAt?: string): string {
    const year = createdAt ? new Date(createdAt).getFullYear() : new Date().getFullYear();
    return `${year}-${`${patientId}`.padStart(5, '0')}`;
  }
}
