import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CreateTaskRequest } from '../data/task.model';
import { DUE_DATE_IN_PAST_MESSAGE, TaskForm } from './task-form';

describe('TaskForm', () => {
  let fixture: ComponentFixture<TaskForm>;
  let el: HTMLElement;
  let emitted: CreateTaskRequest[];

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
      dueDate: ['Due date cannot be in the past.'],
    });
    fixture.detectChanges();
    fixture.detectChanges();

    const fields = Array.from(el.querySelectorAll('mat-form-field'));
    const titleField = fields.find((f) => f.querySelector('input[name=title]'))!;
    const dueField = fields.find((f) => f.querySelector('input[name=dueDate]'))!;
    expect(titleField.textContent).toContain('Title is taken.');
    expect(dueField.textContent).toContain('Due date cannot be in the past.');
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

  it('TaskForm: past-date message matches the server text', () => {
    expect(DUE_DATE_IN_PAST_MESSAGE).toBe('Due date cannot be in the past. Choose today or a later date.');
  });

  it('TaskForm: due date shows the today-or-later hint', () => {
    expect(el.querySelector('mat-hint')!.textContent).toContain('Today or later.');
  });

  it('TaskForm: clearing a past due date clears the error', () => {
    type(q('input[name=dueDate]'), '2000-01-01');
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
});
