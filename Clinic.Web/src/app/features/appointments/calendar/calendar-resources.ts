import { Appointment } from '../../../core/models/appointment.model';
import { Doctor } from '../../../core/models/doctor.model';
import { CalendarResource, CalendarResourceProvider } from './calendar.model';

/** `doctor-7`. Prefixed so ids stay distinct once chairs and rooms exist too. */
export function doctorResourceId(doctorId: number): string {
  return `doctor-${doctorId}`;
}

/**
 * The only resource provider the current backend can support.
 *
 * A column is a doctor, because a doctor is what the API schedules against: the
 * working-hours rule reads `DoctorSchedule`, and the overlap rule is
 * per-`DoctorId`. A chair column would have to answer "who may be booked into
 * it and when", and nothing in the domain can answer that yet.
 *
 * To add chairs later: give `Chair` an owning doctor (or a rota), write a
 * `ChairResourceProvider` that returns one resource per chair with that doctor's
 * id, and map an appointment to its chair in `resourceIdOf`. Nothing outside this
 * file needs to change — the grid, the layout engine and the drop handler only
 * ever see `CalendarResource`.
 */
export class DoctorResourceProvider implements CalendarResourceProvider {
  readonly kind = 'doctor' as const;

  constructor(
    private readonly doctors: readonly Doctor[],
    /** Localises the speciality shown under each column heading. */
    private readonly specialtyLabel: (raw: string | null | undefined) => string
  ) {}

  resources(): CalendarResource[] {
    return this.doctors.map((doctor) => ({
      id: doctorResourceId(doctor.id),
      kind: this.kind,
      name: doctor.name,
      sublabel: this.specialtyLabel(doctor.specialization) || null,
      doctorId: doctor.id,
    }));
  }

  resourceIdOf(appointment: Appointment): string | null {
    return appointment.doctorId ? doctorResourceId(appointment.doctorId) : null;
  }
}
