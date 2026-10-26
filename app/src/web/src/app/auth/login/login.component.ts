import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, ElementRef, inject, signal, viewChild } from '@angular/core';
import {
  AbstractControl,
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { AuthService, CurrentUser } from '../../core/auth/auth.service';
import {
  EMAIL_REQUIRED_MESSAGE,
  INVALID_CREDENTIALS_MESSAGE,
  LOGIN_PASSWORD_REQUIRED_MESSAGE,
  formErrorMessage,
  loginReasonMessage,
} from '../../core/errors/problem-mapper';
import { normalizeEmail } from '../register/register.component';

/** F1.R13 (same trim rule as register): an email that is empty after trimming is missing. No format check on login. */
function emailRequiredValidator(control: AbstractControl): ValidationErrors | null {
  return normalizeEmail(control.value ?? '') === '' ? { email_required: true } : null;
}

type FieldName = 'email' | 'password';

const REQUIRED_MESSAGES: Record<FieldName, string> = {
  email: EMAIL_REQUIRED_MESSAGE,
  password: LOGIN_PASSWORD_REQUIRED_MESSAGE,
};

@Component({
  selector: 'app-login',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LoginComponent {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly form = new FormGroup({
    email: new FormControl('', { nonNullable: true, validators: [emailRequiredValidator] }),
    password: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
  });

  protected readonly submitted = signal(false);
  protected readonly submitting = signal(false);

  /** F1.R15: the guard sends `?reason=login-required`; its copy shows until the first submit. */
  protected readonly formError = signal<string | null>(
    loginReasonMessage(inject(ActivatedRoute).snapshot.queryParamMap.get('reason')),
  );

  private readonly emailInput = viewChild.required<ElementRef<HTMLInputElement>>('emailInput');
  private readonly passwordInput = viewChild.required<ElementRef<HTMLInputElement>>('passwordInput');

  /** Copy for an empty field, shown only after the first submit attempt. */
  protected fieldMessage(name: FieldName): string | null {
    if (!this.submitted() || !this.form.controls[name].errors) {
      return null;
    }
    return REQUIRED_MESSAGES[name];
  }

  protected submit(): void {
    if (this.submitting()) {
      return;
    }
    this.submitted.set(true);
    this.formError.set(null);
    // An empty field is caught here, so no request is sent for it.
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
      .post<CurrentUser>('/api/auth/login', { email: normalizeEmail(email), password })
      .pipe(finalize(() => this.submitting.set(false)))
      .subscribe({
        next: (user) => {
          this.auth.markAuthenticated(user);
          // F1.R15: always the post-login page, never the page the visitor asked for.
          void this.router.navigateByUrl('/trips');
        },
        error: (error: unknown) => this.showFailure(error),
      });
  }

  private showFailure(error: unknown): void {
    // F1.R8: a wrong password and an unknown email get the same answer (401) and the same copy.
    if (error instanceof HttpErrorResponse && error.status === 401) {
      this.formError.set(INVALID_CREDENTIALS_MESSAGE);
      return;
    }
    this.formError.set(formErrorMessage(error));
  }
}
