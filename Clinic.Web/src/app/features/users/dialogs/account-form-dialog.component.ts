import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import {
  AbstractControl,
  FormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  ValidatorFn,
  Validators,
} from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Observable } from 'rxjs';

import { ACCOUNT_ROLES, Account } from '../../../core/models/account.model';
import { AccountsService } from '../../../core/services/accounts.service';
import { NotificationService } from '../../../core/services/notification.service';
import {
  nameValidators,
  passwordMatchValidator,
  phoneValidator,
  strongPasswordValidator,
  usernameValidator,
} from '../../../core/utils/validators';
import { FieldErrorComponent } from '../../../shared/ui/field-error/field-error.component';
import { IconComponent } from '../../../shared/ui/icon/icon.component';

export interface AccountDialogData {
  account?: Account;
  /** The signed-in account's id, so the form can refuse to let it lock itself out. */
  currentUserId: string | null;
}

/** Marks the confirmation field required once a password has been typed. */
const confirmationRequiredValidator: ValidatorFn = (
  group: AbstractControl
): ValidationErrors | null => {
  const password = group.get('password')?.value;
  const confirm = group.get('confirmPassword');

  if (!confirm || !password || confirm.value) {
    return null;
  }

  // Set on the control, not the group, so `ui-field-error` can render it under
  // the field it belongs to.
  confirm.setErrors({ ...(confirm.errors ?? {}), required: true });
  return { required: true };
};

/**
 * Create and edit a staff account.
 *
 * One dialog for both, because the fields are the same apart from the password:
 * mandatory and confirmed on create, optional and confirmed-only-if-supplied on
 * edit. Two components would duplicate seven identical controls to express that
 * one difference.
 */
@Component({
  selector: 'app-account-form-dialog',
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatCheckboxModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    TranslatePipe,
    FieldErrorComponent,
    IconComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './account-form-dialog.component.html',
  styleUrl: './account-form-dialog.component.scss',
})
export class AccountFormDialogComponent {
  private readonly api = inject(AccountsService);
  private readonly formBuilder = inject(FormBuilder);
  private readonly notifications = inject(NotificationService);
  private readonly translate = inject(TranslateService);

  readonly dialogRef = inject<MatDialogRef<AccountFormDialogComponent, boolean>>(MatDialogRef);
  readonly data = inject<AccountDialogData>(MAT_DIALOG_DATA);

  protected readonly isEdit = Boolean(this.data.account);
  protected readonly saving = signal(false);
  protected readonly submitted = signal(false);
  protected readonly showPasswordFields = signal(!this.data.account);

  protected readonly roles = ACCOUNT_ROLES;

  /**
   * The API refuses to let an administrator change their own role or deactivate
   * themselves. Disabling the controls says so before the request is sent,
   * rather than letting the form look editable and then 409.
   */
  protected readonly isSelf =
    Boolean(this.data.account) && this.data.account!.id === this.data.currentUserId;

  protected readonly form = this.formBuilder.nonNullable.group(
    {
      displayName: [this.data.account?.displayName ?? '', nameValidators],
      // The username is fixed after creation: it is the subject of every issued
      // token and the value audit stamps resolve through.
      userName: [
        { value: this.data.account?.userName ?? '', disabled: this.isEdit },
        [usernameValidator, Validators.maxLength(256)],
      ],
      email: [
        this.data.account?.email ?? '',
        [Validators.required, Validators.email, Validators.maxLength(256)],
      ],
      phoneNumber: [this.data.account?.phoneNumber ?? '', [phoneValidator]],
      role: [
        { value: (this.data.account?.role as string) ?? 'Receptionist', disabled: this.isSelf },
        [Validators.required],
      ],
      isActive: [
        { value: this.data.account?.isActive ?? true, disabled: this.isSelf },
      ],
      password: [
        '',
        this.isEdit ? [strongPasswordValidator] : [Validators.required, strongPasswordValidator],
      ],
      confirmPassword: [''],
    },
    {
      validators: [
        passwordMatchValidator('password', 'confirmPassword'),
        // `passwordMatchValidator` returns null when the confirmation is empty,
        // so on an edit a typed password with a blank confirmation would sail
        // through here and come back as a 400 from the API. Require it as soon
        // as there is something to confirm.
        confirmationRequiredValidator,
      ],
    }
  );

  /** Reveals the optional reset fields on an edit. */
  protected togglePasswordFields(): void {
    const next = !this.showPasswordFields();
    this.showPasswordFields.set(next);

    if (!next) {
      // Clearing on collapse matters: a half-typed password left in a hidden
      // control would still be submitted and would still have to validate.
      this.form.controls.password.reset('');
      this.form.controls.confirmPassword.reset('');
    }
  }

  protected submit(): void {
    this.submitted.set(true);

    if (this.form.invalid || this.saving()) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    // getRawValue() rather than value: it includes the disabled controls, and a
    // PUT that omitted role or isActive would be read as "set them to nothing".
    const raw = this.form.getRawValue();

    const shared = {
      displayName: raw.displayName.trim(),
      email: raw.email.trim(),
      phoneNumber: raw.phoneNumber.trim() || null,
      role: raw.role,
      isActive: raw.isActive,
    };

    const request: Observable<Account> = this.data.account
      ? this.api.updateAccount(this.data.account.id, {
          ...shared,
          newPassword: raw.password || null,
          confirmNewPassword: raw.password ? raw.confirmPassword : null,
        })
      : this.api.createAccount({
          ...shared,
          userName: raw.userName.trim() || null,
          password: raw.password,
          confirmPassword: raw.confirmPassword,
        });

    request.subscribe({
      next: () => {
        this.saving.set(false);
        this.notifications.success(
          this.translate.instant(this.isEdit ? 'users.updated' : 'users.createdMessage')
        );
        this.dialogRef.close(true);
      },
      // The HTTP interceptor already surfaces the API's message (a 409 on a
      // duplicate email, a 400 with Identity's password complaints), so there is
      // nothing useful to add here beyond releasing the button.
      error: () => this.saving.set(false),
    });
  }
}
