import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Task, UpdateTaskRequest } from '../data/task.model';
import { DUE_DATE_IN_PAST_MESSAGE, TaskForm } from './task-form';

describe('TaskForm', () => {
  let fixture: ComponentFixture<TaskForm>;
  let el: HTMLElement;
  let emitted: UpdateTaskRequest[];

  const q = <T extends HTMLElement = HTMLInputElement>(sel: string) => el.querySelector<T>(sel)!;
  function type(input: HTMLInputElement | HTMLTextAreaElement, value: string) {
    input.value = value;
    input.dispatchEvent(new Event('input'));
  }
  const submit = () => q<HTMLButtonElement>('button[type=submit]').click();

  beforeEach(() => {
    fixture = TestBed.createComponent(TaskForm);
    el = fixture.nativeElement as HTMLElement;
    emitted = [];
    fixture.componentInstance.submitted.subscribe((r) => emitted.push(r));
    fixture.detectChanges();
  });

  it('TaskForm: title is required', () => {
    submit();
    fixture.detectChanges();
    expect(emitted).toEqual([]);
    expect(el.textContent).toContain('Title is required.');
  });

  it('TaskForm: whitespace-only title is rejected', () => {
    type(q('input[name=title]'), '   ');
    submit();
    fixture.detectChanges();
    expect(emitted).toEqual([]);
    expect(el.textContent).toContain('Title is required.');
  });

  it('TaskForm: title max 200', () => {
    type(q('input[name=title]'), 'a'.repeat(201));
    submit();
    fixture.detectChanges();
    expect(emitted).toEqual([]);
    expect(el.textContent).toContain('Title must be at most 200 characters.');
  });

  it('TaskForm: description max 2000', () => {
    type(q('input[name=title]'), 'ok');
    type(q<HTMLTextAreaElement>('textarea[name=description]'), 'a'.repeat(2001));
    submit();
    fixture.detectChanges();
    expect(emitted).toEqual([]);
    expect(el.textContent).toContain('Description must be at most 2000 characters.');
  });

  it('TaskForm: emits trimmed request with default status ToDo', () => {
    type(q('input[name=title]'), '  Buy milk  ');
    submit();
    expect(emitted).toEqual([{ title: 'Buy milk', description: null, status: 'ToDo', dueDate: null }]);
  });

  it('TaskForm: emits description and due date', () => {
    type(q('input[name=title]'), 'T');
    type(q<HTMLTextAreaElement>('textarea[name=description]'), '  details ');
    type(q('input[name=dueDate]'), '2099-12-31');
    submit();
    expect(emitted).toEqual([{ title: 'T', description: 'details', status: 'ToDo', dueDate: '2099-12-31' }]);
  });

  it('TaskForm: due date min is the local date of today', () => {
    const d = new Date();
    const pad = (n: number) => String(n).padStart(2, '0');
    const today = `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
    const input = q('input[name=dueDate]');
    expect(input.type).toBe('date');
    expect(input.min).toBe(today);
  });

  it('TaskForm: rejects a due date before local today with the server message', () => {
    type(q('input[name=title]'), 'T');
    type(q('input[name=dueDate]'), '2000-01-01');
    submit();
    fixture.detectChanges();
    expect(emitted).toEqual([]);
    expect(el.textContent).toContain(DUE_DATE_IN_PAST_MESSAGE);
  });

  it('TaskForm: accepts local today as due date', () => {
    const d = new Date();
    const pad = (n: number) => String(n).padStart(2, '0');
    const today = `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
    type(q('input[name=title]'), 'T');
    type(q('input[name=dueDate]'), today);
    submit();
    expect(emitted.length).toBe(1);
  });

  it('TaskForm: recomputes the min date after reset (no stale min after midnight)', () => {
    vi.useFakeTimers({ toFake: ['Date'] });
    try {
      vi.setSystemTime(new Date(2099, 0, 5, 12));
      fixture.componentInstance.reset();
      fixture.detectChanges();
      expect(q('input[name=dueDate]').min).toBe('2099-01-05');
    } finally {
      vi.useRealTimers();
    }
  });

  it('TaskForm: displays server field errors under matching controls', () => {
    fixture.componentRef.setInput('serverErrors', {
      title: ['Title is taken.'],
      dueDate: [DUE_DATE_IN_PAST_MESSAGE],
    });
    fixture.detectChanges();
    fixture.detectChanges();

    const fields = Array.from(el.querySelectorAll('mat-form-field'));
    const titleField = fields.find((f) => f.querySelector('input[name=title]'))!;
    const dueField = fields.find((f) => f.querySelector('input[name=dueDate]'))!;
    expect(titleField.textContent).toContain('Title is taken.');
    expect(dueField.textContent).toContain(DUE_DATE_IN_PAST_MESSAGE);
  });

  it('TaskForm: reset clears the form', () => {
    type(q('input[name=title]'), 'abc');
    fixture.componentInstance.reset();
    fixture.detectChanges();
    expect(q('input[name=title]').value).toBe('');
  });

  it('TaskForm: disables submit while submitting', () => {
    fixture.componentRef.setInput('submitting', true);
    fixture.detectChanges();
    expect(q<HTMLButtonElement>('button[type=submit]').disabled).toBe(true);
  });

  it('TaskForm: status defaults to To do', () => {
    const select = q<HTMLSelectElement>('select[name=status]');
    expect(select.value).toBe('ToDo');
    expect(select.selectedOptions[0].textContent?.trim()).toBe('To do');
  });

  it('TaskForm: shows the past-date reason as soon as a past date is entered', () => {
    type(q('input[name=dueDate]'), '2000-01-01');
    fixture.detectChanges();
    expect(el.textContent).toContain(DUE_DATE_IN_PAST_MESSAGE);
  });

  it('TaskForm: does not show the past-date error while the year is still being typed', () => {
    type(q('input[name=dueDate]'), '0002-11-10');
    fixture.detectChanges();
    expect(el.textContent).not.toContain(DUE_DATE_IN_PAST_MESSAGE);
    expect(fixture.componentInstance['form'].controls.dueDate.touched).toBe(false);

    type(q('input[name=dueDate]'), '2000-01-01');
    fixture.detectChanges();
    expect(el.textContent).toContain(DUE_DATE_IN_PAST_MESSAGE);
  });

  it('TaskForm: shows the past-date reason on blur for a year below 1000', () => {
    const input = q('input[name=dueDate]');
    type(input, '0999-01-01');
    fixture.detectChanges();
    expect(el.textContent).not.toContain(DUE_DATE_IN_PAST_MESSAGE);

    input.dispatchEvent(new Event('blur'));
    fixture.detectChanges();
    expect(el.textContent).toContain(DUE_DATE_IN_PAST_MESSAGE);
  });

  // Same literal as TaskItemTests.Create_pastDueDateMessage_tellsTheUserWhatToDo (server side, spec D18).
  // The two sides share no code, so change both tests and both constants together.
  it('TaskForm: past-date message matches the server text', () => {
    expect(DUE_DATE_IN_PAST_MESSAGE).toBe('Due date cannot be in the past. Choose today or a later date.');
  });

  it('TaskForm: due date shows the today-or-later hint', () => {
    expect(el.querySelector('mat-hint')!.textContent).toContain('Today or later.');
  });

  it('TaskForm: clearing a past due date clears the error', () => {
    type(q('input[name=dueDate]'), '2000-01-01');
    fixture.detectChanges();
    expect(el.textContent).toContain(DUE_DATE_IN_PAST_MESSAGE);
    type(q('input[name=dueDate]'), '');
    type(q('input[name=title]'), 'T');
    submit();
    fixture.detectChanges();
    expect(emitted.length).toBe(1);
    expect(emitted[0].dueDate).toBeNull();
    expect(el.textContent).not.toContain(DUE_DATE_IN_PAST_MESSAGE);
  });

  it('TaskForm: accepts 2000-char description with surrounding whitespace', () => {
    type(q('input[name=title]'), 'T');
    type(q<HTMLTextAreaElement>('textarea[name=description]'), '  ' + 'a'.repeat(2000) + '\n');
    submit();
    expect(emitted.length).toBe(1);
    expect(emitted[0].description!.length).toBe(2000);
  });

  it('TaskForm: accepts 200-char title with surrounding whitespace', () => {
    type(q('input[name=title]'), '  ' + 'a'.repeat(200) + '\n');
    submit();
    expect(emitted.length).toBe(1);
    expect(emitted[0].title.length).toBe(200);
  });

  it('TaskForm: reset leaves the due date untouched', () => {
    type(q('input[name=dueDate]'), '2099-12-31');
    fixture.componentInstance.reset();
    expect(fixture.componentInstance['form'].controls.dueDate.touched).toBe(false);
  });

  it('TaskForm: reset after a valid submit shows no validation errors', () => {
    type(q('input[name=title]'), 'Done already');
    submit();
    fixture.componentInstance.reset();
    fixture.detectChanges();
    expect(emitted.length).toBe(1);
    expect(el.textContent).not.toContain('Title is required.');
  });

  describe('edit mode', () => {
    const task = (over: Partial<Task> = {}): Task => ({
      id: 't1',
      title: 'Old title',
      description: 'Old description',
      status: 'InProgress',
      dueDate: '2099-06-01',
      createdAt: '2026-01-01T00:00:00Z',
      updatedAt: '2026-01-01T00:00:00Z',
      ...over,
    });
    const edit = (t: Task | null) => {
      fixture.componentRef.setInput('task', t);
      fixture.detectChanges();
    };
    const heading = () => el.querySelector('mat-card-title')!.textContent!.trim();
    const cancelButton = () =>
      Array.from(el.querySelectorAll<HTMLButtonElement>('button[type=button]')).find(
        (b) => b.textContent!.trim() === 'Cancel',
      );

    it('TaskForm: edit mode prefills fields and shows Edit task and Save changes', () => {
      edit(task());
      expect(q('input[name=title]').value).toBe('Old title');
      expect(q<HTMLTextAreaElement>('textarea[name=description]').value).toBe('Old description');
      expect(q<HTMLSelectElement>('select[name=status]').value).toBe('InProgress');
      expect(q('input[name=dueDate]').value).toBe('2099-06-01');
      expect(heading()).toBe('Edit task');
      expect(q<HTMLButtonElement>('button[type=submit]').textContent!.trim()).toBe('Save changes');

      edit(task({ id: 't2', description: null, dueDate: null }));
      expect(q<HTMLTextAreaElement>('textarea[name=description]').value).toBe('');
      expect(q('input[name=dueDate]').value).toBe('');
    });

    it('TaskForm: edit mode focuses the title input', async () => {
      document.body.appendChild(el);
      try {
        edit(task());
        await fixture.whenStable();
        expect(document.activeElement).toBe(q('input[name=title]'));
      } finally {
        el.remove();
      }
    });

    it('TaskForm: edit mode emits all fields on save', () => {
      edit(task());
      type(q('input[name=title]'), '  New title ');
      submit();
      expect(emitted).toEqual([
        { title: 'New title', description: 'Old description', status: 'InProgress', dueDate: '2099-06-01' },
      ]);
    });

    it("TaskForm: edit mode accepts the task's unchanged past due date", () => {
      edit(task({ dueDate: '2000-01-01' }));
      submit();
      fixture.detectChanges();
      expect(emitted.length).toBe(1);
      expect(emitted[0].dueDate).toBe('2000-01-01');
      expect(el.textContent).not.toContain('cannot be in the past');
    });

    it('TaskForm: edit mode rejects a newly picked past date', () => {
      edit(task({ dueDate: '2000-01-01' }));
      type(q('input[name=dueDate]'), '2000-01-02');
      submit();
      fixture.detectChanges();
      expect(emitted).toEqual([]);
      expect(el.textContent).toContain('Due date cannot be in the past.');
    });

    it('TaskForm: edit mode shows the same validation messages', () => {
      edit(task());
      type(q('input[name=title]'), '');
      submit();
      fixture.detectChanges();
      expect(el.textContent).toContain('Title is required.');

      type(q('input[name=title]'), 'a'.repeat(201));
      submit();
      fixture.detectChanges();
      expect(el.textContent).toContain('Title must be at most 200 characters.');
      expect(emitted).toEqual([]);
    });

    it('TaskForm: Cancel emits cancelled and is hidden in add mode', () => {
      expect(cancelButton()).toBeUndefined();
      let cancelled = 0;
      fixture.componentInstance.cancelled.subscribe(() => cancelled++);
      edit(task());
      cancelButton()!.click();
      expect(cancelled).toBe(1);
      expect(emitted).toEqual([]);
    });

    it('TaskForm: switching task replaces values and clears server errors', () => {
      edit(task());
      fixture.componentRef.setInput('serverErrors', { title: ['Server says no.'] });
      fixture.detectChanges();
      fixture.detectChanges();
      expect(el.textContent).toContain('Server says no.');

      edit(task({ id: 't2', title: 'Other task' }));
      fixture.detectChanges();
      expect(q('input[name=title]').value).toBe('Other task');
      expect(el.textContent).not.toContain('Server says no.');
    });

    it('TaskForm: leaving edit mode after a save shows no validation errors', () => {
      edit(task());
      submit();
      edit(null);
      fixture.detectChanges();
      expect(emitted.length).toBe(1);
      expect(el.textContent).not.toContain('Title is required.');
    });

    it('TaskForm: setting task to null returns to an empty Add a task form', () => {
      edit(task());
      edit(null);
      expect(heading()).toBe('Add a task');
      expect(q('input[name=title]').value).toBe('');
      expect(q<HTMLSelectElement>('select[name=status]').value).toBe('ToDo');
      expect(q<HTMLButtonElement>('button[type=submit]').textContent!.trim()).toBe('Add task');
    });
  });
});
