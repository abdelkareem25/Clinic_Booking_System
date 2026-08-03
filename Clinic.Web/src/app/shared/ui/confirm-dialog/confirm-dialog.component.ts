import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { TranslatePipe } from '@ngx-translate/core';
import { Observable } from 'rxjs';

import { IconComponent } from '../icon/icon.component';
import { IconName } from '../icon/icon.registry';

export interface ConfirmDialogData {
  /** Translation key. */
  title: string;
  /** Translation key; `messageParams` feeds its interpolation. */
  message: string;
  messageParams?: Record<string, unknown>;
  confirmLabel?: string;
  cancelLabel?: string;
  tone?: 'danger' | 'primary';
  icon?: IconName;
}

@Component({
  selector: 'ui-confirm-dialog',
  imports: [MatButtonModule, MatDialogModule, TranslatePipe, IconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="confirm">
      <span class="icon-tile icon-tile-lg" [class]="'icon-tile-' + tone">
        <ui-icon [name]="data.icon ?? (tone === 'danger' ? 'warning' : 'help')" size="lg" />
      </span>

      <div class="text">
        <h2 mat-dialog-title class="title">{{ data.title | translate }}</h2>
        <p class="message">{{ data.message | translate: data.messageParams }}</p>
      </div>
    </div>

    <mat-dialog-actions align="end">
      <button mat-button type="button" (click)="dialogRef.close(false)">
        {{ data.cancelLabel ?? 'common.cancel' | translate }}
      </button>
      <button
        mat-flat-button
        type="button"
        cdkFocusInitial
        [class.btn-danger]="tone === 'danger'"
        (click)="dialogRef.close(true)"
      >
        {{ data.confirmLabel ?? 'common.confirm' | translate }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .confirm {
      display: flex;
      gap: var(--sp-4);
      padding: var(--sp-6) var(--sp-6) var(--sp-2);
      max-width: 440px;
    }

    .text {
      display: flex;
      flex-direction: column;
      gap: var(--sp-2);
      min-width: 0;
    }

    .title {
      padding: 0 !important;
      font-size: var(--fs-md) !important;
    }

    .message {
      font-size: var(--fs-sm);
      color: var(--c-text-muted);
      line-height: var(--lh-snug);
    }
  `,
})
export class ConfirmDialogComponent {
  readonly dialogRef = inject<MatDialogRef<ConfirmDialogComponent, boolean>>(MatDialogRef);
  readonly data = inject<ConfirmDialogData>(MAT_DIALOG_DATA);

  protected readonly tone = this.data.tone ?? 'danger';
}

/**
 * Opens the confirmation and resolves to the user's answer.
 *
 * Destructive actions must never be one click away — this is the single path
 * every delete goes through, so the wording, focus order (Cancel first) and
 * escape behaviour are identical everywhere.
 */
export function confirmDialog(
  dialog: MatDialog,
  data: ConfirmDialogData
): Observable<boolean | undefined> {
  return dialog
    .open<ConfirmDialogComponent, ConfirmDialogData, boolean>(ConfirmDialogComponent, {
      data,
      width: '460px',
      maxWidth: '94vw',
      autoFocus: 'dialog',
      restoreFocus: true,
    })
    .afterClosed();
}
