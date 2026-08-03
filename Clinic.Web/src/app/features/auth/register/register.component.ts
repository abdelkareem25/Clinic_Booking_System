import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { Router, RouterLink } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';

import { ROLES } from '../../../core/models/auth.model';
import { AuthService } from '../../../core/services/auth.service';
import { NotificationService } from '../../../core/services/notification.service';
import {
  nameValidators,
  passwordMatchValidator,
  phoneValidator,
  strongPasswordValidator,
} from '../../../core/utils/validators';
import { FieldErrorComponent } from '../../../shared/ui/field-error/field-error.component';
import { IconComponent } from '../../../shared/ui/icon/icon.component';

@Component({
  selector: 'app-register',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatProgressBarModule,
    MatSelectModule,
    TranslatePipe,
    FieldErrorComponent,
    IconComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './register.component.html',
  styleUrl: './register.component.scss',
})
export class RegisterComponent {
  private readonly auth = inject(AuthService);
  private readonly formBuilder = inject(FormBuilder);
  private readonly notifications = inject(NotificationService);
  private readonly router = inject(Router);
  private readonly translate = inject(TranslateService);

  protected readonly roles = ROLES;

  protected readonly form = this.formBuilder.nonNullable.group(
    {
      displayName: ['', nameValidators],
      email: ['', [Validators.required, Validators.email]],
      phoneNumber: ['', [Validators.required, phoneValidator]],
      password: ['', [Validators.required, strongPasswordValidator]],
      confirmPassword: ['', [Validators.required]],
    },
    { validators: passwordMatchValidator() }
  );

  protected readonly submitting = signal(false);
  protected readonly submitted = signal(false);
  protected readonly showPassword = signal(false);
  protected readonly serverError = signal<string | null>(null);

  protected togglePassword(): void {
    this.showPassword.update((value) => !value);
  }

  protected submit(): void {
    this.submitted.set(true);
    this.serverError.set(null);

    if (this.form.invalid || this.submitting()) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    const { displayName, email, phoneNumber, password } = this.form.getRawValue();

    this.auth.register({ displayName, email, phoneNumber, password }).subscribe({
      next: () => {
        this.submitting.set(false);
        this.notifications.success(this.translate.instant('users.created'));
        void this.router.navigateByUrl(this.auth.redirectPathFor());
      },
      error: (error: unknown) => {
        this.submitting.set(false);
        // Registration is admin-only on the API, so a 401/403 here is a
        // permission problem rather than bad input — say so plainly.
        const status = (error as { status?: number })?.status;
        this.serverError.set(
          status === 401 || status === 403 ? 'errors.forbiddenBody' : 'errors.genericBody'
        );
      },
    });
  }
}
