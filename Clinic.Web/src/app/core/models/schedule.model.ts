import { PageQuery } from './pagination.model';

export enum WeekDay {
  Sunday = 0,
  Monday = 1,
  Tuesday = 2,
  Wednesday = 3,
  Thursday = 4,
  Friday = 5,
  Saturday = 6
}

export interface DoctorSchedule {
  id: number;
  doctorId: number;
  doctorName: string;
  weekDay: WeekDay;
  startTime: string;
  endTime: string;
}

export interface CreateScheduleRequest {
  doctorId: number;
  weekDay: WeekDay;
  startTime: string;
  endTime: string;
}

export interface UpdateScheduleRequest {
  weekDay: WeekDay;
  startTime: string;
  endTime: string;
}

export interface ScheduleQuery extends PageQuery {
  doctorId?: number;
  weekDay?: WeekDay;
}

export const WEEK_DAYS = [
  { value: WeekDay.Sunday, label: 'Sunday' },
  { value: WeekDay.Monday, label: 'Monday' },
  { value: WeekDay.Tuesday, label: 'Tuesday' },
  { value: WeekDay.Wednesday, label: 'Wednesday' },
  { value: WeekDay.Thursday, label: 'Thursday' },
  { value: WeekDay.Friday, label: 'Friday' },
  { value: WeekDay.Saturday, label: 'Saturday' }
];

