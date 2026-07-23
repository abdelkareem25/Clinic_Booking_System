import { PageQuery } from './pagination.model';

export interface Doctor {
  id: number;
  name: string;
  specialization: string;
  email?: string;
  phone?: string;
  bio?: string;
  rating?: number;
  consultationFee?: number;
  imageUrl?: string;
}

export interface CreateDoctorRequest {
  id?: number;
  name: string;
  specialization: string;
}

export interface UpdateDoctorRequest {
  id: number;
  name: string;
  specialization: string;
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

