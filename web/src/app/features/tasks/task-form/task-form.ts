import { ChangeDetectionStrategy, ChangeDetectorRef, Component, effect, inject, input, output, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AbstractControl, FormBuilder, ReactiveFormsModule, ValidationErrors, ValidatorFn, Validators } from '@angular/forms';
import { MatButton } from '@angular/material/button';
import { MatCard, MatCardContent, MatCardHeader, MatCardTitle } from '@angular/material/card';
import { MatError, MatFormField, MatHint, MatLabel } from '@angular/material/form-field';
import { MatInput } from '@angular/material/input';
import { FormError } from '../../../shared/ui/form-error/form-error';
import { CreateTaskRequest, TaskStatus } from '../data/task.model';

/** Must stay identical to the server's `TaskItem.DueDateInPastMessage` (spec D18); both sides pin it in a test. */
export const DUE_DATE_IN_PAST_MESSAGE = 'Due date cannot be in the past. Choose today or a later date.';

function maxTrimmedLength(max: number): ValidatorFn {
  return (control) => {
    const value = control.value;
    if (typeof value !== 'string') {
      return null;
    }
    const actualLength = value.trim().length;
    return actualLength > max ? { maxlength: { requiredLength: max, actualLength } } : null;
  };
}

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
    MatHint,
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

  protected readonly dueDateInPastMessage = DUE_DATE_IN_PAST_MESSAGE;
  protected readonly today = signal(localToday());
  protected readonly form = inject(FormBuilder).nonNullable.group({
    title: ['', [Validators.required, notBlank, maxTrimmedLength(200)]],
    description: ['', [maxTrimmedLength(2000)]],
    status: ['ToDo' as TaskStatus],
    dueDate: ['', [notBeforeLocalToday]],
  });

  constructor() {
    this.form.controls.dueDate.valueChanges.pipe(takeUntilDestroyed()).subscribe((value) => {
      // Mark touched early only once the year is complete, so typing "2026" never flashes a past-date error:
      // a native date input emits 0002-…, 0020-…, 0202-… on the way. Years below 1000 count as "still typing"
      // and show the error only on blur or submit. The year is zero-padded, so the string compare orders like a number.
      if (typeof value === 'string' && /^\d{4}-\d{2}-\d{2}$/.test(value) && value.slice(0, 4) >= '1000') {
        this.form.controls.dueDate.markAsTouched();
        this.cdr.markForCheck();
      }
    });
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
