import { ChangeDetectionStrategy, ChangeDetectorRef, Component, inject, signal } from '@angular/core';
import { AbstractControl, FormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { MatButton } from '@angular/material/button';
import { MatCard, MatCardContent, MatCardHeader, MatCardTitle } from '@angular/material/card';
import { MatError, MatFormField, MatHint, MatLabel } from '@angular/material/form-field';
import { MatInput } from '@angular/material/input';
import { Router, RouterLink } from '@angular/router';
import { AuthStore } from '../../../core/auth/auth-store';
import { AppError } from '../../../core/http/app-error';
import { FormError } from '../../../shared/ui/form-error/form-error';

function matchesPassword(control: AbstractControl): ValidationErrors | null {
  const password = control.parent?.get('password')?.value;
  return control.value === password ? null : { mismatch: true };
}

@Component({
  selector: 'app-register',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatButton,
    MatCard,
    MatCardContent,
    MatCardHeader,
    MatCardTitle,
    MatFormField,
    MatLabel,
    MatError,
    MatHint,
    MatInput,
    FormError,
  ],
  templateUrl: './register.html',
  styleUrl: './register.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Register {
  private readonly auth = inject(AuthStore);
  private readonly router = inject(Router);
  private readonly cdr = inject(ChangeDetectorRef);

  protected readonly form = inject(FormBuilder).nonNullable.group({
    fullName: ['', [Validators.required, Validators.maxLength(100)]],
    email: ['', [Validators.required, Validators.email, Validators.maxLength(254)]],
    password: [
      '',
      [
        Validators.required,
        Validators.minLength(8),
        Validators.maxLength(128),
        Validators.pattern(/^(?=.*[A-Za-z])(?=.*\d).*$/),
      ],
    ],
    confirmPassword: ['', [Validators.required, matchesPassword]],
  });
  protected readonly submitting = signal(false);
  protected readonly errorMessage = signal<string | null>(null);

  constructor() {
    this.form.controls.password.valueChanges.subscribe(() =>
      this.form.controls.confirmPassword.updateValueAndValidity(),
    );
  }

  protected async submit(): Promise<void> {
    if (this.submitting()) {
      return;
    }
    this.form.markAllAsTouched();
    if (this.form.invalid) {
      return;
    }
    this.submitting.set(true);
    this.errorMessage.set(null);
    const { fullName, email, password } = this.form.getRawValue();
    try {
      const outcome = await this.auth.register({ fullName, email, password });
      await this.router.navigateByUrl(outcome === 'signed-in' ? '/tasks' : '/login?reason=registered');
    } catch (e) {
      this.showServerError(e as AppError);
    } finally {
      this.submitting.set(false);
    }
  }

  private showServerError(err: AppError): void {
    const controls: Record<string, AbstractControl> = { ...this.form.controls };
    let mapped = false;
    for (const [field, messages] of Object.entries(err.fieldErrors ?? {})) {
      const key = Object.keys(controls).find((k) => k.toLowerCase() === field.toLowerCase());
      if (key && messages.length > 0) {
        controls[key].setErrors({ server: messages });
        mapped = true;
      }
    }
    if (err.status === 409) {
      this.form.controls.email.setErrors({ server: [err.message] });
    } else if (!mapped) {
      this.errorMessage.set(err.message);
    }
    this.cdr.markForCheck();
  }
}
