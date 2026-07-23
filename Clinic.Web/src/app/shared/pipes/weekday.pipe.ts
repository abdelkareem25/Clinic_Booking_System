import { Pipe, PipeTransform } from '@angular/core';

import { WEEK_DAYS, WeekDay } from '../../core/models/schedule.model';

@Pipe({
  name: 'weekday',
  standalone: true
})
export class WeekdayPipe implements PipeTransform {
  transform(value: WeekDay | number | string | null | undefined): string {
    const numericValue = Number(value);
    return WEEK_DAYS.find((day) => day.value === numericValue)?.label ?? String(value ?? '');
  }
}

