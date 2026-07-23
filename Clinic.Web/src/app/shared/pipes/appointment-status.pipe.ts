import { Pipe, PipeTransform } from '@angular/core';

@Pipe({
  name: 'appointmentStatus',
  standalone: true
})
export class AppointmentStatusPipe implements PipeTransform {
  transform(value: string | null | undefined): string {
    if (!value) {
      return 'Pending';
    }

    return value.charAt(0).toUpperCase() + value.slice(1).toLowerCase();
  }
}

