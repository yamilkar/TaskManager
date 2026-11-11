import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  ElementRef,
  Injector,
  afterNextRender,
  effect,
  inject,
  input,
  output,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import { AbstractControl, FormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { MatButton } from '@angular/material/button';
import { MatCard, MatCardContent, MatCardHeader, MatCardTitle } from '@angular/material/card';
import { MatError, MatFormField, MatLabel } from '@angular/material/form-field';
import { MatInput } from '@angular/material/input';
import { FormError } from '../../../shared/ui/form-error/form-error';
import { Task, TaskStatus, UpdateTaskRequest } from '../data/task.model';

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
  private readonly injector = inject(Injector);
  private readonly titleInput = viewChild.required<ElementRef<HTMLInputElement>>('titleInput');

  /** The task being edited; null means the form adds a new task. */
  readonly task = input<Task | null>(null);
  readonly submitting = input(false);
  readonly serverErrors = input<Record<string, string[]>>({});
  readonly submitted = output<UpdateTaskRequest>();
  readonly cancelled = output<void>();

  protected readonly today = signal(localToday());
  protected readonly form = inject(FormBuilder).nonNullable.group({
    title: ['', [Validators.required, notBlank, Validators.maxLength(200)]],
    description: ['', [Validators.maxLength(2000)]],
    status: ['ToDo' as TaskStatus],
    // Spec D24: the edited task's own due date stays valid even once it is in the past.
    dueDate: ['', [(c: AbstractControl) => (c.value === this.task()?.dueDate ? null : notBeforeLocalToday(c))]],
  });

  constructor() {
    effect(() => {
      const task = this.task();
      untracked(() => {
        if (task) {
          this.form.reset({
            title: task.title,
            description: task.description ?? '',
            status: task.status,
            dueDate: task.dueDate ?? '',
          });
          this.today.set(localToday());
          this.cdr.markForCheck();
          afterNextRender(() => this.titleInput().nativeElement.focus(), { injector: this.injector });
        } else {
          this.reset();
        }
      });
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

  /** Empties the form; also used by the parent after a successful create. */
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
