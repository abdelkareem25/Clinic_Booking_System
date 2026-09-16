import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { TranslatePipe } from '@ngx-translate/core';

import { Appointment } from '../../../core/models/appointment.model';
import { statusConfig } from '../../../core/utils/appointment-status.util';
import { appointmentDuration, appointmentStart } from '../../../core/utils/appointment-time.util';
import { formatTime12 } from '../../../core/utils/date.util';
import { EmptyStateComponent } from '../../../shared/ui/empty-state/empty-state.component';
import { IconComponent } from '../../../shared/ui/icon/icon.component';

export interface WaitingListData {
  /** Already scoped to the day on screen and to the active filters. */
  appointments: Appointment[];
  fallbackDurationMinutes: number;
  dateLabel: string;
}

/**
 * The day's unconfirmed bookings.
 *
 * There is no waiting-list entity on the API — no queue, no priority, no
 * walk-in record — so this does not pretend to be one. Inventing those rules in
 * the browser would produce a list that no other client, report or audit trail
 * could see, and that quietly disagreed with the database.
 *
 * What the domain *does* already record is `AppointmentStatus.Pending`: a
 * booking that exists but has not been confirmed, which is the front desk's
 * real call-back list. That is what this shows, labelled for what it is. When a
 * proper waiting list is added to the API, this component's shape stays and only
 * its data source changes.
 */
@Component({
  selector: 'app-waiting-list',
  imports: [MatButtonModule, MatDialogModule, TranslatePipe, EmptyStateComponent, IconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="wl-head">
      <span class="icon-tile icon-tile-lg icon-tile-warning">
        <ui-icon name="checklist" size="lg" />
      </span>

      <div class="wl-titles">
        <h2 mat-dialog-title class="wl-title">{{ 'appointments.waitingList' | translate }}</h2>
        <p class="wl-sub">{{ data.dateLabel }}</p>
      </div>
    </div>

    <mat-dialog-content class="wl-body">
      <p class="wl-hint">{{ 'appointments.waitingListHint' | translate }}</p>

      @if (rows().length) {
        <ul class="wl-list">
          @for (row of rows(); track row.appointment.id) {
            <li class="wl-row">
              <button class="wl-btn" type="button" (click)="open(row.appointment)">
                <span class="wl-time">{{ row.time }}</span>

                <span class="wl-text">
                  <span class="wl-patient">{{ row.appointment.patientName }}</span>
                  <span class="wl-doctor">{{ row.appointment.doctorName }}</span>
                </span>

                <span class="badge" [class]="'badge-' + row.tone">
                  {{ row.statusLabel | translate }}
                </span>
              </button>
            </li>
          }
        </ul>
      } @else {
        <ui-empty-state
          title="appointments.waitingListEmpty"
          message="appointments.waitingListEmptyHint"
          icon="checklist"
          [compact]="true"
        />
      }
    </mat-dialog-content>

    <mat-dialog-actions align="end">
      <button mat-button type="button" (click)="dialogRef.close(null)">
        {{ 'common.close' | translate }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .wl-head {
      display: flex;
      align-items: center;
      gap: var(--sp-3);
      padding: var(--sp-5) var(--sp-5) var(--sp-3);
    }

    .wl-titles {
      display: flex;
      flex-direction: column;
      gap: 2px;
      min-width: 0;
    }

    .wl-title {
      padding: 0 !important;
      margin: 0 !important;
      font-size: var(--fs-lg) !important;
      font-weight: var(--fw-semibold) !important;
    }

    .wl-sub {
      font-size: var(--fs-sm);
      color: var(--c-text-muted);
    }

    .wl-body {
      padding-inline: var(--sp-5) !important;
      min-width: min(420px, 78vw);
    }

    .wl-hint {
      font-size: var(--fs-xs);
      color: var(--c-text-muted);
      margin-block-end: var(--sp-3);
    }

    .wl-list {
      display: flex;
      flex-direction: column;
      gap: var(--sp-1);
      list-style: none;
      margin: 0;
      padding: 0;
    }

    .wl-btn {
      display: flex;
      align-items: center;
      gap: var(--sp-3);
      width: 100%;
      padding: var(--sp-2) var(--sp-3);
      border: 1px solid var(--c-border);
      border-radius: var(--r-md);
      background: var(--c-surface);
      text-align: start;
      cursor: pointer;
      transition: background-color var(--dur-micro) var(--ease-standard);
    }

    .wl-btn:hover {
      background: var(--c-surface-2);
    }

    .wl-btn:focus-visible {
      outline: none;
      box-shadow: var(--sh-focus);
    }

    .wl-time {
      flex: 0 0 auto;
      font-size: var(--fs-xs);
      font-weight: var(--fw-semibold);
      font-variant-numeric: tabular-nums;
      color: var(--c-text-muted);
      min-width: 72px;
    }

    .wl-text {
      display: flex;
      flex-direction: column;
      min-width: 0;
      flex: 1 1 auto;
    }

    .wl-patient {
      font-size: var(--fs-sm);
      font-weight: var(--fw-medium);
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }

    .wl-doctor {
      font-size: var(--fs-2xs);
      color: var(--c-text-muted);
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
  `,
})
export class WaitingListComponent {
  readonly dialogRef = inject<MatDialogRef<WaitingListComponent, Appointment | null>>(MatDialogRef);
  readonly data = inject<WaitingListData>(MAT_DIALOG_DATA);

  protected readonly rows = computed(() =>
    this.data.appointments
      .map((appointment) => {
        const start = appointmentStart(appointment);
        const startMinutes = start ? start.getHours() * 60 + start.getMinutes() : 0;
        const config = statusConfig(appointment.status);

        return {
          appointment,
          startMinutes,
          time: `${formatTime12(startMinutes)} – ${formatTime12(
            startMinutes + appointmentDuration(appointment, this.data.fallbackDurationMinutes)
          )}`,
          statusLabel: config.label,
          tone: config.tone,
        };
      })
      .sort((a, b) => a.startMinutes - b.startMinutes)
  );

  /** Closing with an appointment asks the calendar to open its quick view. */
  protected open(appointment: Appointment): void {
    this.dialogRef.close(appointment);
  }
}
