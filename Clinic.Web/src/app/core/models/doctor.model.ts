import { PageQuery } from './pagination.model';
import { WeekDay } from './schedule.model';

export interface Doctor {
  id: number;
  name: string;
  specialization: string;
  email?: string;
  phone?: string;
  bio?: string;
  rating?: number;
  consultationFee?: number;
  isActive?: boolean;
  imageUrl?: string;
}

/**
 * One shift on the rota, as sent alongside a new doctor.
 *
 * No `doctorId`: the doctor does not exist yet when this is composed, and the
 * API assigns the key inside the same transaction that writes these rows.
 */
export interface DoctorShiftRequest {
  weekDay: WeekDay;
  /** `HH:mm:ss` — the shape the API's `TimeOnly` binder expects. */
  startTime: string;
  endTime: string;
}

export interface CreateDoctorRequest {
  name: string;
  specialization: string;
  phone?: string | null;
  email?: string | null;
  consultationFee?: number | null;
  bio?: string | null;
  isActive: boolean;
  /** Empty is legitimate: a doctor may be registered before their rota is agreed. */
  schedules: DoctorShiftRequest[];
}

export interface UpdateDoctorRequest {
  id: number;
  name: string;
  specialization: string;
  phone?: string | null;
  email?: string | null;
  consultationFee?: number | null;
  bio?: string | null;
  isActive: boolean;
}

export interface DoctorQuery extends PageQuery {
  specialty?: string;
}

export const DEFAULT_SPECIALIZATIONS = [
  'Cardiology',
  'Dermatology',
  'Family Medicine',
  'Neurology',
  'Orthopedics',
  'Pediatrics',
  'Psychiatry',
  'Radiology'
] as const;
