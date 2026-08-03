import { Injectable, computed } from '@angular/core';

import { BadgeTone } from '../../shared/ui/data-table/data-table.model';
import { IconName } from '../../shared/ui/icon/icon.registry';
import { Identified, LocalCollection, newId } from './local-collection';

export type RecordType =
  | 'visit'
  | 'diagnosis'
  | 'prescription'
  | 'lab'
  | 'imaging'
  | 'procedure'
  | 'note';

export interface Vitals {
  bloodPressure?: string;
  temperature?: number;
  pulse?: number;
  weight?: number;
}

export interface MedicalRecord extends Identified {
  patientId: number;
  patientName: string;
  doctorId?: number;
  doctorName?: string;
  type: RecordType;
  /** ISO date-time of the clinical event, not of data entry. */
  occurredAt: string;
  /** Free clinical text — never translated. */
  title: string;
  complaint?: string;
  diagnosis?: string;
  treatment?: string;
  prescription?: string;
  vitals?: Vitals;
  tags: string[];
  recordedBy: string;
  createdAt: string;
}

/** Presentation metadata per entry type, shared by the timeline and the table. */
export const RECORD_TYPE_META: Record<
  RecordType,
  { label: string; icon: IconName; tone: BadgeTone }
> = {
  visit: { label: 'records.typeVisit', icon: 'visit', tone: 'primary' },
  diagnosis: { label: 'records.typeDiagnosis', icon: 'vitals', tone: 'danger' },
  prescription: { label: 'records.typePrescription', icon: 'prescription', tone: 'info' },
  lab: { label: 'records.typeLab', icon: 'lab', tone: 'secondary' },
  imaging: { label: 'records.typeImaging', icon: 'imaging', tone: 'warning' },
  procedure: { label: 'records.typeProcedure', icon: 'procedure', tone: 'success' },
  note: { label: 'records.typeNote', icon: 'note', tone: 'neutral' },
};

export const RECORD_TYPES = Object.keys(RECORD_TYPE_META) as RecordType[];

/**
 * The clinical history.
 *
 * Records are immutable in spirit — an entry describes what was observed at a
 * point in time — so `occurredAt` is separate from `createdAt`: a doctor
 * writing up yesterday's visit this morning must not have it filed under today.
 */
@Injectable({ providedIn: 'root' })
export class MedicalRecordsStore {
  private readonly collection = new LocalCollection<MedicalRecord>({
    key: 'medical-records',
    version: 1,
    seed: () => [],
    searchFields: ['title', 'patientName', 'diagnosis', 'complaint'],
  });

  /** Newest clinical event first. */
  readonly records = computed(() =>
    [...this.collection.all()].sort((a, b) => b.occurredAt.localeCompare(a.occurredAt))
  );

  readonly count = this.collection.count;

  forPatient(patientId: number): MedicalRecord[] {
    return this.records().filter((record) => record.patientId === patientId);
  }

  getById(id: string): MedicalRecord | undefined {
    return this.collection.getById(id);
  }

  create(input: Omit<MedicalRecord, 'id' | 'createdAt'>): MedicalRecord {
    return this.collection.insert({
      ...input,
      id: newId('rec'),
      createdAt: new Date().toISOString(),
    });
  }

  update(id: string, patch: Partial<MedicalRecord>): void {
    this.collection.update(id, patch);
  }

  remove(id: string): void {
    this.collection.remove(id);
  }

  /** Called when a patient is deleted so records cannot outlive their subject. */
  removeForPatient(patientId: number): number {
    return this.collection.removeWhere((record) => record.patientId === patientId);
  }
}
