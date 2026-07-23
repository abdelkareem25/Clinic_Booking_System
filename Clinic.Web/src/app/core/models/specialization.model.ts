export interface Specialization {
  id: number;
  name: string;
  description?: string;
  isActive?: boolean;
}

export interface UpsertSpecializationRequest {
  name: string;
  description?: string;
  isActive?: boolean;
}

