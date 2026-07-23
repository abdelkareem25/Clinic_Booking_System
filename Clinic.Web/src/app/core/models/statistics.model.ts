import { Appointment } from './appointment.model';

export interface DashboardMetric {
  label: string;
  value: number;
  icon: string;
  tone: 'primary' | 'accent' | 'success' | 'warning';
}

export interface AdminStatistics {
  doctors: number;
  patients: number;
  appointments: number;
  schedules: number;
}

export interface DoctorStatistics {
  totalAppointments: number;
  upcomingAppointments: number;
  completedAppointments: number;
}

export interface DashboardData {
  totalDoctors: number;
  totalPatients: number;
  totalAppointments: number;
  todaysAppointments: number;
  recentAppointments: Appointment[];
}
