import { ChangeDetectionStrategy, ChangeDetectorRef, Component, effect, inject, input, output, signal } from '@angular/core';
import { AbstractControl, FormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { MatButton } from '@angular/material/button';
import { MatCard, MatCardContent, MatCardHeader, MatCardTitle } from '@angular/material/card';
import { MatError, MatFormField, MatLabel } from '@angular/material/form-field';
import { MatInput } from '@angular/material/input';
import { FormError } from '../../../shared/ui/form-error/form-error';
import { CreateTaskRequest, TaskStatus } from '../data/task.model';

function notBlank(control: AbstractControl): ValidationErrors | null {
  return typeof control.value === 'string' && control.value.trim() === '' ? { required: true } : null;
}

function localToday(): string {
  const d = new Date();
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
}

function notBeforeLocalToday(control: AbstractControl): ValidationErrors | null {
  const value = control.value;
  return typeof value === 'string' && value !== '' && value < localToday() ? { pastDate: true } : null;
}

@Component({
  selector: 'app-task-form',
  imports: [
    ReactiveFormsModule,
    MatButton,
    MatCard,
    MatCardContent,
    MatCardHeader,
    MatCardTitle,
    MatFormField,
    MatLabel,
    MatError,
    MatInput,
    FormError,
  ],
  templateUrl: './task-form.html',
  styleUrl: './task-form.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TaskForm {
  private readonly cdr = inject(ChangeDetectorRef);

  readonly submitting = input(false);
  readonly serverErrors = input<Record<string, string[]>>({});
  readonly submitted = output<CreateTaskRequest>();

  protected readonly today = signal(localToday());
  protected readonly form = inject(FormBuilder).nonNullable.group({
    title: ['', [Validators.required, notBlank, Validators.maxLength(200)]],
    description: ['', [Validators.maxLength(2000)]],
    status: ['ToDo' as TaskStatus],
    dueDate: ['', [notBeforeLocalToday]],
  });

  constructor() {
    effect(() => {
      const controls: Record<string, AbstractControl> = { ...this.form.controls };
      for (const [field, messages] of Object.entries(this.serverErrors())) {
        const key = Object.keys(controls).find((k) => k.toLowerCase() === field.toLowerCase());
        if (key && messages.length > 0) {
          controls[key].setErrors({ server: messages });
          controls[key].markAsTouched();
        }
      }
      this.cdr.markForCheck();
    });
  }

  /** Called by the parent after a successful create. */
  reset(): void {
    this.form.reset({ title: '', description: '', status: 'ToDo', dueDate: '' });
    this.today.set(localToday());
    this.cdr.markForCheck();
  }

  protected submit(): void {
    if (this.submitting()) {
      return;
    }
    this.today.set(localToday());
    this.form.controls.dueDate.updateValueAndValidity();
    this.form.markAllAsTouched();
    if (this.form.invalid) {
      return;
    }
    const { title, description, status, dueDate } = this.form.getRawValue();
    this.submitted.emit({
      title: title.trim(),
      description: description.trim() || null,
      status,
      dueDate: dueDate || null,
    });
  }
}
