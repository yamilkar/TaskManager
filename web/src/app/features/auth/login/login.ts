import { ChangeDetectionStrategy, Component, inject, input, signal, viewChild } from '@angular/core';
import { FormBuilder, FormGroupDirective, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButton } from '@angular/material/button';
import { MatCard, MatCardContent, MatCardHeader, MatCardTitle } from '@angular/material/card';
import { MatError, MatFormField, MatLabel } from '@angular/material/form-field';
import { MatInput } from '@angular/material/input';
import { Router, RouterLink } from '@angular/router';
import { AuthStore } from '../../../core/auth/auth-store';
import { AppError } from '../../../core/http/app-error';

const INVALID_CREDENTIALS = 'Invalid email or password.';

@Component({
  selector: 'app-login',
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
    MatInput,
  ],
  templateUrl: './login.html',
  styleUrl: './login.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Login {
  private readonly auth = inject(AuthStore);
  private readonly router = inject(Router);

  readonly returnUrl = input<string>();
  readonly reason = input<string>();

  protected readonly form = inject(FormBuilder).nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required],
  });
  private readonly formDirective = viewChild.required(FormGroupDirective);
  protected readonly submitting = signal(false);
  protected readonly errorMessage = signal<string | null>(null);

  protected useDemo(): void {
    this.form.setValue({ email: 'demo@taskmanager.dev', password: 'Demo123!' });
    this.errorMessage.set(null);
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
    try {
      await this.auth.login(this.form.getRawValue());
      await this.router.navigateByUrl(this.returnUrl() || '/tasks');
    } catch (e) {
      const err = e as Partial<AppError>;
      this.errorMessage.set(err.status === 401 ? INVALID_CREDENTIALS : (err.message ?? INVALID_CREDENTIALS));
      // resetForm also clears the directive's submitted flag, so no "required" error shows on the emptied field.
      this.formDirective().resetForm({ email: this.form.controls.email.value, password: '' });
    } finally {
      this.submitting.set(false);
    }
  }
}
