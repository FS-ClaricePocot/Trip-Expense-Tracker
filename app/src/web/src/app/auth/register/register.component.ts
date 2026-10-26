import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, ElementRef, inject, signal, viewChild } from '@angular/core';
import {
  AbstractControl,
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  ValidationErrors,
} from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { AuthService, CurrentUser } from '../../core/auth/auth.service';
import {
  fieldErrorMessage,
  fieldErrorMessages,
  formErrorMessage,
} from '../../core/errors/problem-mapper';

/** F1.R13: trim and lower-case before the email is checked or sent. */
export function normalizeEmail(value: string): string {
  return value.trim().toLowerCase();
}

/** F1.R13: non-empty, exactly one @, and at least one dot after it. */
function emailValidator(control: AbstractControl): ValidationErrors | null {
  const email = normalizeEmail(control.value ?? '');
  if (email === '') {
    return { email_required: true };
  }
  const parts = email.split('@');
  if (parts.length !== 2 || !parts[1].includes('.')) {
    return { email_invalid: true };
  }
  return null;
}

/** F1.R3 and F1.R14: 8 to 128 code points, not trimmed. */
function passwordValidator(control: AbstractControl): ValidationErrors | null {
  const length = [...(control.value ?? '')].length;
  if (length < 8) {
    return { password_too_short: true };
  }
  if (length > 128) {
    return { password_too_long: true };
  }
  return null;
}

type FieldName = 'email' | 'password';

@Component({
  selector: 'app-register',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './register.component.html',
  styleUrl: './register.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RegisterComponent {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly form = new FormGroup({
    email: new FormControl('', { nonNullable: true, validators: [emailValidator] }),
    password: new FormControl('', { nonNullable: true, validators: [passwordValidator] }),
  });

  protected readonly submitted = signal(false);
  protected readonly submitting = signal(false);
  protected readonly formError = signal<string | null>(null);

  private readonly emailInput = viewChild.required<ElementRef<HTMLInputElement>>('emailInput');
  private readonly passwordInput = viewChild.required<ElementRef<HTMLInputElement>>('passwordInput');

  /** Copy for a field, shown only after the first submit attempt. */
  protected fieldMessage(name: FieldName): string | null {
    const control = this.form.controls[name];
    if (!this.submitted() || !control.errors) {
      return null;
    }
    // Server field errors are stored as a `server` key with the copy already applied.
    if (typeof control.errors['server'] === 'string') {
      return control.errors['server'];
    }
    return fieldErrorMessage(Object.keys(control.errors)[0]);
  }

  protected submit(): void {
    if (this.submitting()) {
      return;
    }
    this.submitted.set(true);
    this.formError.set(null);
    // Re-run the checks so server-side field errors from an earlier attempt are cleared.
    for (const control of Object.values(this.form.controls)) {
      control.updateValueAndValidity();
    }
    if (this.form.invalid) {
      if (this.form.controls.email.invalid) {
        this.emailInput().nativeElement.focus();
      } else {
        this.passwordInput().nativeElement.focus();
      }
      return;
    }

    this.submitting.set(true);
    const { email, password } = this.form.getRawValue();
    // Subscribed directly (not awaited) so the success path runs in the same tick as the response.
    this.http
      .post<CurrentUser>('/api/auth/register', { email: normalizeEmail(email), password })
      .pipe(finalize(() => this.submitting.set(false)))
      .subscribe({
        next: (user) => {
          this.auth.markAuthenticated(user);
          void this.router.navigateByUrl('/trips');
        },
        error: (error: unknown) => this.showFailure(error),
      });
  }

  private showFailure(error: unknown): void {
    if (error instanceof HttpErrorResponse && error.status === 400) {
      const messages = fieldErrorMessages(error);
      const emailMessage = messages['email'];
      const passwordMessage = messages['password'];
      if (emailMessage || passwordMessage) {
        if (emailMessage) {
          this.form.controls.email.setErrors({ server: emailMessage });
        }
        if (passwordMessage) {
          this.form.controls.password.setErrors({ server: passwordMessage });
        }
        return;
      }
    }
    this.formError.set(formErrorMessage(error));
  }
}
